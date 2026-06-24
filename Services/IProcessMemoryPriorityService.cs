/*
 * ThreadPilot - process memory priority service contract.
 */
namespace ThreadPilot.Services
{
    using ThreadPilot.Models;

    public interface IProcessMemoryPriorityService
    {
        Task<ProcessMemoryPriority?> GetMemoryPriorityAsync(ProcessModel process);

        Task<ProcessOperationResult> SetMemoryPriorityAsync(ProcessModel process, ProcessMemoryPriority priority);
    }
}
