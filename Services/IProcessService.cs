namespace ThreadPilot.Services
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using ThreadPilot.Models;

    public interface IProcessService
    {
        Task<ObservableCollection<ProcessModel>> GetProcessesAsync();

        Task SetProcessorAffinity(ProcessModel process, long affinityMask);

        Task<AffinityApplyResult> SetProcessorAffinity(ProcessModel process, CpuSelection selection);

        Task SetProcessPriority(ProcessModel process, ProcessPriorityClass priority);

        Task<bool> SaveProcessProfile(string profileName, ProcessModel process);

        Task<bool> LoadProcessProfile(string profileName, ProcessModel process);

        Task RefreshProcessInfo(ProcessModel process);

        Task<ProcessModel?> GetProcessByIdAsync(int processId);

        Task<IEnumerable<ProcessModel>> GetProcessesByNameAsync(string executableName);

        Task<bool> IsProcessRunningAsync(string executableName);

        Task<IEnumerable<ProcessModel>> GetProcessesWithPathsAsync();

        Task<ObservableCollection<ProcessModel>> GetActiveApplicationsAsync();

        ProcessModel CreateProcessModel(Process process);

        Task<bool> IsProcessStillRunning(ProcessModel process);

        Task<bool> SetIdleServerStateAsync(ProcessModel process, bool enableIdleServer);

        Task<bool> SetRegistryPriorityAsync(ProcessModel process, bool enable, ProcessPriorityClass priority);

        void SetUseCpuSets(bool useCpuSets);

        bool GetUseCpuSets();

        Task<bool> ClearProcessCpuSetAsync(ProcessModel process);

        Task ClearAllAppliedMasksAsync();

        Task ResetAllProcessPrioritiesAsync();

        void TrackAppliedMask(int processId, string maskId);

        void TrackPriorityChange(int processId, ProcessPriorityClass originalPriority);

        void UntrackProcess(int processId);
    }
}
