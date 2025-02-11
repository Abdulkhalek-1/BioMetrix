using System.Threading.Tasks;
using Native_BioReader.Models;
using Native_BioReader.Utilities;

namespace Native_BioReader.Services.TaskProcessor.Processors
{
    public class CreateUserProcessor : ITaskProcessor
    {
        private readonly DeviceService _deviceService;
        private readonly ApiService _apiService;

        public CreateUserProcessor(DeviceService deviceService, ApiService apiService)
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
            task.TaskData.TryGetValue("name", out var name);
            task.TaskData.TryGetValue("device_hash", out var deviceHash);

            // Parsing data
            int.TryParse(portString, out var port);

            // Validate data
            if (!(
                string.IsNullOrEmpty(ip) ||
                string.IsNullOrEmpty(portString) ||
                string.IsNullOrEmpty(enrollNumber) ||
                string.IsNullOrEmpty(name) ||
                string.IsNullOrEmpty(deviceHash)
                ))
            {
                LoggingHelper.Warn($"Invalid task data Task|{task.id}.");
                return "create_user:failed:invalid task data";
            }

            // Connect to the device
            if (!_deviceService.Connect(ip, port))
                return "create_user:failed:failed to connect to the device";

            // Doing the job
            const string password = "";
            const int privilege = 0;
            const bool enabled = true;

            bool deviceUserCreated = _deviceService.CreateUser(1, enrollNumber, name, password, privilege, enabled);

            if (!deviceUserCreated)
                return "create_user:failed:failed to create user on the device";

            bool serverResponse = await _apiService.CreateUser(enrollNumber, deviceHash, name, task.id);
            return serverResponse
                ? "create_user:completed:user created successfully"
                : "create_user:failed:can't send user to server";
        }
    }
}
