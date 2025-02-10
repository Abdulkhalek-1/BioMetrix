using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using zkemkeeper;
using System.IO;
using NLog;
using Quobject.SocketIoClientDotNet.Client;
using TaskSchedulerTask = Microsoft.Win32.TaskScheduler.Task;
using TaskService = Microsoft.Win32.TaskScheduler.TaskService;
using Trigger = Microsoft.Win32.TaskScheduler.Trigger;
using TimeTrigger = Microsoft.Win32.TaskScheduler.TimeTrigger;
using NLog.Config;
using NLog.Targets;
using System.Linq;
using System.Web.UI;

namespace Native_BioReader
{
    class App
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private CZKEM objCZKEM = new CZKEM();
        private bool isDeviceConnected = false;

        public bool Connect(string ipAddress, int portNumber)
        {
            isDeviceConnected = objCZKEM.Connect_Net(ipAddress, portNumber);

            if (isDeviceConnected)
            {
                Console.WriteLine($"Device at {ipAddress}:{portNumber} connected successfully.");
                Logger.Info($"Device at {ipAddress}:{portNumber} connected successfully.");
            }
            else
            {
                Console.WriteLine($"Failed to connect to the device at {ipAddress}:{portNumber}. Check the IP address and port.");
                Logger.Error($"Failed to connect to the device at {ipAddress}:{portNumber}. Check the IP address and port.");
            }

            return isDeviceConnected;
        }

        public bool CreateUser(int machineNumber, string enrollNumber, string name, string password, int privilege, bool enabled)
        {
            if (!isDeviceConnected)
            {
                Console.WriteLine("Cannont create user: Device not connected. Please connect first.");
                Logger.Warn("Cannont create user: Device not connected. Please connect first.");
                return false;
            }

            bool result = objCZKEM.SSR_SetUserInfo(machineNumber, enrollNumber, name, password, privilege, enabled);

            if (result)
            {
                objCZKEM.RefreshData(machineNumber);
                Console.WriteLine($"User {name} with Enroll Number {enrollNumber} created successfully.");
                Logger.Info($"User {name} with Enroll Number {enrollNumber} created successfully.");
            }
            else
            {
                int errorCode = 0;
                objCZKEM.GetLastError(ref errorCode);
                Console.WriteLine($"Failed to create user. Error Code: {errorCode}");
                Logger.Error($"Failed to create user. Error Code: {errorCode}");
            }

            return result;
        }

