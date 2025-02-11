using System;
using Native_BioReader.Models;
using System.Threading.Tasks;
using Native_BioReader.Utilities;

namespace Native_BioReader.Services.TaskProcessor.Processors
{
    public class UpdateIntervalProcessor : ITaskProcessor
    {
        public async Task<string> ProcessTask(TaskItem task)
        {
            // Getting data from task
            task.TaskData.TryGetValue("interval", out var intervalMinutesString);

            // Parsing data
            int.TryParse(intervalMinutesString, out var intervalMinutes);

            // Validate data
            if (!string.IsNullOrEmpty(intervalMinutesString))
            {
                LoggingHelper.Warn($"Invalid task data Task|{task.id}.");
                return "update_interval:failed:invalid task data";
            }


            // Doing the job
            try
            {
                const string taskName = "Fetch";
                TimeSpan repeatInterval = TimeSpan.FromMinutes(intervalMinutes);
                TaskSchedulerHelper.UpdateTaskRepeatInterval(repeatInterval, taskName);
                return "update_interval:completed:interval updated successfully";
            }
            catch (Exception ex)
            {
                LoggingHelper.Error($"Failed to update interval Task|{task.id}.", ex);
                return "update_interval:failed:failed to update interval";
            }
        }
    }
}
