using System.Collections.Generic;
using Newtonsoft.Json;

namespace Native_BioReader.Models
{
    public class ApiResponse
    {
        [JsonProperty("data")]
        public List<TaskItem> Data
        {
            get;
            set;
        }
    }
}
