﻿using Android.App;
using Android.Content;
using Android.Media;
using Android.OS;
using Android.Support.V4.App;
using Android.Util;
using Android.Widget;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using MoreLinq;
using Stream = System.IO.Stream;
using System.Linq;

namespace TFlix.Services
{
    [Service(Exported = false, Name = "com.toddy.tflix.DownloadService")]
    [IntentFilter(new string[] { "com.toddy.tflix.DownloadService" })]
    public class DownloadFilesService : IntentService
    {
        static readonly string TAG = "DownloadFilesService";
        public IBinder Binder { get; private set; }

        private const string CHANNEL_ID = "250801";
        private int DownloadPos = 0;

        private List<bool> Stop = new List<bool>();
        private List<bool> Pause = new List<bool>();
        private List<NotificationCompat.Builder> Builders = new List<NotificationCompat.Builder>();
        private List<RemoteViews> BigRemoteView = new List<RemoteViews>();
        private List<RemoteViews> SmallRemoteView = new List<RemoteViews>();

        private NotificationManager notificationManager;

        protected override void OnHandleIntent(Intent intent)
        {
            bool IsRequestingStop;
            bool IsRequestingPauseResume;

            try
            {
                try
                {
                    IsRequestingStop = intent.Extras.GetBoolean("IsRequestingStop");
                }
                catch
                {
                    IsRequestingStop = false;
                }

                try
                {
                    IsRequestingPauseResume = intent.Extras.GetBoolean("IsRequestingPauseResume");
                }
                catch
                {
                    IsRequestingPauseResume = false;
                }

                if (!IsRequestingStop)
                {
                    if (!IsRequestingPauseResume)
                    {
                        int Pos;
                        string URL = intent.Extras.GetString("DownloadURL");
                        bool IsFromSearch = intent.Extras.GetBoolean("IsFromSearch", false);

                        string FullTitle;
                        string Thumb;
                        string Show;
                        string ShowThumb;
                        int ShowSeason;
                        int Ep;
                        bool IsSubtitled;

                        int downloadPos = DownloadPos;
                        DownloadPos++;

                        Log.Debug(TAG, "OnStartCommand");

                        var NOTIFICATION_ID = Utils.RequestCode.ID();

                        Pos = intent.Extras.GetInt("DownloadSHOWID");

                        if (!IsFromSearch)
                        {
                            if (!Directory.Exists(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal) + "/ShowThumbnail"))
                                Directory.CreateDirectory(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal) + "/ShowThumbnail");

                            (Show, ShowSeason, Ep) = Utils.Utils.BreakFullTitleInParts(List.GetMainPageSeries.Series[Pos].Title);

                            FullTitle = List.GetMainPageSeries.Series[Pos].Title;
                            Thumb = List.GetMainPageSeries.Series[Pos].EPThumb;

                            List.GetMainPageSeries.Series[Pos].Downloading = true;

                            var thumbpath = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal) + "/ShowThumbnail", Show);
                            try
                            {
                                if (!File.Exists(thumbpath))
                                    System.IO.File.WriteAllBytes(thumbpath, Base64.Decode(List.GetMainPageSeries.Series[Pos].IMG64, Base64Flags.UrlSafe));
                            }
                            catch { }
                            ShowThumb = thumbpath;
                        }
                        else
                        {
                            if (!Directory.Exists(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal) + "/ShowThumbnail"))
                                Directory.CreateDirectory(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal) + "/ShowThumbnail");

                            (Show, ShowSeason, Ep) = Utils.Utils.BreakFullTitleInParts(List.GetSearch.Search[Pos].Title);


                            FullTitle = List.GetSearch.Search[Pos].Title.Replace("Online,", "Online ");
                            Thumb = List.GetSearch.Search[Pos].EPThumb;

                            List.GetSearch.Search[Pos].Downloading = true;

