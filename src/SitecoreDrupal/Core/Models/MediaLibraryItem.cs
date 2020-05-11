using System;

namespace Core.Models
{
    public class MediaLibraryItem
    {
        public Guid Id { get; set; }

        public bool IsFolder { get; set; }

        public string Name { get; set; }
        
        public string LocalPath { get; set; }

        public string ParentFolderPath { get; set; }
    }
}
