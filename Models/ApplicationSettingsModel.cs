namespace ThreadPilot.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Text.Json;
    using CommunityToolkit.Mvvm.ComponentModel;
    using ThreadPilot.Models.Core;
    using ThreadPilot.Services;

    public partial class ApplicationSettingsModel : ObservableObject
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
        private int notificationDisplayDurationMs = 3000;

        [ObservableProperty]
        private int balloonNotificationTimeoutMs = 5000;

        [ObservableProperty]
        private string customTrayIconPath = string.Empty;

        [ObservableProperty]
        private bool useCustomTrayIcon = false;

        [ObservableProperty]
        private bool enableNotificationHistory = true;

        [ObservableProperty]
        private int maxNotificationHistoryItems = 50;

        // Autostart Settings. Off for new installs since 1.7.11: the logon task and the UAC-free launch task are
        // created only after the user turns this on. Profiles saved before then keep their value.
        [ObservableProperty]
        private bool autostartWithWindows = false;

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

        [ObservableProperty]
        private bool enableAutomationMonitoring = true;

        [ObservableProperty]
        private CpuAssignmentMode defaultCpuAssignmentMode = CpuAssignmentMode.AffinityMask;

        // Set on a profile that has been through CpuAssignmentModeMigrationPolicy, so the one-time
        // move off Automatic never runs twice and never overrides a later deliberate choice.
        [ObservableProperty]
        private bool hasMigratedCpuAssignmentModeDefault;

        [ObservableProperty]
        private bool hasSeenCpuAssignmentModeChangeNotice;

        // Separate from the profile flag above: profiles were migrated in 1.7.2, saved rules
        // were not, so a shared flag would skip the rules of everyone already on 1.7.2.
        [ObservableProperty]
        private bool hasMigratedPersistentRuleCpuAssignmentModes;

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

        // True for profiles saved before the notice existed (their autostart choice was already made);
        // a brand-new profile clears it in CreateDefaultSettings so the notice is shown once.
        [ObservableProperty]
        private bool hasSeenAutostartOffNotice = true;

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

        // Game Boost
        [ObservableProperty]
        private bool gameBoostEnabled = false;

        [ObservableProperty]
        private GameBoostMode gameBoostMode = GameBoostMode.Automatic;

        [ObservableProperty]
        private bool gameBoostAboveNormalPriority = false;

        [ObservableProperty]
        private List<string> gameBoostManualGames = new();

        [ObservableProperty]
        private List<string> gameBoostExcludedGames = new();

        [ObservableProperty]
        private List<string> gameBoostBackgroundProcesses = new(GameBoostProcessLists.DefaultBackgroundProcesses);

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
            this.NotificationDisplayDurationMs = other.NotificationDisplayDurationMs;
            this.BalloonNotificationTimeoutMs = other.BalloonNotificationTimeoutMs;
            this.CustomTrayIconPath = other.CustomTrayIconPath;
            this.UseCustomTrayIcon = other.UseCustomTrayIcon;
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
            this.IncludePrereleaseUpdates = other.IncludePrereleaseUpdates;

            // Monitoring Settings
            this.PollingIntervalMs = other.PollingIntervalMs;
            this.FallbackPollingIntervalMs = other.FallbackPollingIntervalMs;
            this.EnableWmiMonitoring = other.EnableWmiMonitoring;
            this.EnableFallbackPolling = other.EnableFallbackPolling;
            this.ApplyPersistentRulesOnProcessStart = other.ApplyPersistentRulesOnProcessStart;
            this.EnableAutomationMonitoring = other.EnableAutomationMonitoring;
            // Repair to the shipped default, not to Automatic: a corrupt value is not a choice the
            // user made, and treating it as one would tell them their Automatic setting was changed.
            this.DefaultCpuAssignmentMode = Enum.IsDefined(other.DefaultCpuAssignmentMode)
                ? other.DefaultCpuAssignmentMode
                : CpuAssignmentMode.AffinityMask;

            // Advanced Settings
            this.EnableDebugLogging = other.EnableDebugLogging;
            this.EnablePerformanceCounters = other.EnablePerformanceCounters;
            this.HasSeenPerformanceIntro = other.HasSeenPerformanceIntro;
            this.HasSeenElevationWarning = other.HasSeenElevationWarning;
            this.HasSeenStartupMinimizedSuggestion = other.HasSeenStartupMinimizedSuggestion;
            this.HasSeenAutostartOffNotice = other.HasSeenAutostartOffNotice;
            this.HasMigratedCpuAssignmentModeDefault = other.HasMigratedCpuAssignmentModeDefault;
            this.HasSeenCpuAssignmentModeChangeNotice = other.HasSeenCpuAssignmentModeChangeNotice;
            this.HasMigratedPersistentRuleCpuAssignmentModes = other.HasMigratedPersistentRuleCpuAssignmentModes;
            this.EnableSelfLowImpactMode = other.EnableSelfLowImpactMode;
            this.EnableSelfAffinityLimit = other.EnableSelfAffinityLimit;
            this.MaxLogFileSizeMb = other.MaxLogFileSizeMb;
            this.LogRetentionDays = other.LogRetentionDays;

            // Keyboard Shortcuts
            this.KeyboardShortcuts = other.KeyboardShortcuts?
                .Where(shortcut => shortcut != null)
                .Select(shortcut => shortcut.Clone())
                .ToList() ?? [];

            // Game Boost
            this.GameBoostEnabled = other.GameBoostEnabled;
            this.GameBoostMode = Enum.IsDefined(other.GameBoostMode) ? other.GameBoostMode : GameBoostMode.Automatic;
            this.GameBoostAboveNormalPriority = other.GameBoostAboveNormalPriority;
            this.GameBoostManualGames = new List<string>(other.GameBoostManualGames ?? new());
            this.GameBoostExcludedGames = new List<string>(other.GameBoostExcludedGames ?? new());
            this.GameBoostBackgroundProcesses = new List<string>(other.GameBoostBackgroundProcesses ?? new());
        }

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

            if (!Enum.IsDefined(this.DefaultCpuAssignmentMode))
            {
                errors.Add("Default CPU assignment mode is invalid");
            }

            return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors.ToArray());
        }

        public ApplicationSettingsModel Clone()
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

            var currentSnapshot = this.Clone();
            var otherSnapshot = other.Clone();

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
}