        public bool DeleteUser(int machineNumber, string enrollNumber)
        {
            if (!isDeviceConnected)
            {
                Console.WriteLine("Cannont delete user: Device not connected. Please connect first.");
                Logger.Warn("Cannont delete user: Device not connected. Please connect first.");
                return false;
            }

            bool result = objCZKEM.SSR_DeleteEnrollData(machineNumber, enrollNumber, 12); // 12 means delete all data (fingerprints, password, etc.)

            if (result)
            {
                objCZKEM.RefreshData(machineNumber);
                Console.WriteLine($"User with Enroll Number {enrollNumber} deleted successfully.");
                Logger.Info($"User with Enroll Number {enrollNumber} deleted successfully.");
            }
            else
            {
                int errorCode = 0;
                objCZKEM.GetLastError(ref errorCode);
                Console.WriteLine($"Failed to delete user. Error Code: {errorCode}");
                Logger.Error($"Failed to delete user. Error Code: {errorCode}");
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

        public ICollection<LogInfo> GetLogs(int machineNumber, string ip, string port, DateTime startDate, DateTime endDate)
        {
            if (!isDeviceConnected)
            {
                Console.WriteLine("Device not connected. Please connect first.");
                Logger.Info("Cannont get log: Device not connected.Please connect first.");
                return null;
            }

            ICollection<LogInfo> logs = new List<LogInfo>();

           
            if (!objCZKEM.ReadGeneralLogData(machineNumber))
            {
                int errorCode = 0;
                objCZKEM.GetLastError(ref errorCode);
                Console.WriteLine($"Failed to read logs. Error Code: {errorCode}");
                Logger.Error($"Failed to read logs. Error Code: {errorCode}");
                return logs;
            }

            string enrollNumber;
            int verifyMode, inOutMode, year, month, day, hour, minute, second;
            string deviceId = $"{ip}:{port}";

            // Initialize workCode before passing it by reference
            int workCode = 0;

            // Use 'ref' for workCode
            while (objCZKEM.SSR_GetGeneralLogData(machineNumber, out enrollNumber, out verifyMode, out inOutMode,
                                                   out year, out month, out day, out hour, out minute, out second,
                                                   ref workCode))
            {
                DateTime logDateTime = new DateTime(year, month, day, hour, minute, second);

                if (logDateTime >= startDate && logDateTime <= endDate)
                {
                    logs.Add(new LogInfo
                    {
                        device_hash = deviceId,
                        user_hash = enrollNumber,
                        VerifyMode = verifyMode,
                        indRegId = inOutMode,
                        dateTime = logDateTime,
                        WorkCode = workCode
                    });
                }
            }

            Console.WriteLine($"Retrieved {logs.Count} logs from device {deviceId} successfully.");
            Logger.Info($"Retrieved {logs.Count} logs from device {deviceId} successfully.");
            return logs;
        }

        public UserTemplatesResponse GetUserTemplates(int machineNumber, string enrollNumber)
        {
            if (!isDeviceConnected)
            {
                Console.WriteLine("Device not connected. Please connect first.");
                return null;
            }

            ICollection<UserTemplate> templates = new List<UserTemplate>();
            string templateData;
            int templateLength, flag;

            for (int fingerIndex = 0; fingerIndex < 10; fingerIndex++)
            {
                if (objCZKEM.GetUserTmpExStr(machineNumber, enrollNumber, fingerIndex, out flag, out templateData, out templateLength))
                {
                    templates.Add(new UserTemplate
                    {
                        TemplateIndex = fingerIndex,
                        TemplateData = templateData
                    });
                }
            }

            if (templates.Count == 0)
            {
                Console.WriteLine($"No templates found for user with Enroll Number {enrollNumber}.");
            }
            else
            {
                Console.WriteLine($"Retrieved {templates.Count} templates for user with Enroll Number {enrollNumber}.");
            }

            return new UserTemplatesResponse
            {
                user_hash = enrollNumber,
                device_hash = "device_hash",
                data = (List<UserTemplate>)templates
            };
        }

        public bool InsertUserTemplate(int machineNumber, string enrollNumber, int fingerIndex, string templateData, int flag)
        {
            if (!isDeviceConnected)
            {
                Console.WriteLine("Device not connected. Please connect first.");
                return false;
            }

            // Insert the template into the device
            bool result = objCZKEM.SetUserTmpExStr(machineNumber, enrollNumber, fingerIndex, flag, templateData);

            if (result)
            {
                objCZKEM.RefreshData(machineNumber); // Refresh device data
                Console.WriteLine($"Template for user {enrollNumber}, finger index {fingerIndex} inserted successfully.");
            }
            else
            {
                int errorCode = 0;
                objCZKEM.GetLastError(ref errorCode);
                Console.WriteLine($"Failed to insert template. Error Code: {errorCode}");
            }

            return result;
        }

        public UserTemplatesResponse GetUserFaces(int machineNumber, string enrollNumber)
        {
            if (!isDeviceConnected)
            {
                Console.WriteLine("Device not connected. Please connect first.");
                return null;
            }

            ICollection<UserTemplate> templates = new List<UserTemplate>();
            string faceData = "";
            int faceLength = 0;

            for (int faceIndex = 0; faceIndex < 10; faceIndex++)
            {
                if (objCZKEM.GetUserFaceStr(machineNumber, enrollNumber, faceIndex, ref faceData, ref faceLength))
                {
                    templates.Add(new UserTemplate
                    {
                        TemplateIndex = faceIndex,
                        TemplateData = faceData
                    });
                }
            }

            if (templates.Count == 0)
            {
                Console.WriteLine($"No templates found for user with Enroll Number {enrollNumber}.");
            }
            else
            {
                Console.WriteLine($"Retrieved {templates.Count} templates for user with Enroll Number {enrollNumber}.");
            }

            return new UserTemplatesResponse
            {
                user_hash = enrollNumber,
                device_hash = "device_hash",
                data = (List<UserTemplate>)templates
            };
        }

        public bool InsertUserFace(int machineNumber, string enrollNumber, int faceIndex, string faceData, int faceLength)
        {
            if (!isDeviceConnected)
            {
                Console.WriteLine("Device not connected. Please connect first.");
                return false;
            }

            // Insert the template into the device
            bool result = objCZKEM.SetUserFaceStr(machineNumber, enrollNumber, faceIndex, faceData, faceLength);

            if (result)
            {
                objCZKEM.RefreshData(machineNumber); // Refresh device data
                Console.WriteLine($"Template for user {enrollNumber}, finger index {faceIndex} inserted successfully.");
            }
            else
            {
                int errorCode = 0;
                objCZKEM.GetLastError(ref errorCode);
                Console.WriteLine($"Failed to insert template. Error Code: {errorCode}");
            }

            return result;
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
    public class UserTemplatesResponse
    {
        public string user_hash { get; set; }
        public string device_hash { get; set; }
        public List<UserTemplate> data { get; set; }
    }
    public class UserTemplate
    {
        public int TemplateIndex { get; set; }
        public string TemplateData { get; set; }
    }

    public class UserFace
    {
        public string EnrollNumber { get; set; }
        public int FaceIndex { get; set; }
        public string FaceData { get; set; }
        public int FaceLength { get; set; }
    }

    internal class Program
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static HttpClient httpClient = new HttpClient();

        static async Task Main(string[] args)
        {
            App app = new App();
            if (app.Connect("192.168.1.201", 4370))
            {
                string enrollNumber = "1123358"; // Example enroll number
                bool created  = app.CreateUser(1, "1234321", "Abdulkhalek", "", 0, true);

                ICollection<UserTemplate> templates = app.GetUserTemplates(1, enrollNumber);

                foreach (var template in templates)
                {
                    bool inserted = app.InsertUserTemplate(1, "1234321", template.TemplateIndex, template.TemplateData, 1);
                    Console.WriteLine(inserted? "Inserted": "failed");
                }

                ICollection<UserFace> faces = app.GetUserFaces(1, enrollNumber);
                foreach (var face in faces)
                {
                    bool inserted = app.InsertUserFace(1, "1234321", face.FaceIndex, face.FaceData, face.FaceLength);
                    Console.WriteLine(inserted ? "Inserted" : "failed");
                }
            }
            //ConfigureNLog();

            //var client = new SocketClient();
            //client.Connect();
            //Config config = LoadConfig("config.json");
            //if (config == null)
            //{
            //    Console.WriteLine("Failed to load configuration.");
            //    Logger.Error("Failed to load configuration.");
            //    return;
            //}
            //httpClient.BaseAddress = new Uri(config.BASE_URL);

            //Console.ReadLine();

            //client.Disconnect();
        }

        static void ConfigureNLog()
        {
            // Create logging configuration
            var config = new LoggingConfiguration();

            // Define the log file target
            var logfile = new FileTarget("logfile")
            {
                FileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "${shortdate}.log"),
                Layout = "${longdate} ${level:uppercase=true} ${message} ${exception:format=ToString}"
            };

            // Add the target to the configuration
            config.AddTarget(logfile);

            // Define logging rules
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, logfile);

            // Set the configuration
            LogManager.Configuration = config;

            // Ensure logs directory exists
            string logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }
        }

