using System;

namespace Core.Models
{
    public class MediaLibraryScItem
    {
        public Guid ItemID { get; set; }

        public string ItemName { get; set; }

        public bool HasChildren { get; set; }

        public string TemplateName { get; set; }

        public string Extension { get; set; }
    }
}
