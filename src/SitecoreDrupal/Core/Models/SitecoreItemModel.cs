using Newtonsoft.Json;
using System;
using System.Text.Json;

namespace Core.Models
{
    public class SitecoreItemModel
    {
        public Guid ItemID { get; set; }

        public Guid ParentID { get; set; }

        public bool HasChildren { get; set; }

        public string TemplateName { get; set; }

        [JsonProperty("__Renderings")]
        public string Renderings { get; set; }

        [JsonProperty("__Final Renderings")]
        public string FinalRenderings { get; set; }

        [JsonIgnore]
        public bool IsPage
        {
            get
            {
                return !string.IsNullOrEmpty(this.FinalRenderings)
                    || !string.IsNullOrEmpty(this.Renderings);
            }
        }
    }
}
