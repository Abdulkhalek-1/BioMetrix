using System.Threading.Tasks;
using Native_BioReader.Models;

namespace Native_BioReader.Services.TaskProcessor.Processors
{
    public interface ITaskProcessor
    {
        Task<string> ProcessTask(TaskItem task);
    }
}