        public static async Task<List<TaskItem>> FetchTasks()
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
                Logger.Error($"Error fetching tasks: {ex.Message}");
                return new List<TaskItem>();
            }
        }

        public static async Task<string> ProcessTask(TaskItem task)
        {
            App app = new App();
            if (task.task_type == "create_user" && (task.status == "pending"))
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
                            bool serverResponse = await CreateUserAsync(enrollNumber, deviceHash, name, task.id);

                            if (serverResponse)
                            {
                                Console.WriteLine("User created successfully on the server.");
                                Logger.Info("User created successfully on the server.");
                                return "create_user:completed:user created successfully";
                            }
                            else
                            {
                                Console.WriteLine("Failed to create user on the server.");
                                Logger.Info("Failed to create user on the server.");
                                return "create_user:failed:can't create user on your server";
                            }
                        }
                        else
                        {
                            Console.WriteLine("Failed to create user on the device.");
                            Logger.Error("Failed to create user on the device.");
                            return "create_user:failed:failed to create user on the device";
                        }
                    }
                    else
                    {
                        Console.WriteLine("Failed to connect to the device.");
                        Logger.Error("Failed to connect to the device.");
                        return "create_user:failed:failed to connect to the device";
                    }
                }
                else
                {
                    Console.WriteLine("Invalid IP or Port in task data.");
                    Logger.Error("Invalid IP or Port in task data.");
                }
            }
            else if (task.task_type == "get_log" && (task.status == "pending")) 
            {
                // Extract IP and port safely using TryGetValue
                if (task.TaskData.TryGetValue("ip", out var ip) &&
                    task.TaskData.TryGetValue("port", out var portString) &&
                    int.TryParse(portString, out var port) &&
                    task.TaskData.TryGetValue("start_date", out var startDateString) &&
                    task.TaskData.TryGetValue("end_date", out var endDateString) &&
                    DateTime.TryParse(startDateString, out var startDate) &&
                    DateTime.TryParse(endDateString, out var endDate))
                {
                    if (app.Connect(ip, port))
                    {
                        // Fetch logs from the device
                        var logs = app.GetLogs(1, ip, portString, startDate, endDate);

                        if (logs != null && logs.Count > 0)
                        {
                            // Convert logs to JSON
                            string jsonLogs = JsonConvert.SerializeObject(logs);

                            // Send logs to the API
                            bool logsSent = await SendLogsToAPI(jsonLogs);

                            if (logsSent)
                            {
                                Console.WriteLine("Logs sent to API successfully.");
                                Logger.Info("Logs sent to API successfully.");
                                return "get_log:completed:logs sent to API successfully";
                            }
                            else
                            {
                                Console.WriteLine("Failed to send logs to API.");
                                Logger.Error("Failed to send logs to API.");
                                return "get_log:failed:failed to send logs to API";
                            }
                        }
                        else
                        {
                            Console.WriteLine("No logs found on the device.");
                            Logger.Info("No logs found on the device.");
                            return "get_log:failed:no logs found on the device";
                        }
                    }
                    else
                    {
                        Console.WriteLine("Failed to connect to the device.");
                        Logger.Error("Failed to connect to the device.");
                        return "get_log:failed:failed to connect to the device";
                    }
                }
                else
                {
                    Console.WriteLine("Invalid IP or Port in task data.");
                    Logger.Info("Invalid IP or Port in task data.");
                    return "get_log:failed:invalid IP or Port in task data";
                }
            }
            else if (task.task_type == "delete_user" &&( task.status == "pending"))
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
                            Logger.Info($"User with Enroll Number {enrollNumber} deleted successfully.");

                            // Optionally notify the server about the deletion
                            bool serverResponse = await DeleteUserAsync(enrollNumber);

                            if (serverResponse)
                            {
                                Console.WriteLine("User deletion confirmed by the server.");
                                Logger.Info("User deletion confirmed by the server.");
                                return "delete_user:completed:user deletion confirmed by the server";
                            }
                            else
                            {
                                Console.WriteLine("Failed to notify the server about user deletion.");
                                Logger.Error("Failed to notify the server about user deletion.");
                                return "delete_user:failed:failed to notify the server about user deletion";
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Failed to delete user with Enroll Number {enrollNumber}.");
                            Logger.Error($"Failed to delete user with Enroll Number {enrollNumber}.");
                            return $"delete_user:failed:failed to delete user with Enroll Number {enrollNumber}";
                        }
                    }
                    else
                    {
                        Console.WriteLine("Failed to connect to the device.");
                        Logger.Error("Failed to connect to the device.");
                        return "delete_user:failed:failed to connect to the device";
                    }
                }
                else
                {
                    Console.WriteLine("Invalid IP or Port in task data.");
                    Logger.Error("Invalid IP or Port in task data.");
                    return "delete_user:failed:invalid IP or Port in task data";
                }
            }
            else if (task.task_type == "update_interval" && ( task.status == "pending"))
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
                        Logger.Info($"Successfully updated the interval for task 'FetchTasks' to {repeatInterval.TotalMinutes} minutes.");
                        return $"update_interval:completed:successfully updated the interval for task 'FetchTasks' to {repeatInterval.TotalMinutes} minutes";
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to update interval for task 'FetchTasks': {ex.Message}");
                        Logger.Error($"Failed to update interval for task 'FetchTasks': {ex.Message}");
                        return $"update_interval:failed:Failed to update interval for task 'FetchTasks' {ex.Message.Replace(':', '-')}";
                    }
                }
                else
                {
                    Console.WriteLine("Invalid task_name or interval_minutes in TaskData.");
                    Logger.Error("Invalid task_name or interval_minutes in TaskData.");
                    return "update_interval:failed:Invalid task_name or interval_minutes in TaskData";
                }
            }
            else if (task.task_type == "get_user_finger_templates" && (task.status == "pending"))
            {
                // Extract IP and port safely using TryGetValue
                if (task.TaskData.TryGetValue("ip", out var ip) &&
                    task.TaskData.TryGetValue("port", out var portString) &&
                    int.TryParse(portString, out var port) &&
                    task.TaskData.TryGetValue("user_hash", out var userHashString))
                {
                    if (app.Connect(ip, port))
                    {

                        task.TaskData.TryGetValue("device_hash", out
                            var deviceHashObj);
                        string deviceHash = deviceHashObj ?? string.Empty;

                        // Fetch logs from the device
                        var userFingers = app.GetUserTemplates(1, userHashString);
                        userFingers.device_hash = deviceHash;

                        if (userFingers != null)
                        {
                            // Convert logs to JSON
                            string jsonLogs = JsonConvert.SerializeObject(userFingers);

                            // Send logs to the API
                            bool logsSent = await SendFingersToAPI(userFingers);

                            if (logsSent)
                            {
                                Console.WriteLine("fingers sent to API successfully.");
                                Logger.Info("fingers sent to API successfully.");
                                return "get_user_finger_templates:completed:fingers sent to API successfully";
                            }
                            else
                            {
                                Console.WriteLine("Failed to send fingers to API.");
                                Logger.Error("Failed to send fingers to API.");
                                return "get_user_finger_templates:failed:failed to send fingers to API";
                            }
                        }
                        else
                        {
                            Console.WriteLine("No fingers found on the device.");
                            Logger.Info("No fingers found on the device.");
                            return "get_user_finger_templates:failed:no fingers found on the device";
                        }
                    }
                    else
                    {
                        Console.WriteLine("Failed to connect to the device.");
                        Logger.Error("Failed to connect to the device.");
                        return "get_user_finger_templates:failed:failed to connect to the device";
                    }
                }
                else
                {
                    Console.WriteLine("Invalid IP or Port in task data.");
                    Logger.Info("Invalid IP or Port in task data.");
                    return "get_user_finger_templates:failed:invalid IP or Port in task data";
                }
            }
            else if (task.task_type == "set_user_finger_templates" && (task.status == "pending"))
            {
                // Extract IP and port safely using TryGetValue
                if (task.TaskData.TryGetValue("ip", out var ip) &&
                    task.TaskData.TryGetValue("port", out var portString) &&
                    int.TryParse(portString, out var port) &&
                    task.TaskData.TryGetValue("finger_index", out var userFingerIndexString) &&
                    task.TaskData.TryGetValue("finger_data", out var userFingerDataString) &&
                    int.TryParse(userFingerIndexString, out var userFingerIndex) &&
                    task.TaskData.TryGetValue("user_hash", out var userHashString))
                {
                    if (app.Connect(ip, port))
                    {

                        task.TaskData.TryGetValue("device_hash", out
                            var deviceHashObj);
                        string deviceHash = deviceHashObj ?? string.Empty;

                        // Fetch logs from the device
                        bool success = app.InsertUserTemplate(1, userHashString, userFingerIndex, userFingerDataString, 1);

                        if (success)
                        {
                            Console.WriteLine($"User with Enroll Number {userHashString} finger data inserted.");
                            Logger.Info($"User with Enroll Number {userHashString} finger data inserted.");
                            return $"set_user_finger_templates:completed:User with Enroll Number {userHashString} finger data inserted.";
                        }
                        else
                        {
                            Console.WriteLine($"Failed to insert finger data for user with Enroll Number {userHashString}.");
                            Logger.Error($"Failed to insert finger data for user with Enroll Number {userHashString}.");
                            return $"set_user_finger_templates:failed:Failed to insert finger data for user with Enroll Number {userHashString}";
                        }
                    }
                    else
                    {
                        Console.WriteLine("Failed to connect to the device.");
                        Logger.Error("Failed to connect to the device.");
                        return "set_user_finger_templates:failed:failed to connect to the device";
                    }
                }
                else
                {
                    Console.WriteLine("Invalid IP or Port in task data.");
                    Logger.Info("Invalid IP or Port in task data.");
                    return "set_user_finger_templates:failed:invalid IP or Port in task data";
                }
            }
            else if (task.task_type == "get_user_face_templates" && (task.status == "pending"))
            {
                // Extract IP and port safely using TryGetValue
                if (task.TaskData.TryGetValue("ip", out var ip) &&
                    task.TaskData.TryGetValue("port", out var portString) &&
                    int.TryParse(portString, out var port) &&
                    task.TaskData.TryGetValue("user_hash", out var userHashString))
                {
                    if (app.Connect(ip, port))
                    {

                        task.TaskData.TryGetValue("device_hash", out
                            var deviceHashObj);
                        string deviceHash = deviceHashObj ?? string.Empty;

                        // Fetch logs from the device
                        var userFingers = app.GetUserFaces(1, userHashString);
                        userFingers.device_hash = deviceHash;

                        if (userFingers != null)
                        {
                            // Convert logs to JSON
                            string jsonLogs = JsonConvert.SerializeObject(userFingers);

                            // Send logs to the API
                            bool logsSent = await SendFacesToAPI(userFingers);

                            if (logsSent)
                            {
                                Console.WriteLine("faces sent to API successfully.");
                                Logger.Info("faces sent to API successfully.");
                                return "get_user_face_templates:completed:faces sent to API successfully";
                            }
                            else
                            {
                                Console.WriteLine("Failed to send faces to API.");
                                Logger.Error("Failed to send faces to API.");
                                return "get_user_face_templates:failed:failed to send faces to API";
                            }
                        }
                        else
                        {
                            Console.WriteLine("No faces found on the device.");
                            Logger.Info("No faces found on the device.");
                            return "get_user_face_templates:failed:no faces found on the device";
                        }
                    }
                    else
                    {
                        Console.WriteLine("Failed to connect to the device.");
                        Logger.Error("Failed to connect to the device.");
                        return "get_user_face_templates:failed:failed to connect to the device";
                    }
                }
                else
                {
                    Console.WriteLine("Invalid IP or Port in task data.");
                    Logger.Info("Invalid IP or Port in task data.");
                    return "get_user_face_templates:failed:invalid IP or Port in task data";
                }
            }
            else if (task.task_type == "set_user_face_templates" && (task.status == "pending"))
            {
                // Extract IP and port safely using TryGetValue
                if (task.TaskData.TryGetValue("ip", out var ip) &&
                    task.TaskData.TryGetValue("port", out var portString) &&
                    int.TryParse(portString, out var port) &&
                    task.TaskData.TryGetValue("face_index", out var userFaceIndexString) &&
                    task.TaskData.TryGetValue("face_data", out var userFaceDataString) &&
                    int.TryParse(userFaceIndexString, out var userFaceIndex) &&
                    task.TaskData.TryGetValue("user_hash", out var userHashString))
                {
                    if (app.Connect(ip, port))
                    {
                        task.TaskData.TryGetValue("device_hash", out
                            var deviceHashObj);
                        string deviceHash = deviceHashObj ?? string.Empty;

                        // Fetch logs from the device
                        bool success = app.InsertUserFace(1, userHashString, userFaceIndex, userFaceDataString, 1);

                        if (success)
                        {
                            Console.WriteLine($"User with Enroll Number {userHashString} face data inserted.");
                            Logger.Info($"User with Enroll Number {userHashString} face data inserted.");
                            return $"set_user_face_templates:completed:User with Enroll Number {userHashString} face data inserted.";
                        }
                        else
                        {
                            Console.WriteLine($"Failed to insert face data for user with Enroll Number {userHashString}.");
                            Logger.Error($"Failed to insert face data for user with Enroll Number {userHashString}.");
                            return $"set_user_face_templates:failed:Failed to insert face data for user with Enroll Number {userHashString}";
                        }
                    }
                    else
                    {
                        Console.WriteLine("Failed to connect to the device.");
                        Logger.Error("Failed to connect to the device.");
                        return "set_user_face_templates:failed:failed to connect to the device";
                    }
                }
                else
                {
                    Console.WriteLine("Invalid IP or Port in task data.");
                    Logger.Info("Invalid IP or Port in task data.");
                    return "set_user_face_templates:failed:invalid IP or Port in task data";
                }
            }

            return "unknow::";
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

                // Read and log the API response
                string responseBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine(responseBody);

                // Ensure the request was successful
                response.EnsureSuccessStatusCode();
                Console.WriteLine("API Response: " + responseBody);
                Logger.Info("API Response: " + responseBody);

                return true;
            }
            catch (HttpRequestException e)
            {
                // Log any HTTP request errors
                Console.WriteLine($"Error sending logs to API: {e.Message}");
                Logger.Error($"Error sending logs to API: {e.Message}");
                return false;
            }
            catch (Exception ex)
            {
                // Log any other unexpected errors
                Console.WriteLine($"Unexpected error sending logs to API: {ex.Message}");
                Logger.Error($"Unexpected error sending logs to API: {ex.Message}");
                return false;
            }
        }
        private static async Task<bool> SendFingersToAPI(UserTemplatesResponse userFingers)
        {
            try
            {
                // Prepare the payload as form data
                var payload = new Dictionary<string, string>
                {
                    { "user_hash", userFingers.user_hash },
                    { "device_hash", userFingers.device_hash },
                    { "data", JsonConvert.SerializeObject(userFingers.data) }
                };

                // Create FormUrlEncodedContent
                var content = new FormUrlEncodedContent(payload);

                // Make the POST request
                HttpResponseMessage response = await httpClient.PostAsync($"{httpClient.BaseAddress}/user-finger/Create", content);

                // Read and log the API response
                string responseBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine(responseBody);

                // Ensure the request was successful
                response.EnsureSuccessStatusCode();
                Console.WriteLine("API Response: " + responseBody);
                Logger.Info("API Response: " + responseBody);

                return true;
            }
            catch (HttpRequestException e)
            {
                // Log any HTTP request errors
                Console.WriteLine($"Error sending logs to API: {e.Message}");
                Logger.Error($"Error sending logs to API: {e.Message}");
                return false;
            }
            catch (Exception ex)
            {
                // Log any other unexpected errors
                Console.WriteLine($"Unexpected error sending logs to API: {ex.Message}");
                Logger.Error($"Unexpected error sending logs to API: {ex.Message}");
                return false;
            }
        }
        
        private static async Task<bool> SendFacesToAPI(UserTemplatesResponse userFingers)
        {
            try
            {
                // Prepare the payload as form data
                var payload = new Dictionary<string, string>
                {
                    { "user_hash", userFingers.user_hash },
                    { "device_hash", userFingers.device_hash },
                    { "data", JsonConvert.SerializeObject(userFingers.data) }
                };

                // Create FormUrlEncodedContent
                var content = new FormUrlEncodedContent(payload);

                // Make the POST request
                HttpResponseMessage response = await httpClient.PostAsync($"{httpClient.BaseAddress}/user-face/Create", content);

                // Read and log the API response
                string responseBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine(responseBody);

                // Ensure the request was successful
                response.EnsureSuccessStatusCode();
                Console.WriteLine("API Response: " + responseBody);
                Logger.Info("API Response: " + responseBody);

                return true;
            }
            catch (HttpRequestException e)
            {
                // Log any HTTP request errors
                Console.WriteLine($"Error sending logs to API: {e.Message}");
                Logger.Error($"Error sending logs to API: {e.Message}");
                return false;
            }
            catch (Exception ex)
            {
                // Log any other unexpected errors
                Console.WriteLine($"Unexpected error sending logs to API: {ex.Message}");
                Logger.Error($"Unexpected error sending logs to API: {ex.Message}");
                return false;
            }
        }

        public static async Task UpdateTaskStatus(string taskId, string status, string msg)
        {
            try
            {
                // Prepare the payload as form data
                var formData = new Dictionary<string, string>
                {
                    { "status", status },
                    { "message", msg }
                };

                // Create FormUrlEncodedContent
                var content = new FormUrlEncodedContent(formData);

                // Make the POST request
                HttpResponseMessage response = await httpClient.PostAsync($"{httpClient.BaseAddress}/zk_que/update/{taskId}", content);
                response.EnsureSuccessStatusCode();

                // Log success
                Console.WriteLine($"Task {taskId} updated to {status}.");
                Logger.Info($"Task {taskId} updated to {status}.");
            }
            catch (Exception ex)
            {
                // Log any errors
                Console.WriteLine($"Error updating task {taskId}: {ex.Message}");
                Logger.Error($"Error updating task {taskId}: {ex.Message}");
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
                Logger.Error($"Error reading configuration file: {ex.Message}");
                return null;
            }
        }
        public static async Task<bool> CreateUserAsync(string userHash, string deviceHash, string name, string id)
        {
            var payload = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "name", name },
                { "user_hash", userHash },
                { "device_hash", deviceHash },
                { "que_id", id},
            });

            while (true)
            {
                try
                {
                    HttpResponseMessage response = await httpClient.PostAsync($"{httpClient.BaseAddress}/zk_users/create", payload);
                    response.EnsureSuccessStatusCode();
                    string responseBody = await response.Content.ReadAsStringAsync();
                    Console.WriteLine(id);
                    return true;
                }
                catch (HttpRequestException e)
                {
                    Console.WriteLine($"Request failed: {e.Message}.");
                    Logger.Error($"Request failed: {e.Message}.");
                    return false;
                }
                catch (TaskCanceledException e)
                {
                    Console.WriteLine($"Request timed out: {e.Message}. Retrying...");
                    Logger.Error($"Request timed out: {e.Message}. Retrying...");
                }

                await Task.Delay(1000);
            }
        }

        public static async Task<bool> DeleteUserAsync(string userId)
        {
            while (true)
            {
                try
                {
                    HttpResponseMessage response = await httpClient.DeleteAsync($"{httpClient.BaseAddress}/zk_users/delete/{userId}");
                    response.EnsureSuccessStatusCode();
                    string responseBody = await response.Content.ReadAsStringAsync();
                    return true;
                }
                catch (HttpRequestException e)
                {
                    Console.WriteLine($"Request failed: {e.Message}.");
                    Logger.Error($"Request failed: {e.Message}.");
                    return false;
                }
                catch (TaskCanceledException e)
                {
                    Console.WriteLine($"Request timed out: {e.Message}. Retrying...");
                    Logger.Error($"Request timed out: {e.Message}. Retrying...");
                }

                await Task.Delay(1000);
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
                                Logger.Info($"Task 'FetchTasks' repeat interval updated to {repeatInterval}.");
                            }
                        }

                        // Save the updated task definition
                        taskService.RootFolder.RegisterTaskDefinition("FetchTasks", task.Definition);
                    }
                    else
                    {
                        Console.WriteLine($"Task 'FetchTasks' does not have any triggers.");
                        Logger.Info($"Task 'FetchTasks' does not have any triggers.");
                    }
                }
                else
                {
                    Console.WriteLine($"Task 'FetchTasks' not found.");
                    Logger.Error($"Task 'FetchTasks' not found.");
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


    public class SocketClient
    {
        private Socket socket;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();


        public void Connect()
        {
            socket = IO.Socket("https://zone.native-code-iq.com:3000/");

            socket.On("connect", () =>
            {
                Console.WriteLine("Connected");
                Logger.Info("Connected");
                socket.Emit("zk_joinRoom", "zk_105");
            });

            socket.On("zk_message", async (data) =>
            {
                if (data.ToString() == "start_fetch")
                {
                    await HandleStartFetch();
                }
            });
        }

        private async Task HandleStartFetch()
        {
            try
            {
                Console.WriteLine("Fetching tasks...");
                Logger.Info("Fetching tasks...");
                List<TaskItem> tasks = await Program.FetchTasks();

                foreach (var task in tasks)
                {
                    await Program.UpdateTaskStatus(task.id, "in_progress", "");

                    string msg = await Program.ProcessTask(task);
                    if (msg.Split(':').ElementAt(1)=="completed")
                    {
                        await Program.UpdateTaskStatus(task.id, "completed", msg);
                    }
                    else
                    {
                       await Program.UpdateTaskStatus(task.id, "failed", msg);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                Logger.Error($"An error occurred: {ex.Message}");
            }
        }


        public void Disconnect()
        {
            socket?.Disconnect();
        }
    }
}