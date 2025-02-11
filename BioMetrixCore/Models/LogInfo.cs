using System;

namespace Native_BioReader.Models
{
    public class LogInfo
    {
        public string device_hash { get; set; }
        public string user_hash { get; set; }
        public int VerifyMode { get; set; }
        public int indRegId { get; set; }
        public DateTime dateTime { get; set; }
        public int WorkCode { get; set; }
    }

}
