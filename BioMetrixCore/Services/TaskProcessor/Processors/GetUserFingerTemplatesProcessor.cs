using Native_BioReader.Models;
using Native_BioReader.Utilities;
using System.Threading.Tasks;

namespace Native_BioReader.Services.TaskProcessor.Processors
{
    public class GetUserFingerTemplatesProcessor: ITaskProcessor
    {
        private readonly DeviceService _deviceService;
        private readonly ApiService _apiService;

        public GetUserFingerTemplatesProcessor(DeviceService deviceService, ApiService apiService)
        {
            _deviceService = deviceService;
            _apiService = apiService;
        }

        public async Task<string> ProcessTask(TaskItem task)
        {
            // Getting data from task
            task.TaskData.TryGetValue("ip", out var ip);
            task.TaskData.TryGetValue("port", out var portString);
            task.TaskData.TryGetValue("user_hash", out var enrollNumber);

            // Parsing data
            int.TryParse(portString, out var port);

            // Validate data
            if (!(
                string.IsNullOrEmpty(ip) ||
                string.IsNullOrEmpty(portString) ||
                string.IsNullOrEmpty(enrollNumber)
                ))
            {
                LoggingHelper.Warn($"Invalid task data Task|{task.id}.");
                return "get_user_finger_templates:failed:invalid task data";
            }

            // Connect to the device
            if (!_deviceService.Connect(ip, port))
                return "get_user_finger_templates:failed:failed to connect to the device";

            // Doing the job
            UserTemplatesResponse deviceUserFingers = _deviceService.GetUserFingers(1, enrollNumber);

            bool facesSent = await _apiService.SendFingers(deviceUserFingers);

            return facesSent
                ? "get_user_finger_templates:completed:fingers sent to API successfully"
                : "get_user_finger_templates:failed:failed to send faces to API";
        }
    }
}
