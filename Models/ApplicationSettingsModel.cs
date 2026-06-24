namespace ThreadPilot.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Text.Json;
    using CommunityToolkit.Mvvm.ComponentModel;
    using ThreadPilot.Models.Core;
    using ThreadPilot.Services;

    public partial class ApplicationSettingsModel : ObservableObject, IModel
    {
        private static readonly JsonSerializerOptions UserSettingsComparisonJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        [ObservableProperty]
        private string id = "ApplicationSettings"; // Singleton settings

        [ObservableProperty]
        private DateTime createdAt = DateTime.UtcNow;

        [ObservableProperty]
        private DateTime updatedAt = DateTime.UtcNow;

        [ObservableProperty]
        private bool enableNotifications = true;

        [ObservableProperty]
        private NotificationLevelProfile notificationLevel = NotificationLevelProfile.All;

        [ObservableProperty]
        private bool enableBalloonNotifications = true;

        [ObservableProperty]
        private bool enableToastNotifications = true;

        [ObservableProperty]
        private bool enablePowerPlanChangeNotifications = true;

        [ObservableProperty]
        private bool enableProcessMonitoringNotifications = true;

        [ObservableProperty]
        private bool enableErrorNotifications = true;

        [ObservableProperty]
        private bool enableSuccessNotifications = true;

        [ObservableProperty]
        private bool minimizeToTray = true;

        [ObservableProperty]
        private bool closeToTray = true; // Default true: close to tray like CPU Set Setter

        [ObservableProperty]
        private bool startMinimized = false;

        [ObservableProperty]
        private bool showTrayIcon = true;

        [ObservableProperty]
        private bool enableQuickApplyFromTray = true;

        [ObservableProperty]
        private bool enableMonitoringControlFromTray = true;

        [ObservableProperty]
        private int notificationDisplayDurationMs = 3000;

        [ObservableProperty]
        private int balloonNotificationTimeoutMs = 5000;

        [ObservableProperty]
        private NotificationPosition notificationPosition = NotificationPosition.BottomRight;

        [ObservableProperty]
        private NotificationSound notificationSound = NotificationSound.Default;

        [ObservableProperty]
        private bool enableNotificationSound = false;

        [ObservableProperty]
        private string customTrayIconPath = string.Empty;

        [ObservableProperty]
        private bool useCustomTrayIcon = false;

        [ObservableProperty]
        private TrayIconStyle trayIconStyle = TrayIconStyle.Default;

        [ObservableProperty]
        private bool showDetailedTooltips = true;

        [ObservableProperty]
        private bool enableContextMenuAnimations = true;

        [ObservableProperty]
        private bool autoHideNotifications = true;

        [ObservableProperty]
        private bool enableNotificationHistory = true;

        [ObservableProperty]
        private int maxNotificationHistoryItems = 50;

        // Autostart Settings
        [ObservableProperty]
        private bool autostartWithWindows = true;

        // Power Plan Settings
        [ObservableProperty]
        private string defaultPowerPlanId = string.Empty;

        [ObservableProperty]
        private string defaultPowerPlanName = "Balanced";

        [ObservableProperty]
        private bool restoreDefaultPowerPlanOnExit = true;

        [ObservableProperty]
        private bool clearMasksOnClose = true;

        [ObservableProperty]
        private bool useDarkTheme = false;

        [ObservableProperty]
        private bool hasUserThemePreference = false;

        [ObservableProperty]
        private string language = LocalizationService.DefaultLanguage;

        [ObservableProperty]
        private bool enableAutomaticUpdateChecks = true;

        [ObservableProperty]
        private DateTimeOffset? lastUpdateCheckUtc = null;

        [ObservableProperty]
        private int updateCheckIntervalDays = 7;

        [ObservableProperty]
        private bool includePrereleaseUpdates = false;

        // Monitoring Settings
        [ObservableProperty]
        private int pollingIntervalMs = 5000;

        [ObservableProperty]
        private int fallbackPollingIntervalMs = 10000;

        [ObservableProperty]
        private bool enableWmiMonitoring = true;

        [ObservableProperty]
        private bool enableFallbackPolling = true;

        [ObservableProperty]
        private bool applyPersistentRulesOnProcessStart = true;

        // Advanced Settings
        [ObservableProperty]
        private bool enableDebugLogging = false;

        [ObservableProperty]
        private bool enablePerformanceCounters = false;

        [ObservableProperty]
        private bool hasSeenPerformanceIntro = false;

        [ObservableProperty]
        private bool hasSeenElevationWarning = false;

        [ObservableProperty]
        private bool hasSeenStartupMinimizedSuggestion = false;

        [ObservableProperty]
        private bool enableSelfLowImpactMode = true;

        [ObservableProperty]
        private bool enableSelfAffinityLimit = false;

        [ObservableProperty]
        private int maxLogFileSizeMb = 10;

        [ObservableProperty]
        private int logRetentionDays = 7;

        [ObservableProperty]
        private List<KeyboardShortcut> keyboardShortcuts = new();

        public void CopyFrom(ApplicationSettingsModel other)
        {
            if (other == null)
            {
                return;
            }

            this.EnableNotifications = other.EnableNotifications;
            this.NotificationLevel = other.NotificationLevel;
            this.EnableBalloonNotifications = other.EnableBalloonNotifications;
            this.EnableToastNotifications = other.EnableToastNotifications;
            this.EnablePowerPlanChangeNotifications = other.EnablePowerPlanChangeNotifications;
            this.EnableProcessMonitoringNotifications = other.EnableProcessMonitoringNotifications;
            this.EnableErrorNotifications = other.EnableErrorNotifications;
            this.EnableSuccessNotifications = other.EnableSuccessNotifications;
            this.MinimizeToTray = other.MinimizeToTray;
            this.CloseToTray = other.CloseToTray;
            this.StartMinimized = other.StartMinimized;
            this.ShowTrayIcon = other.ShowTrayIcon;
            this.EnableQuickApplyFromTray = other.EnableQuickApplyFromTray;
            this.EnableMonitoringControlFromTray = other.EnableMonitoringControlFromTray;
            this.NotificationDisplayDurationMs = other.NotificationDisplayDurationMs;
            this.BalloonNotificationTimeoutMs = other.BalloonNotificationTimeoutMs;
            this.NotificationPosition = other.NotificationPosition;
            this.NotificationSound = other.NotificationSound;
            this.EnableNotificationSound = other.EnableNotificationSound;
            this.CustomTrayIconPath = other.CustomTrayIconPath;
            this.UseCustomTrayIcon = other.UseCustomTrayIcon;
            this.TrayIconStyle = other.TrayIconStyle;
            this.ShowDetailedTooltips = other.ShowDetailedTooltips;
            this.EnableContextMenuAnimations = other.EnableContextMenuAnimations;
            this.AutoHideNotifications = other.AutoHideNotifications;
            this.EnableNotificationHistory = other.EnableNotificationHistory;
            this.MaxNotificationHistoryItems = other.MaxNotificationHistoryItems;

            // Autostart Settings
            this.AutostartWithWindows = other.AutostartWithWindows;

            // Power Plan Settings
            this.DefaultPowerPlanId = other.DefaultPowerPlanId;
            this.DefaultPowerPlanName = other.DefaultPowerPlanName;
            this.RestoreDefaultPowerPlanOnExit = other.RestoreDefaultPowerPlanOnExit;
            this.ClearMasksOnClose = other.ClearMasksOnClose;
            this.UseDarkTheme = other.UseDarkTheme;
            this.HasUserThemePreference = other.HasUserThemePreference;
            this.Language = LocalizationService.NormalizeLanguage(other.Language);
            this.EnableAutomaticUpdateChecks = other.EnableAutomaticUpdateChecks;
            this.LastUpdateCheckUtc = other.LastUpdateCheckUtc;
            this.UpdateCheckIntervalDays = other.UpdateCheckIntervalDays;
            this.IncludePrereleaseUpdates = other.IncludePrereleaseUpdates;

            // Monitoring Settings
            this.PollingIntervalMs = other.PollingIntervalMs;
            this.FallbackPollingIntervalMs = other.FallbackPollingIntervalMs;
            this.EnableWmiMonitoring = other.EnableWmiMonitoring;
            this.EnableFallbackPolling = other.EnableFallbackPolling;
            this.ApplyPersistentRulesOnProcessStart = other.ApplyPersistentRulesOnProcessStart;

            // Advanced Settings
            this.EnableDebugLogging = other.EnableDebugLogging;
            this.EnablePerformanceCounters = other.EnablePerformanceCounters;
            this.HasSeenPerformanceIntro = other.HasSeenPerformanceIntro;
            this.HasSeenElevationWarning = other.HasSeenElevationWarning;
            this.HasSeenStartupMinimizedSuggestion = other.HasSeenStartupMinimizedSuggestion;
            this.EnableSelfLowImpactMode = other.EnableSelfLowImpactMode;
            this.EnableSelfAffinityLimit = other.EnableSelfAffinityLimit;
            this.MaxLogFileSizeMb = other.MaxLogFileSizeMb;
            this.LogRetentionDays = other.LogRetentionDays;

            // Keyboard Shortcuts
            this.KeyboardShortcuts = other.KeyboardShortcuts != null
                ? new List<KeyboardShortcut>(other.KeyboardShortcuts)
                : new List<KeyboardShortcut>();
        }

        // IModel implementation - properties are auto-generated by ObservableProperty
        public ValidationResult Validate()
        {
            var errors = new List<string>();

            if (this.NotificationDisplayDurationMs < 1000 || this.NotificationDisplayDurationMs > 30000)
            {
                errors.Add("Notification display duration must be between 1 and 30 seconds");
            }

            if (this.PollingIntervalMs < 1000 || this.PollingIntervalMs > 60000)
            {
                errors.Add("Process polling interval must be between 1 and 60 seconds");
            }

            if (this.FallbackPollingIntervalMs < 1000 || this.FallbackPollingIntervalMs > 60000)
            {
                errors.Add("Fallback polling interval must be between 1 and 60 seconds");
            }

            if (this.UpdateCheckIntervalDays < 1 || this.UpdateCheckIntervalDays > 365)
            {
                errors.Add("Update check interval must be between 1 and 365 days");
            }

            return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors.ToArray());
        }

        public IModel Clone()
        {
            var clone = new ApplicationSettingsModel();
            clone.CopyFrom(this);
            clone.Id = this.Id;
            clone.CreatedAt = this.CreatedAt;
            clone.UpdatedAt = this.UpdatedAt;
            return clone;
        }

        public bool HasSameUserSettingsAs(ApplicationSettingsModel? other)
        {
            if (other == null)
            {
                return false;
            }

            var currentSnapshot = (ApplicationSettingsModel)this.Clone();
            var otherSnapshot = (ApplicationSettingsModel)other.Clone();

            currentSnapshot.Id = otherSnapshot.Id;
            currentSnapshot.CreatedAt = otherSnapshot.CreatedAt;
            currentSnapshot.UpdatedAt = otherSnapshot.UpdatedAt;

            var currentJson = JsonSerializer.Serialize(currentSnapshot, UserSettingsComparisonJsonOptions);
            var otherJson = JsonSerializer.Serialize(otherSnapshot, UserSettingsComparisonJsonOptions);
            return string.Equals(currentJson, otherJson, StringComparison.Ordinal);
        }
    }

    public enum NotificationLevelProfile
    {
        All,
        WarningsAndErrorsOnly,
        Silent,
    }

    public enum NotificationPosition
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        Center,
    }

    public enum NotificationSound
    {
        None,
        Default,
        Information,
        Warning,
        Error,
        Custom,
    }

    public enum TrayIconStyle
    {
        Default,
        Monochrome,
        Colored,
        Custom,
    }
}
