using System;
using System.Collections.Generic;

namespace Core.Models
{
    public class SitecoreTreeScanModel
    {
        public DateTime Start { get; set; }

        public DateTime End { get; set; }

        public TimeSpan Time { get; set; }

        public int Count { get; set; }

        public List<SitecorePageDbModel> Pages { get; set; }
    }
}
