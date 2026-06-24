namespace ThreadPilot.Services
{
    using System;
    using System.Threading.Tasks;
    using ThreadPilot.Models;

    public interface ISystemTrayService : IDisposable
    {
        event EventHandler? QuickApplyRequested;

        event EventHandler? ShowMainWindowRequested;

        event EventHandler? ExitRequested;

        event EventHandler<MonitoringToggleEventArgs>? MonitoringToggleRequested;

        event EventHandler? SettingsRequested;

        event EventHandler<PowerPlanChangeRequestedEventArgs>? PowerPlanChangeRequested;

        event EventHandler<ProfileApplicationRequestedEventArgs>? ProfileApplicationRequested;

        event EventHandler? PerformanceDashboardRequested;

        event EventHandler? DashboardRequested;

        void Initialize();

        void Show();

        void Hide();

        void UpdateTooltip(string tooltip);

        void ShowBalloonTip(string title, string text, int timeoutMs = 3000);

        void UpdateContextMenu(string? selectedProcessName = null, bool hasSelection = false);

        void UpdateMonitoringStatus(bool isMonitoring, bool isWmiAvailable = true);

        void UpdateTrayIcon(TrayIconState state);

        void ShowTrayNotification(string title, string message, NotificationType type = NotificationType.Information, int timeoutMs = 3000);

        void UpdateSettings(ApplicationSettingsModel settings);

        void ApplyTheme(bool useDarkTheme);

        void UpdatePowerPlans(IEnumerable<PowerPlanModel> powerPlans, PowerPlanModel? activePlan);

        void UpdateProfiles(IEnumerable<string> profileNames);

        void UpdateSystemStatus(string currentPowerPlan);

        void UpdateSystemStatus(string currentPowerPlan, double cpuUsage, double memoryUsage);
    }

    public class MonitoringToggleEventArgs : EventArgs
    {
        public bool EnableMonitoring { get; }

        public MonitoringToggleEventArgs(bool enableMonitoring)
        {
            this.EnableMonitoring = enableMonitoring;
        }
    }

    public class PowerPlanChangeRequestedEventArgs : EventArgs
    {
        public string PowerPlanGuid { get; }

        public string PowerPlanName { get; }

        public PowerPlanChangeRequestedEventArgs(string powerPlanGuid, string powerPlanName)
        {
            this.PowerPlanGuid = powerPlanGuid;
            this.PowerPlanName = powerPlanName;
        }
    }

    public class ProfileApplicationRequestedEventArgs : EventArgs
    {
        public string ProfileName { get; }

        public ProfileApplicationRequestedEventArgs(string profileName)
        {
            this.ProfileName = profileName;
        }
    }

    public enum TrayIconState
    {
        Normal,
        Monitoring,
        Error,
        Disabled,
    }
}

