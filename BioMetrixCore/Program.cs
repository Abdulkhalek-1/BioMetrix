using System;
using System.Net.Http;
using Native_BioReader.Models;
using Native_BioReader.Services;
using Native_BioReader.Services.TaskProcessor;
using Native_BioReader.Utilities;

namespace Native_BioReader
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Config config = ConfigHelper.LoadConfig();
            DeviceService deviceService = new DeviceService();
            ApiService apiService = new ApiService(new HttpClient(), config.BASE_URL);
            TaskProcessorFactory taskProcessorFactory = new TaskProcessorFactory(deviceService, apiService);
            TaskProcessorManager taskProcessorManager = new TaskProcessorManager(taskProcessorFactory);
            SocketService socketService = new SocketService(taskProcessorManager, apiService);

            // Keep the program running
            Console.ReadLine();
        }
    }
}