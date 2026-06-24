namespace ThreadPilot.Services
{
    using System;
    using System.Threading.Tasks;

    public interface ISystemTweaksService
    {
        event EventHandler<TweakStatusChangedEventArgs>? TweakStatusChanged;

        Task<TweakStatus> GetCoreParkingStatusAsync();

        Task<bool> SetCoreParkingAsync(bool enabled);

        Task<TweakStatus> GetCStatesStatusAsync();

        Task<bool> SetCStatesAsync(bool enabled);

        Task<TweakStatus> GetSysMainStatusAsync();

        Task<bool> SetSysMainAsync(bool enabled);

        Task<TweakStatus> GetPrefetchStatusAsync();

        Task<bool> SetPrefetchAsync(bool enabled);

        Task<TweakStatus> GetPowerThrottlingStatusAsync();

        Task<bool> SetPowerThrottlingAsync(bool enabled);

        Task<TweakStatus> GetHpetStatusAsync();

        Task<bool> SetHpetAsync(bool enabled);

        Task<TweakStatus> GetHighSchedulingCategoryStatusAsync();

        Task<bool> SetHighSchedulingCategoryAsync(bool enabled);

        Task<TweakStatus> GetMenuShowDelayStatusAsync();

        Task<bool> SetMenuShowDelayAsync(bool enabled);

        Task RefreshAllStatusesAsync();
    }

    public class TweakStatus
    {
        public bool IsEnabled { get; set; }

        public bool IsAvailable { get; set; }

        public string? ErrorMessage { get; set; }

        public string? Description { get; set; }
    }

    public class TweakStatusChangedEventArgs : EventArgs
    {
        public string TweakName { get; }

        public TweakStatus Status { get; }

        public DateTime ChangeTime { get; }

        public TweakStatusChangedEventArgs(string tweakName, TweakStatus status)
        {
            this.TweakName = tweakName;
            this.Status = status;
            this.ChangeTime = DateTime.UtcNow;
        }
    }

    public enum SystemTweak
    {
        CoreParking,
        CStates,
        SysMain,
        Prefetch,
        PowerThrottling,
        Hpet,
        HighSchedulingCategory,
        MenuShowDelay,
    }
}

