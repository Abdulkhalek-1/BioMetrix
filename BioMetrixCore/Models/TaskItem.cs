using System.Collections.Generic;
using Newtonsoft.Json;

namespace Native_BioReader.Models
{
    public class TaskItem
    {
        public string id { get; set; }
        public string task_type { get; set; }
        public string task_data { get; set; }
        public string status { get; set; }
        public string created_at { get; set; }
        public string updated_at { get; set; }
        public Dictionary<string, string> TaskData
        {
            get
            {
                if (!string.IsNullOrEmpty(task_data))
                {
                    try
                    {
                        return JsonConvert.DeserializeObject<Dictionary<string, string>>(task_data);
                    }
                    catch (JsonException)
                    {
                        // Handle parsing errors (e.g., log or return an empty dictionary)
                        return new Dictionary<string, string>();
                    }
                }
                return new Dictionary<string, string>();
            }
        }
    }
}
