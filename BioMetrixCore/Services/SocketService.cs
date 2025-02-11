using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Native_BioReader.Models;
using Quobject.SocketIoClientDotNet.Client;
using Native_BioReader.Services;
using Native_BioReader.Services.TaskProcessor;

namespace Native_BioReader.Utilities
{
    public class SocketService
    {
        private Socket socket;
        private readonly ApiService _apiService;
        private readonly TaskProcessorManager _taskProcessorManager;
        public SocketService(TaskProcessorManager taskProcessorManager, ApiService apiService)
        {
            _apiService = apiService;
            _taskProcessorManager = taskProcessorManager;
            Connect();
        }
        public void Connect()
        {
            socket = IO.Socket("https://zone.native-code-iq.com:3000/");

            socket.On("connect", () =>
            {
                LoggingHelper.Info("Socket Connected");
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
                LoggingHelper.Info("Fetching tasks...");
                List<TaskItem> tasks = await _apiService.FetchTasks();

                foreach (var task in tasks)
                {
                    await _apiService.UpdateTaskStatus(task.id, "in_progress", "");
                    string msg = await _taskProcessorManager.ProcessTask(task);
                    if (msg.Split(':').ElementAt(1) == "completed")
                    {
                        await _apiService.UpdateTaskStatus(task.id, "completed", msg);
                    }
                    else
                    {
                        await _apiService.UpdateTaskStatus(task.id, "failed", msg);
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingHelper.Error($"An error occurred while HandleStartFetch in SocketClient", ex);
            }
        }


        public void Disconnect()
        {
            socket?.Disconnect();
        }
    }
}
