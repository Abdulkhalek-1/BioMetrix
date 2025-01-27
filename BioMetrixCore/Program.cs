using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using zkemkeeper;
using System.IO;
using TaskSchedulerTask = Microsoft.Win32.TaskScheduler.Task;
using TaskService = Microsoft.Win32.TaskScheduler.TaskService;
using Trigger = Microsoft.Win32.TaskScheduler.Trigger;
using TimeTrigger = Microsoft.Win32.TaskScheduler.TimeTrigger;

namespace Native_BioReader
{
    class App
    {
        private CZKEM objCZKEM = new CZKEM();
        private bool isDeviceConnected = false;

        public bool Connect(string ipAddress, int portNumber)
        {
            isDeviceConnected = objCZKEM.Connect_Net(ipAddress, portNumber);

            if (isDeviceConnected)
            {
                Console.WriteLine($"Device at {ipAddress}:{portNumber} connected successfully.");
            }
            else
            {
                Console.WriteLine($"Failed to connect to the device at {ipAddress}:{portNumber}. Check the IP address and port.");
            }

            return isDeviceConnected;
        }

        public bool CreateUser(int machineNumber, string enrollNumber, string name, string password, int privilege, bool enabled)
        {
            if (!isDeviceConnected)
            {
                Console.WriteLine("Device not connected. Please connect first.");
                return false;
            }

            bool result = objCZKEM.SSR_SetUserInfo(machineNumber, enrollNumber, name, password, privilege, enabled);

            if (result)
            {
                objCZKEM.RefreshData(machineNumber);
                Console.WriteLine($"User {name} with Enroll Number {enrollNumber} created successfully.");
            }
            else
            {
                int errorCode = 0;
                objCZKEM.GetLastError(ref errorCode);
                Console.WriteLine($"Failed to create user. Error Code: {errorCode}");
            }

            return result;
        }

        public bool DeleteUser(int machineNumber, string enrollNumber)
        {
            if (!isDeviceConnected)
            {
                Console.WriteLine("Device not connected. Please connect first.");
                return false;
            }

            bool result = objCZKEM.SSR_DeleteEnrollData(machineNumber, enrollNumber, 12); // 12 means delete all data (fingerprints, password, etc.)

            if (result)
            {
                objCZKEM.RefreshData(machineNumber);
                Console.WriteLine($"User with Enroll Number {enrollNumber} deleted successfully.");
            }
            else
            {
                int errorCode = 0;
                objCZKEM.GetLastError(ref errorCode);
                Console.WriteLine($"Failed to delete user. Error Code: {errorCode}");
            }

            return result;
        }

        public ICollection<UserInfo> GetAllUserInfo(int machineNumber)
        {
            string enrollNumber, name, password, templateData;
            int privilege, templateLength, flag;
            bool enabled;

            ICollection<UserInfo> users = new List<UserInfo>();

            objCZKEM.ReadAllUserID(machineNumber);
            objCZKEM.ReadAllTemplate(machineNumber);

            while (objCZKEM.SSR_GetAllUserInfo(machineNumber, out enrollNumber, out name, out password, out privilege, out enabled))
            {
                for (int fingerIndex = 0; fingerIndex < 10; fingerIndex++)
                {
                    if (objCZKEM.GetUserTmpExStr(machineNumber, enrollNumber, fingerIndex, out flag, out templateData, out templateLength))
                    {
                        users.Add(new UserInfo
                        {
                            MachineNumber = machineNumber,
                            EnrollNumber = enrollNumber,
                            Name = name,
                            Password = password,
                            Privelage = privilege,
                            Enabled = enabled,
                            FingerIndex = fingerIndex,
                            TmpData = templateData,
                            iFlag = flag.ToString()
                        });
                    }
                }
            }

            return users;
        }

