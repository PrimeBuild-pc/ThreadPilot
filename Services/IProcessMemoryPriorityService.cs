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

        Task<ProcessIoPriority?> GetIoPriorityAsync(ProcessModel process) =>
            Task.FromResult<ProcessIoPriority?>(null);

        Task<ProcessOperationResult> SetIoPriorityAsync(ProcessModel process, ProcessIoPriority priority) =>
            Task.FromResult(ProcessOperationResult.Failed(
                "Unsupported",
                "I/O priority is unavailable on this system.",
                "The configured priority service does not support I/O priority."));

        Task<ProcessEfficiencyMode?> GetEfficiencyModeAsync(ProcessModel process) =>
            Task.FromResult<ProcessEfficiencyMode?>(null);

        Task<ProcessOperationResult> SetEfficiencyModeAsync(ProcessModel process, ProcessEfficiencyMode mode) =>
            Task.FromResult(ProcessOperationResult.Failed(
                "Unsupported",
                "Efficiency mode is unavailable on this system.",
                "The configured priority service does not support efficiency mode."));
    }
}
