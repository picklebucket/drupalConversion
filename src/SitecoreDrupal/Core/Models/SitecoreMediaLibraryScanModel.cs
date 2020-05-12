using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Core.Models
{
    public class SitecoreMediaLibraryScanModel
    {
        public DateTime Start { get; set; }

        public DateTime End { get; set; }

        public TimeSpan Time { get; set; }

        public int Count { get; set; }

        public string Message { get; set; }

        [JsonIgnore]
        public Dictionary<Guid, MediaLibraryItem> MediaItems { get; set; }
    }
}
