using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Native_BioReader.Models;
using Native_BioReader.Utilities;
using Newtonsoft.Json;

namespace Native_BioReader.Services
{
    public class ApiService
    {
        private readonly HttpClient _client;

        public ApiService(HttpClient client, string baseUrl)
        {
            _client = client;
            _client.BaseAddress = new Uri(baseUrl);
        }

        public async Task<bool> SendLogs(string jsonLogs)
        {
            try
            {
                var payload = new Dictionary<string, string>
                {
                    { "data", jsonLogs }
                };
                var content = new FormUrlEncodedContent(payload);
                HttpResponseMessage response = await _client.PostAsync("/zk_fingerprintsLogs/create", content);
                response.EnsureSuccessStatusCode();
                LoggingHelper.Info("Logs Sent To API Successfully");

                return true;
            }
            catch (Exception ex)
            {
                LoggingHelper.Error("Error sending logs to API", ex);
                return false;
            }
        }
        public async Task<bool> SendFingers(UserTemplatesResponse userFingers)
        {
            try
            {
                var payload = new Dictionary<string, string>
                {
                    { "user_hash", userFingers.user_hash },
                    { "device_hash", userFingers.device_hash },
                    { "data", JsonConvert.SerializeObject(userFingers.data) }
                };
                var content = new FormUrlEncodedContent(payload);
                HttpResponseMessage response = await _client.PostAsync("/user-finger/Create", content);
                response.EnsureSuccessStatusCode();
                LoggingHelper.Info("User Fingers Sent To API Successfully");

                return true;
            }
            catch (Exception ex)
            {
                LoggingHelper.Error("Unexpected error user fingers to API", ex);
                return false;
            }
        }
        public async Task<bool> SendFaces(UserTemplatesResponse userFingers)
        {
            try
            {
                var payload = new Dictionary<string, string>
                {
                    { "user_hash", userFingers.user_hash },
                    { "device_hash", userFingers.device_hash },
                    { "data", JsonConvert.SerializeObject(userFingers.data) }
                };
                var content = new FormUrlEncodedContent(payload);
                HttpResponseMessage response = await _client.PostAsync("/user-face/Create", content);
                response.EnsureSuccessStatusCode();
                LoggingHelper.Info("User Faces Sent To API Successfully");
                return true;
            }
            catch (Exception ex)
            {
                LoggingHelper.Error("Unexpected error sending user faces to API", ex);
                return false;
            }
        }
        public async Task<bool> UpdateTaskStatus(string taskId, string status, string msg)
        {
            try
            {
                var formData = new Dictionary<string, string>
                {
                    { "status", status },
                    { "message", msg }
                };
                var content = new FormUrlEncodedContent(formData);
                HttpResponseMessage response = await _client.PostAsync($"/zk_que/update/{taskId}", content);
                response.EnsureSuccessStatusCode();
                LoggingHelper.Info($"Task {taskId} updated to {status}.");
                return true;
            }
            catch (Exception ex)
            {
                LoggingHelper.Error($"Error updating task {taskId}", ex);
                return false;
            }
        }
        public async Task<bool> CreateUser(string userHash, string deviceHash, string name, string id)
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
                    HttpResponseMessage response = await _client.PostAsync("/zk_users/create", payload);
                    response.EnsureSuccessStatusCode();
                    LoggingHelper.Info("User Sent To API");
                    return true;
                }
                catch (HttpRequestException e)
                {
                    LoggingHelper.Error("Error Sending User Create To API", e);
                    return false;
                }
                catch (TaskCanceledException e)
                {
                    LoggingHelper.Error($"User Create Request timed out: {e.Message}. Retrying...");
                }

                await Task.Delay(1000);
            }
        }
        public async Task<bool> DeleteUser(string userId)
        {
            while (true)
            {
                try
                {
                    HttpResponseMessage response = await _client.DeleteAsync($"/zk_users/delete/{userId}");
                    response.EnsureSuccessStatusCode();
                    return true;
                }
                catch (HttpRequestException e)
                {
                    LoggingHelper.Error("Error Sending User Delete To API", e);
                    return false;
                }
                catch (TaskCanceledException e)
                {
                    LoggingHelper.Error($"User Delete Request timed out: {e.Message}. Retrying...");
                }

                await Task.Delay(1000);
            }
        }
        public async Task<List<TaskItem>> FetchTasks()
        {
            try
            {
                HttpResponseMessage response = await _client.GetAsync("/zk_que/pending");
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();
                var responseObject = JsonConvert.DeserializeObject<ApiResponse>(json);

                return responseObject.Data ?? new List<TaskItem>();
            }
            catch (Exception ex)
            {
                LoggingHelper.Error("Error fetching tasks", ex);
                return new List<TaskItem>();
            }
        }
    }
}
