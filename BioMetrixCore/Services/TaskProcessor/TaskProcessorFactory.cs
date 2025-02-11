using Native_BioReader.Services.TaskProcessor.Processors;
using System.Collections.Generic;
using System;

namespace Native_BioReader.Services.TaskProcessor
{
    public class TaskProcessorFactory
    {
        private readonly Dictionary<string, ITaskProcessor> _processors;

        public TaskProcessorFactory(DeviceService deviceService, ApiService apiService)
        {
            // Register processors for each task type.
            _processors = new Dictionary<string, ITaskProcessor>
            {
                {"create_user", new CreateUserProcessor(deviceService, apiService)},
                {"get_log", new GetLogProcessor(deviceService, apiService)},
                {"delete_user", new DeleteUserProcessor(deviceService, apiService)},
                {"update_interval", new UpdateIntervalProcessor()},
                {"get_user_finger_templates", new GetUserFingerTemplatesProcessor(deviceService, apiService)},
                {"set_user_finger_templates", new SetUserFingerTemplatesProcessor(deviceService)},
                {"get_user_face_templates", new GetUserFaceTemplatesProcessor(deviceService, apiService)},
                {"set_user_face_templates", new SetUserFaceTemplatesProcessor(deviceService)},
            };
        }

        public ITaskProcessor GetProcessor(string taskType)
        {
            if (_processors.TryGetValue(taskType, out var processor))
            {
                return processor;
            }
            throw new ArgumentException($"No processor found for task type '{taskType}'");
        }
    }
}