using System;
using System.Collections.Generic;
using Native_BioReader.Models;
using zkemkeeper;
using Native_BioReader.Utilities;

namespace Native_BioReader.Services
{
    public class DeviceService
    {
        private CZKEM objCZKEM = new CZKEM();
        private bool _isDeviceConnected = false;
        public bool IsDeviceConnected => _isDeviceConnected;

        public bool Connect(string ipAddress, int portNumber)
        {
            _isDeviceConnected = objCZKEM.Connect_Net(ipAddress, portNumber);

            if (_isDeviceConnected)
            {
                LoggingHelper.Info($"Device at {ipAddress}:{portNumber} connected successfully.");
                return _isDeviceConnected;
            }
            else
            {
                LoggingHelper.Warn($"Failed to connect to the device at {ipAddress}:{portNumber}. Check the IP address and port.");
                return _isDeviceConnected;
            }
        }

        public bool CreateUser(int machineNumber, string enrollNumber, string name, string password, int privilege, bool enabled)
        {
            if (!_isDeviceConnected)
            {
                LoggingHelper.Error("Cannont CreateUser: Device not connected. Please connect first.");
                return false;
            }

            bool result = objCZKEM.SSR_SetUserInfo(machineNumber, enrollNumber, name, password, privilege, enabled);

            if (result)
            {
                objCZKEM.RefreshData(machineNumber);
                LoggingHelper.Info($"User {name} with Enroll Number {enrollNumber} created successfully.");
            }
            else
            {
                int errorCode = 0;
                objCZKEM.GetLastError(ref errorCode);
                LoggingHelper.Warn($"Failed to create user. Error Code: {errorCode}");
            }

            return result;
        }

        public bool DeleteUser(int machineNumber, string enrollNumber)
        {
            if (!_isDeviceConnected)
            {
                LoggingHelper.Error("Cannont DeleteUser: Device not connected. Please connect first.");
                return false;
            }

            bool result = objCZKEM.SSR_DeleteEnrollData(machineNumber, enrollNumber, 12);

            if (result)
            {
                objCZKEM.RefreshData(machineNumber);
                LoggingHelper.Info($"User with Enroll Number {enrollNumber} deleted successfully.");
            }
            else
            {
                int errorCode = 0;
                objCZKEM.GetLastError(ref errorCode);
                LoggingHelper.Warn($"Failed to delete user. Error Code: {errorCode}");
            }

            return result;
        }

        public ICollection<UserInfo> GetAllUserInfo(int machineNumber)
        {
            string enrollNumber, name, password, templateData;
            int privilege, templateLength, flag;
            bool enabled;

            ICollection<UserInfo> users = new List<UserInfo>();

            if (!_isDeviceConnected)
            {
                LoggingHelper.Error("Cannont GetAllUserInfo: Device not connected. Please connect first.");
                return users;
            }

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
            if (!_isDeviceConnected)
            {
                LoggingHelper.Error("Cannont GetLog: Device not connected.Please connect first.");
                return null;
            }

            ICollection<LogInfo> logs = new List<LogInfo>();


            if (!objCZKEM.ReadGeneralLogData(machineNumber))
            {
                int errorCode = 0;
                objCZKEM.GetLastError(ref errorCode);
                LoggingHelper.Warn($"Failed to read logs. Error Code: {errorCode}");
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

            LoggingHelper.Info($"Retrieved {logs.Count} logs from device {deviceId} successfully.");
            return logs;
        }

        public UserTemplatesResponse GetUserFingers(int machineNumber, string enrollNumber)
        {
            if (!_isDeviceConnected)
            {
                LoggingHelper.Error("Cannot GetUserTemplates: Device not connected. Please connect first.");
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
                LoggingHelper.Warn($"No templates found for user with Enroll Number {enrollNumber}.");
            }
            else
            {
                LoggingHelper.Info($"Retrieved {templates.Count} templates for user with Enroll Number {enrollNumber}.");
            }

            return new UserTemplatesResponse
            {
                user_hash = enrollNumber,
                device_hash = "device_hash",
                data = (List<UserTemplate>)templates
            };
        }

        public bool InsertUserFinger(int machineNumber, string enrollNumber, int fingerIndex, string templateData, int flag)
        {
            if (!_isDeviceConnected)
            {
                LoggingHelper.Error("Cannot InsertUserTemplate: Device not connected. Please connect first.");
                return false;
            }

            // Insert the template into the device
            bool result = objCZKEM.SetUserTmpExStr(machineNumber, enrollNumber, fingerIndex, flag, templateData);

            if (result)
            {
                objCZKEM.RefreshData(machineNumber); // Refresh device data
                LoggingHelper.Info($"Template for user {enrollNumber}, finger index {fingerIndex} inserted successfully.");
            }
            else
            {
                int errorCode = 0;
                objCZKEM.GetLastError(ref errorCode);
                LoggingHelper.Warn($"Failed to insert template. Error Code: {errorCode}");
            }

            return result;
        }

        public UserTemplatesResponse GetUserFaces(int machineNumber, string enrollNumber)
        {
            if (!_isDeviceConnected)
            {
                LoggingHelper.Error("Cannot GetUserFaces: Device not connected. Please connect first.");
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
                LoggingHelper.Warn($"No templates found for user with Enroll Number {enrollNumber}.");
            }
            else
            {
                LoggingHelper.Info($"Retrieved {templates.Count} templates for user with Enroll Number {enrollNumber}.");
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
            if (!_isDeviceConnected)
            {
                LoggingHelper.Error("Cannot InsertUserFace: Device not connected. Please connect first.");
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

    }
}