        public ICollection<LogInfo> GetLogs(int machineNumber)
        {
            if (!isDeviceConnected)
            {
                Console.WriteLine("Device not connected. Please connect first.");
                return null;
            }

            ICollection<LogInfo> logs = new List<LogInfo>();

            if (!objCZKEM.ReadGeneralLogData(machineNumber))
            {
                int errorCode = 0;
                objCZKEM.GetLastError(ref errorCode);
                Console.WriteLine($"Failed to read logs. Error Code: {errorCode}");
                return logs;
            }

            string enrollNumber;
            int verifyMode, inOutMode, year, month, day, hour, minute, second;

            // Initialize workCode before passing it by reference
            int workCode = 0;

            // Use 'ref' for workCode
            while (objCZKEM.SSR_GetGeneralLogData(machineNumber, out enrollNumber, out verifyMode, out inOutMode,
                                                   out year, out month, out day, out hour, out minute, out second,
                                                   ref workCode))
            {
                logs.Add(new LogInfo
                {
                    user_hash = enrollNumber,
                    VerifyMode = verifyMode,
                    indRegId = inOutMode,
                    dateTime = new DateTime(year, month, day, hour, minute, second),
                    WorkCode = workCode
                });
            }

            Console.WriteLine($"Retrieved {logs.Count} logs successfully.");
            return logs;
        }


        public bool IsDeviceConnected => isDeviceConnected;
    }

    class Config
    {
        public int INTERVAL = 60;
        public string BASE_URL
        {
            get;
            set;
        }
    }

    class TaskItem
    {
        public string id
        {
            get;
            set;
        }
        public string task_type
        {
            get;
            set;
        }
        public string task_data
        {
            get;
            set;
        }
        public string status
        {
            get;
            set;
        }
        public string created_at
        {
            get;
            set;
        }
        public string updated_at
        {
            get;
            set;
        }

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

    internal class Program
    {
        private static HttpClient httpClient = new HttpClient();

