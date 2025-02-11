using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Native_BioReader.Models;
using Native_BioReader.Utilities;
using Newtonsoft.Json;

namespace Native_BioReader.Services.TaskProcessor.Processors
{
    public class GetLogProcessor : ITaskProcessor
    {
        private readonly DeviceService _deviceService;
        private readonly ApiService _apiService;

        public GetLogProcessor(DeviceService deviceService, ApiService apiService)
        {
            _deviceService = deviceService;
            _apiService = apiService;
        }

        public async Task<string> ProcessTask(TaskItem task)
        {
            // Getting data from task
            task.TaskData.TryGetValue("ip", out var ip);
            task.TaskData.TryGetValue("port", out var portString);
            task.TaskData.TryGetValue("start_date", out var startDateString);
            task.TaskData.TryGetValue("end_date", out var endDateString);

            // Parsing data
            int.TryParse(portString, out var port);
            DateTime.TryParse(startDateString, out var startDate);
            DateTime.TryParse(endDateString, out var endDate);

            // Validate data
            if (!(
                string.IsNullOrEmpty(ip) || 
                string.IsNullOrEmpty(portString) ||
                string.IsNullOrEmpty(startDateString) ||
                string.IsNullOrEmpty(endDateString)
                ))
            {
                LoggingHelper.Warn($"Invalid data Task|{task.id}.");
                return "get_log:failed:invalid task data";
            }

            // Connect to the device
            if (!_deviceService.Connect(ip, port))
                return "get_log:failed:failed to connect to the device";

            // Doing the job
            ICollection<LogInfo> logs = _deviceService.GetLogs(1, ip, portString, startDate, endDate);
            string jsonLogs = JsonConvert.SerializeObject(logs);
            bool logsSent = await _apiService.SendLogs(jsonLogs);

            return logsSent 
                ? "get_log:completed:logs sent to API successfully"
                : "get_log:failed:failed to send logs to API";
        }
    }
}
