﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Runtime;
using Android.Support.V7.Widget;
using Android.Util;
using Android.Views;
using Android.Widget;
using Square.Picasso;

namespace TFlix.Adapter
{
    class SearchAdapter : RecyclerView.Adapter
    {
        Context context;
        RecyclerView rec;

        public SearchAdapter(Context c, RecyclerView recyclerView)
        {
            context = c;
            rec = recyclerView;
        }

        public override int ItemCount => List.GetSearch.Search.Count;

        public override long GetItemId(int position) { return base.GetItemId(position); }

        public class SearchAdapterHolder : RecyclerView.ViewHolder
        {
            public View mMain { get; set; }
            public ImageView Image { get; set; }
            public TextView Title { get; set; }
            public ImageView Download { get; set; }

            public SearchAdapterHolder(View view) : base(view)
            {
                mMain = view;
            }
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            View row = LayoutInflater.From(parent.Context).Inflate(Resource.Layout.search_row, parent, false);
            ImageView image = row.FindViewById<ImageView>(Resource.Id.thumbnail_spg);
            TextView title = row.FindViewById<TextView>(Resource.Id.title_spg);
            ImageView download = row.FindViewById<ImageView>(Resource.Id.download_spg);

            row.Click += Row_Click;
            download.Click += Download_Click;

            image.SetScaleType(ImageView.ScaleType.CenterCrop);
            SearchAdapterHolder view = new SearchAdapterHolder(row) { Image = image, Title = title, Download = download };
            return view;
        }

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            SearchAdapterHolder Holder = holder as SearchAdapterHolder;
            bool isSubtitled;

            Holder.Title.Text = List.GetSearch.Search[position].Title.Replace("ª Temporada Episódio ", "x").Replace("Online,", "").Replace("(SEASON FINALE)", "").Replace("LEGENDADO", "LEG").Replace("DUBLADO", "DUB").Replace("SEM LEGENDA", "S/L");
            Picasso.With(context).Load(List.GetSearch.Search[position].ImgLink).Into(Holder.Image, new Action(async () => { await Task.Run(() => List.GetSearch.Search[position].IMG64 = Base64.EncodeToString(Utils.Utils.GetImageBytes(Holder.Image.Drawable), Base64Flags.UrlSafe)); }), new Action(() => { }));

            var (Show, Season, Ep) = Utils.Utils.BreakFullTitleInParts(List.GetSearch.Search[position].Title.Replace("Online,", "Online "));

            if (List.GetSearch.Search[position].Title.Contains("LEGENDADO") || List.GetSearch.Search[position].Title.Contains("SEM LEGENDA"))
                isSubtitled = true;
            else
                isSubtitled = false;

            if (!List.GetSearch.Search[position].AlreadyChecked)
            {
                List.GetSearch.Search[position].Downloaded = Utils.Database.IsItemDownloaded(Season, Show, isSubtitled, Ep);
                List.GetSearch.Search[position].AlreadyChecked = true;
            }

            if (List.GetSearch.Search[position].Downloaded)
            {
                Holder.Download.SetColorFilter(Android.Graphics.Color.Argb(255, 255, 255, 255));
                Holder.Download.SetImageDrawable(context.GetDrawable(Resource.Drawable.baseline_cloud_done_24));
            }
            else
            {
                Holder.Download.SetColorFilter(null);

                try
                {
                    Utils.Database.ReadDB();
                    var listIndex = List.GetDownloads.Series.FindIndex(x => x.Show == Show && x.IsSubtitled == isSubtitled);
                    var epIndex = List.GetDownloads.Series[listIndex].Episodes.FindIndex(x => x.ShowSeason == Season && x.EP == Ep);

                    List.GetSearch.Search[position].Downloading = List.GetDownloads.Series[listIndex].Episodes[epIndex].downloadInfo.IsDownloading;
                }
                catch
                {
                    List.GetSearch.Search[position].Downloading = false;
                }

                if (List.GetSearch.Search[position].Downloading)
                    Holder.Download.SetImageDrawable(context.GetDrawable(Resource.Drawable.baseline_cloud_download_on));
                else
                    Holder.Download.SetImageDrawable(context.GetDrawable(Resource.Drawable.baseline_cloud_download_24));
            }
        }

        private void Download_Click(object sender, EventArgs e)
        {
            View v = (View)sender;
            try
            {
                int Pos = rec.GetChildAdapterPosition((View)v.Parent);
                if (!List.GetSearch.Search[Pos].Downloading && !List.GetSearch.Search[Pos].Downloaded)
                {
                    var holder = rec.FindViewHolderForAdapterPosition(Pos);
                    var dowload = holder.ItemView.FindViewById<ImageView>(Resource.Id.download_spg);
                    
                    Utils.Utils.DownloadVideo(true, Pos, context);
                    dowload.SetImageDrawable(context.GetDrawable(Resource.Drawable.baseline_cloud_download_on));
                }
            }
            catch { }
        }

        private void Row_Click(object sender, EventArgs e)
        {
            View v = (View)sender;
            Intent intent = new Intent(v.Context, typeof(Activities.Player));
            intent.PutExtra("Pos", rec.GetChildAdapterPosition(v));
            intent.PutExtra("IsOnline", true);
            intent.PutExtra("IsFromSearch", true);
            context.StartActivity(intent);
        }

        
    }
}