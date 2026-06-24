namespace ThreadPilot.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using ThreadPilot.Models;

    public class ProfileApplicationEventArgs : EventArgs
    {
        public ConditionalProcessProfile Profile { get; set; } = new();

        public ProcessModel Process { get; set; } = new();

        public SystemState SystemState { get; set; } = new();

        public bool WasApplied { get; set; }

        public string Reason { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class ProfileConflictEventArgs : EventArgs
    {
        public List<ConditionalProcessProfile> ConflictingProfiles { get; set; } = new();

        public ProcessModel Process { get; set; } = new();

        public ConditionalProcessProfile SelectedProfile { get; set; } = new();

        public string Resolution { get; set; } = string.Empty;
    }

    public interface IConditionalProfileService
    {
        Task InitializeAsync();

        Task AddProfileAsync(ConditionalProcessProfile profile);

        Task RemoveProfileAsync(string profileId);

        Task UpdateProfileAsync(ConditionalProcessProfile profile);

        Task<List<ConditionalProcessProfile>> GetAllProfilesAsync();

        Task<List<ConditionalProcessProfile>> GetProfilesForProcessAsync(string processName);

        Task<List<ConditionalProcessProfile>> EvaluateProfilesAsync(ProcessModel process);

        Task<bool> ApplyBestProfileAsync(ProcessModel process);

        Task<SystemState> GetSystemStateAsync();

        Task StartMonitoringAsync();

        Task StopMonitoringAsync();

        bool IsMonitoring { get; }

        ConditionalProcessProfile ResolveProfileConflict(List<ConditionalProcessProfile> conflictingProfiles, ProcessModel process);

        ConditionalProcessProfile CreateDefaultProfile(string processName);

        Task<(bool IsValid, List<string> Errors)> ValidateProfileAsync(ConditionalProcessProfile profile);

        Task<string> ExportProfilesToJsonAsync();

        Task<int> ImportProfilesFromJsonAsync(string json);

        event EventHandler<ProfileApplicationEventArgs>? ProfileApplied;

        event EventHandler<ProfileConflictEventArgs>? ProfileConflictResolved;

        event EventHandler<SystemState>? SystemStateChanged;
    }
}

