using System;

namespace Core.Models
{
    public class SitecorePageDbModel
    {
        public Guid Id { get; set; }

        public Guid ParentId { get; set; }

        public string TemplateName { get; set; }

        public string Page { get; set; }

        public string Items { get; set; }
    }
}
