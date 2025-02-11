using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Native_BioReader.Services.TaskProcessor.Processors;
using System.Threading.Tasks;
using Native_BioReader.Models;
using Native_BioReader.Utilities;

namespace Native_BioReader.Services.TaskProcessor
{
    public class TaskProcessorManager
    {
        private readonly TaskProcessorFactory _factory;

        public TaskProcessorManager(TaskProcessorFactory factory)
        {
            _factory = factory;
        }

        public async Task<string> ProcessTask(TaskItem task)
        {
            try
            {
                ITaskProcessor processor = _factory.GetProcessor(task.task_type);
                return await processor.ProcessTask(task);
            }
            catch (ArgumentException ex)
            {
                LoggingHelper.Error(ex.Message);
                return $"{task.task_type}:failed:{ex.Message}";
            }
            catch (Exception ex)
            {
                LoggingHelper.Error($"An error occurred processing task '{task.task_type}'|{task.id}: {ex.Message}");
                return $"{task.task_type}:failed:unexpected error";
            }
        }
    }

}
