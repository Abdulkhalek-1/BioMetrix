using System.Collections.Generic;
using System;
using Newtonsoft.Json;
using zkemkeeper;
using System.IO;

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
                Console.WriteLine("Device connected successfully.");
            }
            else
            {
                Console.WriteLine("Failed to connect to the device. Check the IP address and port.");
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

        public bool IsDeviceConnected => isDeviceConnected;
    }

    class Config
    {
        public string IPAddress { get; set; }
        public int Port { get; set; }
    }

    internal class Program
    {
        static void Main(string[] args)
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

                App app = new App();
                Console.WriteLine("Connecting to device...");
                if (!app.Connect(config.IPAddress, config.Port))
                {
                    return;
                }

                //Console.WriteLine("Creating a new user...");
                //bool isCreated = app.CreateUser(1, "12345", "John Doe", "1234", 0, true);

                //if (isCreated)
                //{
                //    Console.WriteLine("Fetching all user information...");
                //    ICollection<UserInfo> users = app.GetAllUserInfo(1);

                //    foreach (var user in users)
                //    {
                //        Console.WriteLine($"Enroll Number: {user.EnrollNumber}, Name: {user.Name}, Privilege: {user.Privelage}, Enabled: {user.Enabled}");
                //    }
                //}
                //else
                //{
                //    Console.WriteLine("Failed to create the user.");
                //}
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
            Console.Write("Press Any Key...");
            Console.ReadKey();
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
    }

}