        static async Task Main(string[] args)
        {
            try
            {
                // Load configuration from JSON file
                Config config = LoadConfig("config.json");
                if (config == null)
                {
                    Console.WriteLine("Failed to load configuration.");
                    return;
                }

                httpClient.BaseAddress = new Uri(config.BASE_URL);

                Console.WriteLine("Fetching tasks...");
                List<TaskItem> tasks = await FetchTasks();

                foreach (var task in tasks)
                {
                    bool success = await ProcessTask(task);

                    if (success)
                    {
                        await UpdateTaskStatus(task.id, "completed");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }

        private static async Task<List<TaskItem>> FetchTasks()
        {
            try
            {
                HttpResponseMessage response = await httpClient.GetAsync($"{httpClient.BaseAddress}/zk_que/pending");
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();
                var responseObject = JsonConvert.DeserializeObject<ApiResponse>(json);

                // Return the `data` property containing the list of tasks
                return responseObject.Data ?? new List<TaskItem>();

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching tasks: {ex.Message}");
                return new List<TaskItem>();
            }
        }

        private static async Task<bool> ProcessTask(TaskItem task)
        {
            App app = new App();

            if (task.task_type == "create_user" && task.status == "pending")
            {
                // Extract IP and port safely using TryGetValue
                if (task.TaskData.TryGetValue("ip", out
                        var ip) &&
                    task.TaskData.TryGetValue("port", out
                        var portString) &&
                    int.TryParse(portString, out
                        var port))
                {
                    if (app.Connect(ip, port))
                    {
                        // Extract additional fields safely using TryGetValue
                        task.TaskData.TryGetValue("user_hash", out
                            var enrollNumberObj);
                        task.TaskData.TryGetValue("name", out
                            var nameObj);
                        task.TaskData.TryGetValue("device_hash", out
                            var deviceHashObj);

                        string enrollNumber = enrollNumberObj ?? string.Empty;
                        string name = nameObj ?? string.Empty;
                        string deviceHash = deviceHashObj ?? string.Empty;
                        string password = string.Empty;
                        int privilege = 0;
                        bool enabled = true;

                        // Create the user on the device
                        bool deviceUserCreated = app.CreateUser(1, enrollNumber, name, password, privilege, enabled);

                        if (deviceUserCreated)
                        {
                            // If the user is successfully created on the device, send the data to the server
                            bool serverResponse = await CreateUserAsync(enrollNumber, deviceHash, name);

                            if (serverResponse)
                            {
                                Console.WriteLine("User created successfully on the server.");
                                return true;
                            }
                            else
                            {
                                Console.WriteLine("Failed to create user on the server.");
                                return false;
                            }
                        }
                        else
                        {
                            Console.WriteLine("Failed to create user on the device.");
                            return false;
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Invalid IP or Port in task data.");
                }
            }
            else if (task.task_type == "get_log" && task.status == "pending") 
            {
                // Extract IP and port safely using TryGetValue
                if (task.TaskData.TryGetValue("ip", out var ip) &&
                    task.TaskData.TryGetValue("port", out var portString) &&
                    int.TryParse(portString, out var port))
                {
                    if (app.Connect(ip, port))
                    {
                        // Fetch logs from the device
                        var logs = app.GetLogs(1); // Assuming machineNumber is 1

                        if (logs != null && logs.Count > 0)
                        {
                            // Convert logs to JSON
                            string jsonLogs = JsonConvert.SerializeObject(logs);

                            // Send logs to the API
                            bool logsSent = await SendLogsToAPI(jsonLogs);

                            if (logsSent)
                            {
                                Console.WriteLine("Logs sent to API successfully.");
                                return true;
                            }
                            else
                            {
                                Console.WriteLine("Failed to send logs to API.");
                                return false;
                            }
                        }
                        else
                        {
                            Console.WriteLine("No logs found on the device.");
                            return false;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Failed to connect to the device.");
                        return false;
                    }
                }
                else
                {
                    Console.WriteLine("Invalid IP or Port in task data.");
                    return false;
                }
            }
            else if (task.task_type == "delete_user" && task.status == "pending")
            {
                // Extract IP and port safely using TryGetValue
                if (task.TaskData.TryGetValue("ip", out var ip) &&
                    task.TaskData.TryGetValue("port", out var portString) &&
                    int.TryParse(portString, out var port))
                {
                    if (app.Connect(ip, port))
                    {
                        // Extract user_hash safely using TryGetValue
                        task.TaskData.TryGetValue("user_hash", out var enrollNumberObj);
                        task.TaskData.TryGetValue("userId", out var userIdObj);

                        string enrollNumber = enrollNumberObj ?? string.Empty;
                        string userId = userIdObj ?? string.Empty;

                        // Delete the user from the device
                        bool userDeleted = app.DeleteUser(1, enrollNumber);

                        if (userDeleted)
                        {
                            Console.WriteLine($"User with Enroll Number {enrollNumber} deleted successfully.");

                            // Optionally notify the server about the deletion
                            bool serverResponse = await DeleteUserAsync(userId);

                            if (serverResponse)
                            {
                                Console.WriteLine("User deletion confirmed by the server.");
                                return true;
                            }
                            else
                            {
                                Console.WriteLine("Failed to notify the server about user deletion.");
                                return false;
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Failed to delete user with Enroll Number {enrollNumber}.");
                            return false;
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Invalid IP or Port in task data.");
                    return false;
                }
            }
            else if (task.task_type == "update_interval" && task.status == "pending")
            {
                // Check for task name and new interval in TaskData
                if (task.TaskData.TryGetValue("interval", out var intervalMinutesString) &&
                    int.TryParse(intervalMinutesString, out var intervalMinutes))
                {
                    TimeSpan repeatInterval = TimeSpan.FromMinutes(intervalMinutes);

                    try
                    {
                        // Update the interval using the helper method
                        UpdateTaskRepeatInterval(repeatInterval);

                        Console.WriteLine($"Successfully updated the interval for task 'FetchTasks' to {repeatInterval.TotalMinutes} minutes.");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to update interval for task 'FetchTasks': {ex.Message}");
                        return false;
                    }
                }
                else
                {
                    Console.WriteLine("Invalid task_name or interval_minutes in TaskData.");
                    return false;
                }
            }



            return false;
        }
        private static async Task<bool> SendLogsToAPI(string jsonLogs)
        {
            try
            {
                // Prepare the payload as form data
                var payload = new Dictionary<string, string>
                {
                    { "data", jsonLogs } // Ensure the key matches what the API expects
                };

                // Create FormUrlEncodedContent
                var content = new FormUrlEncodedContent(payload);

                // Make the POST request
                HttpResponseMessage response = await httpClient.PostAsync($"{httpClient.BaseAddress}/zk_fingerprintsLogs/create", content);

                // Ensure the request was successful
                response.EnsureSuccessStatusCode();

                // Read and log the API response
                string responseBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine("API Response: " + responseBody);

                return true;
            }
            catch (HttpRequestException e)
            {
                // Log any HTTP request errors
                Console.WriteLine($"Error sending logs to API: {e.Message}");
                return false;
            }
            catch (Exception ex)
            {
                // Log any other unexpected errors
                Console.WriteLine($"Unexpected error sending logs to API: {ex.Message}");
                return false;
            }
        }

        private static async Task UpdateTaskStatus(string taskId, string status)
        {
            try
            {
                // Prepare the payload as form data
                var formData = new Dictionary<string, string>
                {
                    { "status", status }
                };

                // Create FormUrlEncodedContent
                var content = new FormUrlEncodedContent(formData);

                // Make the POST request
                HttpResponseMessage response = await httpClient.PostAsync($"{httpClient.BaseAddress}/zk_que/update/{taskId}", content);
                response.EnsureSuccessStatusCode();

                // Log success
                Console.WriteLine($"Task {taskId} updated to {status}.");
            }
            catch (Exception ex)
            {
                // Log any errors
                Console.WriteLine($"Error updating task {taskId}: {ex.Message}");
            }
        }

        private static Config LoadConfig(string filePath)
        {
            try
            {
                string json = File.ReadAllText(filePath);
                return JsonConvert.DeserializeObject<Config>(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading configuration file: {ex.Message}");
                return null;
            }
        }
        public static async Task<bool> CreateUserAsync(string userHash, string deviceHash, string name)
        {
            var payload = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "name", name },
                { "user_hash", userHash },
                { "device_hash", deviceHash },
            });

            try
            {
                HttpResponseMessage response = await httpClient.PostAsync($"{httpClient.BaseAddress}/zk_users/create", payload);
                response.EnsureSuccessStatusCode(); // Throws an exception if the status code is not successful
                string responseBody = await response.Content.ReadAsStringAsync();
                return true;
            }
            catch (HttpRequestException e)
            {
                return false;
            }
        }

        public static async Task<bool> DeleteUserAsync(string userId)
        {

            try
            {
                HttpResponseMessage response = await httpClient.DeleteAsync($"{httpClient.BaseAddress}/zk_users/delete/{userId}");
                response.EnsureSuccessStatusCode(); // Throws an exception if the status code is not successful
                string responseBody = await response.Content.ReadAsStringAsync();
                return true;
            }
            catch (HttpRequestException e)
            {
                return false;
            }
        }

        static void UpdateTaskRepeatInterval(TimeSpan repeatInterval)
        {
            using (TaskService taskService = new TaskService())
            {
                // Get the task by name
                TaskSchedulerTask task = taskService.GetTask("FetchTasks");

                if (task != null)
                {
                    // Ensure the task has triggers
                    if (task.Definition.Triggers.Count > 0)
                    {
                        foreach (Trigger trigger in task.Definition.Triggers)
                        {
                            if (trigger is TimeTrigger timeTrigger)
                            {
                                // Update the repetition settings
                                timeTrigger.Repetition.Interval = repeatInterval;

                                Console.WriteLine($"Task 'FetchTasks' repeat interval updated to {repeatInterval}.");
                            }
                        }

                        // Save the updated task definition
                        taskService.RootFolder.RegisterTaskDefinition("FetchTasks", task.Definition);
                    }
                    else
                    {
                        Console.WriteLine($"Task 'FetchTasks' does not have any triggers.");
                    }
                }
                else
                {
                    Console.WriteLine($"Task 'FetchTasks' not found.");
                }
            }
        }

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
}