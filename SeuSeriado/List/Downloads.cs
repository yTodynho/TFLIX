﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace SeuSeriado.List
{
    public class Downloads
    {
        public int ShowID { get; set; }

        public string EpThumb { get; set; }
        public int EP { get; set; }
        public int ShowSeason { get; set; }
        private int _Progress { get; set; }
        public long Bytes { get; set; }
        public long TotalBytesEP { get; set; }
        public long Duration { get; set; }
        public bool IsSelected { get; set; }
        public bool IsDownloading { get; set; }
        public long TimeWatched { get; set; }

        public int Progress
        {
            get
            {
                return _Progress;
            }
            set
            {
                _Progress = value;
                try
                {
                    if(IsDownloading)
                        Activities.DetailedDownloads.ProgressChanged(this.ShowSeason, this.EP);
                }
                catch { }
            }
        }
        
    }

    public class AllDownloads
    {
        public string ShowThumb { get; set; }
        public long TotalBytes { get; set; }
        public string Show { get; set; }
        public bool IsSubtitled { get; set; }
        public List<Downloads> Episodes { get; set; }
    }

    public class GetDownloads
    {
        public static List<AllDownloads> Series;
    }
}