using System;

namespace Core.Models
{
    public class SyncModelResponse
    {
        public TimeSpan ReadFromSitecoreTime { get; set; }

        public TimeSpan SaveToDBTime { get; set; }

        public int PagesCount { get; set; }

        public string Message { get; set; }
    }
}