                            var thumbpath = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal) + "/ShowThumbnail", Show);
                            if (!File.Exists(thumbpath))
                                System.IO.File.WriteAllBytes(thumbpath, Base64.Decode(List.GetSearch.Search[Pos].IMG64, Base64Flags.UrlSafe));
                            ShowThumb = thumbpath;
                        }

                        if (FullTitle.ToLower().Contains("legendado"))
                            IsSubtitled = true;
                        else
                            IsSubtitled = false;

                        Stop.Insert(downloadPos, false);
                        Pause.Insert(downloadPos, false);

                        DownloadFile(URL, NOTIFICATION_ID, FullTitle, Show, Ep, ShowSeason, IsSubtitled, Thumb, ShowThumb, downloadPos, IsFromSearch, Pos);
                    }
                    else
                    {
                        bool pause = intent.Extras.GetBoolean("PauseResume");
                        int notificationID = intent.Extras.GetInt("NotificationID");
                        int downloadPos = intent.Extras.GetInt("DownloadPos");
                        int listPos = intent.Extras.GetInt("ListPos");
                        bool IsFromSearch = intent.Extras.GetBoolean("IsFromSearch", false);
                        var builder = Builders[downloadPos];

                        string URL = intent.Extras.GetString("URL");
                        string FullTitle = intent.Extras.GetString("FullTitle");
                        string Show = intent.Extras.GetString("Show");
                        int Ep = intent.Extras.GetInt("Ep");
                        int ShowSeason = intent.Extras.GetInt("ShowSeason");
                        bool IsSubtitled = intent.Extras.GetBoolean("IsSubtitled");
                        string Thumb = intent.Extras.GetString("Thumb");
                        string ShowThumb = intent.Extras.GetString("ShowThumb");

                        NotificationCompat.Action pauseresumeAction;
                        NotificationCompat.Action cancelAction;
                        Intent broadcastPauseResumeIntent = new Intent(this, typeof(DownloadFileBroadcastListener));
                        Intent broadcastCancelIntent = new Intent(this, typeof(DownloadFileBroadcastListener));

                        builder.MActions.Clear();

                        broadcastPauseResumeIntent.SetAction("com.toddy.tflix.PAUSERESUMEDOWNLOAD");
                        broadcastPauseResumeIntent.PutExtra("NotificationID", notificationID);
                        broadcastPauseResumeIntent.PutExtra("PauseResume", !pause);
                        broadcastPauseResumeIntent.PutExtra("DownloadPos", downloadPos);
                        broadcastPauseResumeIntent.PutExtra("ListPos", listPos);
                        broadcastPauseResumeIntent.PutExtra("IsFromSearch", IsFromSearch);

                        broadcastPauseResumeIntent.PutExtra("URL", URL);
                        broadcastPauseResumeIntent.PutExtra("FullTitle", FullTitle);
                        broadcastPauseResumeIntent.PutExtra("Show", Show);
                        broadcastPauseResumeIntent.PutExtra("Ep", Ep);
                        broadcastPauseResumeIntent.PutExtra("ShowSeason", ShowSeason);
                        broadcastPauseResumeIntent.PutExtra("IsSubtitled", IsSubtitled);
                        broadcastPauseResumeIntent.PutExtra("Thumb", Thumb);
                        broadcastPauseResumeIntent.PutExtra("ShowThumb", ShowThumb);

                        broadcastCancelIntent.SetAction("com.toddy.tflix.CANCELDOWNLOAD");
                        broadcastCancelIntent.PutExtra("NotificationID", notificationID);
                        broadcastCancelIntent.PutExtra("DownloadPos", downloadPos);
                        broadcastCancelIntent.PutExtra("ListPos", listPos);
                        broadcastCancelIntent.PutExtra("IsFromSearch", IsFromSearch);
                        broadcastCancelIntent.PutExtra("Show", Show);
                        broadcastCancelIntent.PutExtra("Ep", Ep);
                        broadcastCancelIntent.PutExtra("ShowSeason", ShowSeason);
                        broadcastCancelIntent.PutExtra("IsSubtitled", IsSubtitled);

                        PendingIntent pauseresumeDownloadPI = PendingIntent.GetBroadcast(this, Utils.RequestCode.ID(), broadcastPauseResumeIntent, PendingIntentFlags.CancelCurrent);
                        PendingIntent cancelDownloadPI = PendingIntent.GetBroadcast(this, Utils.RequestCode.ID(), broadcastCancelIntent, PendingIntentFlags.CancelCurrent);

                        Pause[downloadPos] = pause;

                        if (pause)
                        {
                            pauseresumeAction = new NotificationCompat.Action(0, "Continuar", pauseresumeDownloadPI);
                            builder.SetSmallIcon(Android.Resource.Drawable.StatSysDownloadDone);
                        }
                        else
                        {
                            pauseresumeAction = new NotificationCompat.Action(0, "Pausar", pauseresumeDownloadPI);
                            builder.SetSmallIcon(Android.Resource.Drawable.StatSysDownload);
                            //DownloadFile(URL, notificationID, FullTitle, Show, Ep, ShowSeason, IsSubtitled, Thumb, ShowThumb, downloadPos);
                        }

                        cancelAction = new NotificationCompat.Action(0, "Cancelar", cancelDownloadPI);

                        builder.AddAction(pauseresumeAction)
                                    .AddAction(cancelAction);
                        notificationManager.Notify(notificationID, builder.Build());

                        Builders[downloadPos] = builder;

                        if (!pause)
                            DownloadFile(URL, notificationID, FullTitle, Show, Ep, ShowSeason, IsSubtitled, Thumb, ShowThumb, downloadPos, IsFromSearch, listPos);
                    }
                }
                else
                {
                    var downloadPos = intent.Extras.GetInt("DownloadPos");
                    var NOTIFICATION_ID = intent.Extras.GetInt("NotificationID");
                    var IsSubtitled = intent.Extras.GetBoolean("IsSubtitled", false);
                    var IsFromSearch = intent.Extras.GetBoolean("IsFromSearch", false);
                    var Show = intent.Extras.GetString("Show");
                    var Ep = intent.Extras.GetInt("Ep");
                    var ShowSeason = intent.Extras.GetInt("ShowSeason");
                    var listPos = intent.Extras.GetInt("ListPos");
                    var _Path = Utils.Database.GetVideoPath(IsSubtitled, Show, Ep, ShowSeason);

                    Stop[downloadPos] = true;

                    if (Pause[downloadPos])
                        CancelDownload(NOTIFICATION_ID, IsSubtitled, Show, Ep, ShowSeason, _Path, IsFromSearch, listPos);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);

                var builder = new NotificationCompat.Builder(this, CHANNEL_ID)
                   .SetSmallIcon(Android.Resource.Drawable.StatSysDownloadDone)
                   .SetContentText("Falha no download.")
                   .SetStyle(new NotificationCompat.DecoratedCustomViewStyle())
                   .SetColor(Android.Graphics.Color.ParseColor("#FFD80C0C"))
                   .SetPriority((int)NotificationPriority.Low)
                   .SetOngoing(false)
                   .SetOnlyAlertOnce(true);

                notificationManager.Notify(Utils.RequestCode.ID(), builder.Build());
            }

        }

        public override void OnCreate()
        {
            base.OnCreate();
            Log.Debug(TAG, "OnCreate");
        }

        public override IBinder OnBind(Intent intent)
        {
            Log.Debug(TAG, "Onbind");
            notificationManager = (NotificationManager)GetSystemService(NotificationService);
            Binder = new DownloadFilesBinder(this);
            return Binder;
        }

        public override void OnDestroy()
        {
            notificationManager.CancelAll();
            Binder = null;
            base.OnDestroy();
        }

        private void CreateNotification(int NOTIFICATION_ID, string Show, int ShowSeason, int Ep, long bytes_total, int downloadPos, string URL, string FullTitle, bool IsSubtitled, string Thumb, string ShowThumb, bool IsFromSearch, int listPos)
        {
            Log.Info(TAG, "Notification Created With ID {0} AND Show {1}", NOTIFICATION_ID, Show);

            RemoteViews notificationLayout;
            RemoteViews smallNotificationLayout;

            Intent broadcastPauseResumeIntent = new Intent(this, typeof(DownloadFileBroadcastListener));
            Intent broadcastCancelIntent = new Intent(this, typeof(DownloadFileBroadcastListener));

            broadcastPauseResumeIntent.SetAction("com.toddy.tflix.PAUSERESUMEDOWNLOAD");
            broadcastPauseResumeIntent.PutExtra("NotificationID", NOTIFICATION_ID);
            broadcastPauseResumeIntent.PutExtra("PauseResume", true);
            broadcastPauseResumeIntent.PutExtra("DownloadPos", downloadPos);
            broadcastPauseResumeIntent.PutExtra("ListPos", listPos);
            broadcastPauseResumeIntent.PutExtra("IsFromSearch", IsFromSearch);

            broadcastPauseResumeIntent.PutExtra("URL", URL);
            broadcastPauseResumeIntent.PutExtra("FullTitle", FullTitle);
            broadcastPauseResumeIntent.PutExtra("Show", Show);
            broadcastPauseResumeIntent.PutExtra("Ep", Ep);
            broadcastPauseResumeIntent.PutExtra("ShowSeason", ShowSeason);
            broadcastPauseResumeIntent.PutExtra("IsSubtitled", IsSubtitled);
            broadcastPauseResumeIntent.PutExtra("Thumb", Thumb);
            broadcastPauseResumeIntent.PutExtra("ShowThumb", ShowThumb);

            broadcastCancelIntent.SetAction("com.toddy.tflix.CANCELDOWNLOAD");
            broadcastCancelIntent.PutExtra("NotificationID", NOTIFICATION_ID);
            broadcastCancelIntent.PutExtra("DownloadPos", downloadPos);
            broadcastCancelIntent.PutExtra("ListPos", listPos);
            broadcastCancelIntent.PutExtra("IsFromSearch", IsFromSearch);
            broadcastCancelIntent.PutExtra("Show", Show);
            broadcastCancelIntent.PutExtra("Ep", Ep);
            broadcastCancelIntent.PutExtra("ShowSeason", ShowSeason);
            broadcastCancelIntent.PutExtra("IsSubtitled", IsSubtitled);

            PendingIntent pauseresumeDownloadPI = PendingIntent.GetBroadcast(this, Utils.RequestCode.ID(), broadcastPauseResumeIntent, PendingIntentFlags.CancelCurrent);
            PendingIntent cancelDownloadPI = PendingIntent.GetBroadcast(this, Utils.RequestCode.ID(), broadcastCancelIntent, PendingIntentFlags.CancelCurrent);

            NotificationCompat.Action pauseresumeAction = new NotificationCompat.Action(0, "Pausar", pauseresumeDownloadPI);
            NotificationCompat.Action cancelAction = new NotificationCompat.Action(0, "Cancelar", cancelDownloadPI);

            notificationLayout = new RemoteViews(PackageName, Resource.Layout.download_notification);
            smallNotificationLayout = new RemoteViews(PackageName, Resource.Layout.download_notification_smallview);

            notificationLayout.SetTextViewText(Resource.Id.downloadn_title, Show);
            notificationLayout.SetTextViewText(Resource.Id.downloadn_se, string.Format("T{0}:E{1}", ShowSeason, Ep));
            notificationLayout.SetTextViewText(Resource.Id.downloadn_perc, string.Format("{0}% ({1} MB/{2})", 0, 0, Utils.Utils.Size(bytes_total)));
            notificationLayout.SetProgressBar(Resource.Id.downloadn_progress, 100, 0, false);

            smallNotificationLayout.SetTextViewText(Resource.Id.downloadn_titles, Show);
            smallNotificationLayout.SetTextViewText(Resource.Id.downloadn_percs, string.Format("{0}%", 0));
            smallNotificationLayout.SetProgressBar(Resource.Id.downloadn_progresss, 100, 0, false);

            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                NotificationChannel chan = new NotificationChannel(CHANNEL_ID, "DownloadContent", NotificationImportance.Low);
                chan.SetSound(null, null);
                chan.SetShowBadge(false);
                chan.EnableLights(false);
                chan.EnableVibration(false);
                notificationManager.CreateNotificationChannel(chan);
            }

            try
            {
                var index = List.GetDownloads.Series.FindIndex(x => x.Show == Show && x.IsSubtitled == IsSubtitled);
                var epIndex = List.GetDownloads.Series[index].Episodes.FindIndex(x => x.EP == Ep && x.ShowSeason == ShowSeason);

                List.GetDownloads.Series[index].Episodes[epIndex].downloadInfo.IsDownloading = true;
                List.GetDownloads.Series[index].Episodes[epIndex].downloadInfo.DownloadPos = downloadPos;
                List.GetDownloads.Series[index].Episodes[epIndex].downloadInfo.IsFromSearch = IsFromSearch;
                List.GetDownloads.Series[index].Episodes[epIndex].downloadInfo.URL = URL;
                List.GetDownloads.Series[index].Episodes[epIndex].downloadInfo.FullTitle = FullTitle;
                List.GetDownloads.Series[index].Episodes[epIndex].downloadInfo.ListPos = listPos;
                List.GetDownloads.Series[index].Episodes[epIndex].downloadInfo.NotificationID = NOTIFICATION_ID;
                List.GetDownloads.Series[index].Episodes[epIndex].downloadInfo.DownloadPos = downloadPos;
            }
            catch { }

            var builder = new NotificationCompat.Builder(this, CHANNEL_ID)
               .SetSmallIcon(Android.Resource.Drawable.StatSysDownload)
               .SetCustomBigContentView(notificationLayout)
               .SetCustomContentView(smallNotificationLayout)
               .AddAction(pauseresumeAction)
               .AddAction(cancelAction)
               .SetStyle(new NotificationCompat.DecoratedCustomViewStyle())
               .SetColor(Android.Graphics.Color.ParseColor("#FFD80C0C"))
               .SetPriority((int)NotificationPriority.Low)
               .SetOngoing(true)
               .SetOnlyAlertOnce(true);

            Builders.Insert(downloadPos, builder);
            SmallRemoteView.Insert(downloadPos, smallNotificationLayout);
            BigRemoteView.Insert(downloadPos, notificationLayout);
            notificationManager.Notify(NOTIFICATION_ID, builder.Build());
        }


        private void DownloadFile(string URL, int NOTIFICATION_ID, string FullTitle, string Show, int Ep, int ShowSeason, bool IsSubtitled, string Thumb, string ShowThumb, int downloadPos, bool IsFromSearch, int listPos)
        {
            long Duration = 0;
            long prevTotalBytes = 0;
            long bytes_total = 1;

            if (!Directory.Exists(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal) + "/Series"))
                Directory.CreateDirectory(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal) + "/Series");

            var _Path = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal) + "/Series", FullTitle);

            var startRange = Utils.Database.GetDownloadedBytes(IsSubtitled, Show, Ep, ShowSeason);

            WebClient header = new WebClient();
            HttpWebRequest download = (HttpWebRequest)WebRequest.Create(URL);

            download.Method = "GET";
            download.Timeout = 200000;

            try
            {
                prevTotalBytes = Utils.Database.GetTotalBytes(IsSubtitled, Show, Ep, ShowSeason);
            }
            catch { }

            try
            {
                MediaMetadataRetriever reader = new MediaMetadataRetriever();
                header.OpenRead(URL);
                bytes_total = long.Parse(header.ResponseHeaders["Content-Length"]);

                reader.SetDataSource(URL, new Dictionary<string, string>());
                Duration = long.Parse(reader.ExtractMetadata(MetadataKey.Duration));

                reader.Release();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
                this.StopSelf();
            }

            if (prevTotalBytes != 0 && prevTotalBytes != bytes_total)
            {
                Utils.Database.DeleteItem(IsSubtitled, Show, Ep, ShowSeason);
                File.Delete(_Path);
                startRange = 0;
            }

            download.AddRange(startRange);

            if (!Utils.Database.IsSeasonOnDB(ShowSeason, Show, IsSubtitled))
                Utils.Database.InsertData("", Show, ShowThumb, ShowSeason, 0, 0, 0, IsSubtitled, "", 0);
            Utils.Database.InsertData(Thumb, Show, ShowThumb, ShowSeason, Ep, 0, bytes_total, IsSubtitled, System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal) + "/Series", FullTitle), Duration);

            try
            {
                Utils.Database.ReadDB();

                var index = List.GetDownloads.Series.FindIndex(x => x.Show == Show && x.IsSubtitled == IsSubtitled);
                var epIndex = List.GetDownloads.Series[index].Episodes.FindIndex(x => x.EP == Ep && x.ShowSeason == ShowSeason);
                List.GetDownloads.Series[index].Episodes[epIndex].downloadInfo.IsDownloading = true;
                List.GetDownloads.Series[index].Episodes[epIndex].downloadInfo.DownloadPos = downloadPos;
                List.GetDownloads.Series[index].Episodes[epIndex].downloadInfo.IsFromSearch = IsFromSearch;

                //Activities.DetailedDownloads.ReloadDataset();
            }
            catch { }

            if (downloadPos >= Builders.Count)
                CreateNotification(NOTIFICATION_ID, Show, ShowSeason, Ep, bytes_total, downloadPos, URL, FullTitle, IsSubtitled, Thumb, ShowThumb, IsFromSearch, listPos);

            download.BeginGetResponse(new AsyncCallback(result => PlayResponseAsync(result, NOTIFICATION_ID, startRange, _Path, bytes_total, Show, ShowSeason, Ep, IsSubtitled, downloadPos, IsFromSearch, listPos, URL, FullTitle)), download);

        }

        private void DownloadFileCompleted(int NOTIFICATION_ID, string Show, int ShowSeason, int Ep, long bytes_total, bool IsSubtitled)
        {
            Utils.Database.UpdateProgress(Show, ShowSeason, Ep, 100, bytes_total, IsSubtitled);

            try
            {
                var index = List.GetDownloads.Series.FindIndex(x => x.Show == Show && x.IsSubtitled == IsSubtitled);
                var epIndex = List.GetDownloads.Series[index].Episodes.FindIndex(x => x.EP == Ep && x.ShowSeason == ShowSeason);
                List.GetDownloads.Series[index].Episodes[epIndex].Progress = 100;
                List.GetDownloads.Series[index].Episodes[epIndex].downloadInfo.IsDownloading = false;
            }
            catch { }

            var builder = new NotificationCompat.Builder(this, CHANNEL_ID)
               .SetSmallIcon(Resource.Drawable.baseline_mobile_friendly_24)
               .SetContentText("Download Completo!")
               .SetStyle(new NotificationCompat.DecoratedCustomViewStyle())
               .SetColor(Android.Graphics.Color.ParseColor("#FFD80C0C"))
               .SetPriority((int)NotificationPriority.Low)
               .SetOngoing(false)
               .SetOnlyAlertOnce(true);

            notificationManager.Notify(NOTIFICATION_ID, builder.Build());
        }

        private int DownloadProgressChanged(long received, int NOTIFICATION_ID, int PreviousPercentage, long bytes_total, string Show, int ShowSeason, int Ep, bool IsSubtitled, int downloadPos, bool IsFromSearch, string URL, string FullTitle, int listPos)
        {
            int percentage = (int)(received / (bytes_total / 100));

            //builder.SetOngoing(true);

            try
            {
                if (IsFromSearch)
                {
                    var index = List.GetSearch.Search.FindIndex(x => x.Title == FullTitle);
                    List.GetSearch.Search[index].Downloading = true;
                }
                else
                {
                    var index = List.GetMainPageSeries.Series.FindIndex(x => x.Title == FullTitle);
                    List.GetMainPageSeries.Series[index].Downloading = true;
                }
            }
            catch { }

            if (PreviousPercentage != percentage && received != bytes_total)
            {
                try
                {
                    Utils.Database.UpdateProgress(Show, ShowSeason, Ep, (int)(received / (bytes_total / 100)), received, IsSubtitled);
                }
                catch { }

                try
                {
                    var index = List.GetDownloads.Series.FindIndex(x => x.Show == Show && x.IsSubtitled == IsSubtitled);
                    var epIndex = List.GetDownloads.Series[index].Episodes.FindIndex(x => x.EP == Ep && x.ShowSeason == ShowSeason);

                    List.GetDownloads.Series[index].Episodes[epIndex].Progress = percentage;
                    List.GetDownloads.Series[index].Episodes[epIndex].Bytes = received;
                }
                catch { }

                try
                {
                    SmallRemoteView[downloadPos].SetTextViewText(Resource.Id.downloadn_percs, string.Format("{0}%", (int)(received / (bytes_total / 100))));
                    SmallRemoteView[downloadPos].SetProgressBar(Resource.Id.downloadn_progresss, 100, (int)(received / (bytes_total / 100)), false);

                    BigRemoteView[downloadPos].SetTextViewText(Resource.Id.downloadn_perc, string.Format("{0}% ({1}/{2})", (int)(received / (bytes_total / 100)), Utils.Utils.Size(received), Utils.Utils.Size(bytes_total)));
                    BigRemoteView[downloadPos].SetProgressBar(Resource.Id.downloadn_progress, 100, (int)(received / (bytes_total / 100)), false);

                    notificationManager.Notify(NOTIFICATION_ID, Builders[downloadPos].Build());
                }
                catch { }
            }

            return (int)(received / (bytes_total / 100));
        }

        private void CancelDownload(int NOTIFICATION_ID, bool IsSubtitled, string Show, int Ep, int ShowSeason, string _Path, bool IsFromSearch, int listPos)
        {
            notificationManager.Cancel(NOTIFICATION_ID);
            Utils.Database.DeleteItem(IsSubtitled, Show, Ep, ShowSeason);
            File.Delete(_Path);

            try
            {
                if (IsFromSearch)
                    List.GetSearch.Search[listPos].Downloading = false;
                else
                    List.GetMainPageSeries.Series[listPos].Downloading = false;
            }
            catch { }

            try
            {
                var index = List.GetDownloads.Series.FindIndex(x => x.Show == Show && x.IsSubtitled == IsSubtitled);
                var epIndex = List.GetDownloads.Series[index].Episodes.FindIndex(x => x.EP == Ep && x.ShowSeason == ShowSeason);
                List.GetDownloads.Series[index].Episodes[epIndex].downloadInfo.IsDownloading = false;
            }
            catch { }
        }

        private async void PlayResponseAsync(IAsyncResult asyncResult, int NOTIFICATION_ID, long startRange, string _Path, long bytes_total, string Show, int ShowSeason, int Ep, bool IsSubtitled, int downloadPos, bool IsFromSearch, int listPos, string URL, string FullTitle)
        {
            long received = startRange;
            int PreviousPercentage = 0;

            HttpWebRequest webRequest = (HttpWebRequest)asyncResult.AsyncState;

            try
            {
                
                using (HttpWebResponse webResponse = (HttpWebResponse)webRequest.EndGetResponse(asyncResult))
                {
                    byte[] buffer = new byte[1024];

                    FileStream fileStream = new FileStream(_Path, FileMode.Append);

                    using (Stream input = webResponse.GetResponseStream())
                    {
                        int size = input.Read(buffer, 0, buffer.Length);
                        while (size > 0)
                        {
                            if (Stop[downloadPos] || Pause[downloadPos])
                                break;
                            fileStream.Write(buffer, 0, size);
                            received += size;
                            
                            await Task.Run(() =>
                            { 
                                PreviousPercentage = DownloadProgressChanged(received, NOTIFICATION_ID, PreviousPercentage, bytes_total, Show, ShowSeason, Ep, IsSubtitled, downloadPos, IsFromSearch, URL, FullTitle, listPos);
                            });

                            size = input.Read(buffer, 0, buffer.Length);
                        }
                        input.Close();
                    }

                    fileStream.Flush();
                    fileStream.Dispose();
                    fileStream.Close();
                    webResponse.Close();

                    if (!Stop[downloadPos])

                    {
                        if (!Pause[downloadPos])
                        {
                            DownloadFileCompleted(NOTIFICATION_ID, Show, ShowSeason, Ep, bytes_total, IsSubtitled);
                            try
                            {
                                if (IsFromSearch)
                                    List.GetSearch.Search[listPos].Downloading = false;
                                else
                                    List.GetMainPageSeries.Series[listPos].Downloading = false;
                            }
                            catch { }
                        }
                    }
                    else
                    {
                        CancelDownload(NOTIFICATION_ID, IsSubtitled, Show, Ep, ShowSeason, _Path, IsFromSearch, listPos);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("ERROR: "+e.StackTrace);

                notificationManager.Cancel(NOTIFICATION_ID);

                Builders[downloadPos] = new NotificationCompat.Builder(this, CHANNEL_ID)
                   .SetSmallIcon(Resource.Drawable.baseline_error_outline_24)
                   .SetContentText("Falha no download.")
                   .SetStyle(new NotificationCompat.DecoratedCustomViewStyle())
                   .SetColor(Android.Graphics.Color.ParseColor("#FFD80C0C"))
                   .SetPriority((int)NotificationPriority.Low)
                   .SetOngoing(false)
                   .SetOnlyAlertOnce(true);

                notificationManager.Notify(NOTIFICATION_ID, Builders[downloadPos].Build());
                try
                {
                    if (IsFromSearch)
                        List.GetSearch.Search[listPos].Downloading = false;
                    else
                        List.GetMainPageSeries.Series[listPos].Downloading = false;
                }
                catch { }

                try
                {
                    var index = List.GetDownloads.Series.FindIndex(x => x.Show == Show && x.IsSubtitled == IsSubtitled);
                    var epIndex = List.GetDownloads.Series[index].Episodes.FindIndex(x => x.EP == Ep && x.ShowSeason == ShowSeason);
                    List.GetDownloads.Series[index].Episodes[epIndex].downloadInfo.IsDownloading = false;
                }
                catch { }
            }
        }

    }

    [BroadcastReceiver(Enabled = true)]
    [IntentFilter(new string[] { "com.toddy.tflix.PAUSERESUMEDOWNLOAD", "com.toddy.tflix.CANCELDOWNLOAD" })]
    public class DownloadFileBroadcastListener : BroadcastReceiver
    {

        NotificationManager notificationManager;

        public override void OnReceive(Context context, Intent intent)
        {
            int ServiceNotificationID = 0;
            int downloadPos = 0;
            int listPos = 0;
            int Ep = 0;
            int ShowSeason = 0;
            bool IsFromSearch = false;
            bool IsSubtitled = false;
            string Show = "";

            if (notificationManager == null)
                notificationManager = (NotificationManager)context.GetSystemService(Context.NotificationService);

            Console.WriteLine("Received click at: " + intent.Action);

            try
            {
                ServiceNotificationID = intent.Extras.GetInt("NotificationID");
                downloadPos = intent.Extras.GetInt("DownloadPos");
                listPos = intent.Extras.GetInt("ListPos");
                Ep = intent.Extras.GetInt("Ep");
                ShowSeason = intent.Extras.GetInt("ShowSeason");
                IsFromSearch = intent.Extras.GetBoolean("IsFromSearch", false);
                IsSubtitled = intent.Extras.GetBoolean("IsSubtitled", false);
                Show = intent.Extras.GetString("Show");
            }
            catch { }

            switch (intent.Action)
            {
                case "com.toddy.tflix.PAUSERESUMEDOWNLOAD":
                    bool pause = intent.Extras.GetBoolean("PauseResume");

                    string URL = intent.Extras.GetString("URL");
                    string FullTitle = intent.Extras.GetString("FullTitle");
                    string Thumb = intent.Extras.GetString("Thumb");
                    string ShowThumb = intent.Extras.GetString("ShowThumb");

                    var index = List.GetDownloads.Series.FindIndex(x => x.Show == Show && x.IsSubtitled == IsSubtitled);
                    var epIndex = List.GetDownloads.Series[index].Episodes.FindIndex(x => x.EP == Ep && x.ShowSeason == ShowSeason);

                    if(intent.Extras.GetBoolean("IsFromNotification", true))
                        Event.Progress.OnProgressPaused(this, ShowSeason, Ep, List.GetDownloads.Series[index].Episodes[epIndex].ShowID);

                    Intent intentPauseResume = new Intent(context, typeof(DownloadFilesService));
                    intentPauseResume.PutExtra("IsRequestingPauseResume", true);
                    intentPauseResume.PutExtra("NotificationID", ServiceNotificationID);
                    intentPauseResume.PutExtra("PauseResume", pause);
                    intentPauseResume.PutExtra("DownloadPos", downloadPos);
                    intentPauseResume.PutExtra("ListPos", listPos);
                    intentPauseResume.PutExtra("IsFromSearch", IsFromSearch);

                    intentPauseResume.PutExtra("URL", URL);
                    intentPauseResume.PutExtra("FullTitle", FullTitle);
                    intentPauseResume.PutExtra("Show", Show);
                    intentPauseResume.PutExtra("Ep", Ep);
                    intentPauseResume.PutExtra("ShowSeason", ShowSeason);
                    intentPauseResume.PutExtra("IsSubtitled", IsSubtitled);
                    intentPauseResume.PutExtra("Thumb", Thumb);
                    intentPauseResume.PutExtra("ShowThumb", ShowThumb);

                    context.StartService(intentPauseResume);
                    break;
                case "com.toddy.tflix.CANCELDOWNLOAD":

                    Intent intentCancel = new Intent(context, typeof(DownloadFilesService));
                    intentCancel.PutExtra("IsRequestingStop", true);
                    intentCancel.PutExtra("NotificationID", ServiceNotificationID);
                    intentCancel.PutExtra("DownloadPos", downloadPos);
                    intentCancel.PutExtra("ListPos", listPos);
                    intentCancel.PutExtra("IsFromSearch", IsFromSearch);
                    intentCancel.PutExtra("IsSubtitled", IsSubtitled);
                    intentCancel.PutExtra("Ep", Ep);
                    intentCancel.PutExtra("ShowSeason", ShowSeason);
                    intentCancel.PutExtra("Show", Show);

                    context.StartService(intentCancel);
                    break;
            }
        }
    }

    public class DownloadFilesBinder : Binder
    {
        public DownloadFilesBinder(DownloadFilesService service)
        {
            this.Service = service;
        }

        public DownloadFilesService Service { get; private set; }

    }
}