using Native_BioReader.Models;
using System.Threading.Tasks;
using Native_BioReader.Utilities;

namespace Native_BioReader.Services.TaskProcessor.Processors
{
    public class SetUserFaceTemplatesProcessor: ITaskProcessor
    {
        private readonly DeviceService _deviceService;

        public SetUserFaceTemplatesProcessor(DeviceService deviceService)
        {
            _deviceService = deviceService;
        }

        public async Task<string> ProcessTask(TaskItem task)
        {
            // Getting data from task
            task.TaskData.TryGetValue("ip", out var ip);
            task.TaskData.TryGetValue("port", out var portString);
            task.TaskData.TryGetValue("user_hash", out var enrollNumber);
            task.TaskData.TryGetValue("face_index", out var userFaceIndexString);
            task.TaskData.TryGetValue("face_data", out var userFaceData);

            // Parsing data
            int.TryParse(portString, out var port);
            int.TryParse(userFaceIndexString, out var userFaceIndex);

            // Validate data
            if (!(
                string.IsNullOrEmpty(ip) ||
                string.IsNullOrEmpty(portString) ||
                string.IsNullOrEmpty(enrollNumber) || 
                string.IsNullOrEmpty(userFaceIndexString) || 
                string.IsNullOrEmpty(userFaceData)
                ))
            {
                LoggingHelper.Warn($"Invalid task data Task|{task.id}.");
                return "set_user_face_templates:failed:invalid task data";
            }

            // Connect to the device
            if (!_deviceService.Connect(ip, port))
                return "set_user_face_templates:failed:failed to connect to the device";

            // Doing the job
            const int faceLength = 1;
            bool deviceUserFaceSet = _deviceService.InsertUserFace(1, enrollNumber, userFaceIndex, userFaceData, faceLength);

            return deviceUserFaceSet
                ? "set_user_face_templates:completed:user face inseted to device"
                : "set_user_face_templates:failed:user face insertion failed on device";
        }
    }
}
