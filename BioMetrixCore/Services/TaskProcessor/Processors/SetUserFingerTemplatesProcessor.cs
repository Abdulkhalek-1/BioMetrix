using Native_BioReader.Models;
using System.Threading.Tasks;
using Native_BioReader.Utilities;

namespace Native_BioReader.Services.TaskProcessor.Processors
{
    public class SetUserFingerTemplatesProcessor: ITaskProcessor
    {
        private readonly DeviceService _deviceService;

        public SetUserFingerTemplatesProcessor(DeviceService deviceService)
        {
            _deviceService = deviceService;
        }

        public async Task<string> ProcessTask(TaskItem task)
        {
            // Getting data from task
            task.TaskData.TryGetValue("ip", out var ip);
            task.TaskData.TryGetValue("port", out var portString);
            task.TaskData.TryGetValue("user_hash", out var enrollNumber);
            task.TaskData.TryGetValue("finger_index", out var userFingerIndexString);
            task.TaskData.TryGetValue("finger_data", out var userFingerData);

            // Parsing data
            int.TryParse(portString, out var port);
            int.TryParse(userFingerIndexString, out var userFingerIndex);

            // Validate data
            if (!(
                string.IsNullOrEmpty(ip) ||
                string.IsNullOrEmpty(portString) ||
                string.IsNullOrEmpty(enrollNumber) ||
                string.IsNullOrEmpty(userFingerIndexString) ||
                string.IsNullOrEmpty(userFingerData)
                ))
            {
                LoggingHelper.Warn($"Invalid task data Task|{task.id}.");
                return "set_user_finger_templates:failed:invalid task data";
            }

            // Connect to the device
            if (!_deviceService.Connect(ip, port))
                return "set_user_finger_templates:failed:failed to connect to the device";

            // Doing the job
            const int fingerFlag = 1;
            bool deviceUserFIngerSet = _deviceService.InsertUserFinger(1, enrollNumber, userFingerIndex, userFingerData, fingerFlag);

            return deviceUserFIngerSet
                ? "set_user_finger_templates:completed:user finger inseted to device"
                : "set_user_finger_templates:failed:user finger insertion failed on device";
        }
    }
}
