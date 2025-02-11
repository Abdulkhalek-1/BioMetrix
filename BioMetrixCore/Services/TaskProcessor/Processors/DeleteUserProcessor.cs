using Native_BioReader.Models;
using System.Threading.Tasks;
using Native_BioReader.Utilities;

namespace Native_BioReader.Services.TaskProcessor.Processors
{
    public class DeleteUserProcessor : ITaskProcessor
    {
        private readonly DeviceService _deviceService;
        private readonly ApiService _apiService;




        public DeleteUserProcessor(DeviceService deviceService, ApiService apiService)
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
            task.TaskData.TryGetValue("userId", out var userId);

            // Parsing data
            int.TryParse(portString, out var port);

            // Validate data
            if (!(
                string.IsNullOrEmpty(ip) ||
                string.IsNullOrEmpty(portString) ||
                string.IsNullOrEmpty(enrollNumber) ||
                string.IsNullOrEmpty(userId)
                ))
            {
                LoggingHelper.Warn($"Invalid task data Task|{task.id}.");
                return "delete_user:failed:invalid task data";
            }

            // Connect to the device
            if (!_deviceService.Connect(ip, port))
                return "delete_user:failed:failed to connect to the device";

            // Doing the job
            bool deviceUserDeleted = _deviceService.DeleteUser(1, enrollNumber);

            if (!deviceUserDeleted)
                return "delete_user:failed:failed to delete user on the device";

            bool serverResponse = await _apiService.DeleteUser(enrollNumber);
            return serverResponse
                ? "delete_user:completed:user deleted successfully"
                : "delete_user:failed:can't delete user on server";
        }
    }
}
