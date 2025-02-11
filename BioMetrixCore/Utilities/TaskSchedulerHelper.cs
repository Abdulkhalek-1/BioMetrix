using System;
using Microsoft.Win32.TaskScheduler;

namespace Native_BioReader.Utilities
{
    public static class TaskSchedulerHelper
    {
        public static void UpdateTaskRepeatInterval(TimeSpan repeatInterval, string taskName)
        {
            using (TaskService taskService = new TaskService())
            {
                // Get the task by name
                Task task = taskService.GetTask(taskName);

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

                                Console.WriteLine($"Task '{taskName}' repeat interval updated to {repeatInterval}.");
                                LoggingHelper.Info($"Task '{taskName}' repeat interval updated to {repeatInterval}.");
                            }
                        }

                        // Save the updated task definition
                        taskService.RootFolder.RegisterTaskDefinition(taskName, task.Definition);
                    }
                    else
                    {
                        Console.WriteLine($"Task '{taskName}' does not have any triggers.");
                        LoggingHelper.Info($"Task '{taskName}' does not have any triggers.");
                    }
                }
                else
                {
                    Console.WriteLine($"Task '{taskName}' not found.");
                    LoggingHelper.Error($"Task '{taskName}' not found.");
                }
            }
        }
    }
}
