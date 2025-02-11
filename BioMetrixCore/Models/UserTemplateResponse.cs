using System.Collections.Generic;

namespace Native_BioReader.Models
{
    public class UserTemplatesResponse
    {
        public string user_hash { get; set; }
        public string device_hash { get; set; }
        public List<UserTemplate> data { get; set; }
    }

}
