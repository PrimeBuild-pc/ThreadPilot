namespace ThreadPilot.Services
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    using Microsoft.Extensions.Logging;
    using ThreadPilot.Models;

    public class NotificationService : INotificationService, IDisposable
    {
        private const int NotificationDisplayDurationMs = 2000;

        private readonly ILogger<NotificationService> logger;
        private readonly IApplicationSettingsService settingsService;
        private readonly ISystemTrayService systemTrayService;
        private readonly ILocalizationService localizationService;
        private readonly List<NotificationModel> notificationHistory;
        private ApplicationSettingsModel settings;
        private bool disposed = false;

        public event EventHandler<NotificationEventArgs>? NotificationShown;

        public event EventHandler<NotificationEventArgs>? NotificationDismissed;

        public event EventHandler<NotificationActionEventArgs>? NotificationActionClicked;

        public IReadOnlyList<NotificationModel> NotificationHistory => this.notificationHistory.AsReadOnly();

        public NotificationService(
            ILogger<NotificationService> logger,
            IApplicationSettingsService settingsService,
            ISystemTrayService systemTrayService,
            ILocalizationService localizationService)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            this.systemTrayService = systemTrayService ?? throw new ArgumentNullException(nameof(systemTrayService));
            this.localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));

            this.notificationHistory = new List<NotificationModel>();
            this.settings = this.settingsService.Settings;

            // Subscribe to settings changes
            this.settingsService.SettingsChanged += this.OnSettingsChanged;
        }

        public async Task InitializeAsync()
        {
            try
            {
                this.logger.LogInformation("Initializing notification service");

                // Load settings
                this.settings = this.settingsService.Settings;

                this.logger.LogInformation("Notification service initialized successfully");
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to initialize notification service");
                throw;
            }
        }

        public async Task ShowNotificationAsync(string title, string message, NotificationType type = NotificationType.Information)
        {
            var notification = new NotificationModel(title, message, type)
            {
                DurationMs = NotificationDisplayDurationMs,
                Category = "General",
                SourceService = "NotificationService",
            };

            await this.ShowNotificationAsync(notification);
        }

        public async Task ShowNotificationAsync(NotificationModel notification)
        {
            if (notification == null)
            {
                return;
            }

            try
            {
                notification.Title = this.TryGetLocalizedNotificationString(notification.Title);
                notification.Message = this.TryGetLocalizedNotificationString(notification.Message);

                // Check if notifications are enabled
                if (!this.AreNotificationsEnabled(notification.Type))
                {
                    this.logger.LogDebug("Notifications disabled for type {Type}", notification.Type);
                    return;
                }

                // Add to history
                notification.DurationMs = NotificationDisplayDurationMs;
                this.AddToHistory(notification);

                // Show balloon tip if enabled
                if (this.settings.EnableBalloonNotifications)
                {
                    await this.ShowBalloonTipInternalAsync(notification);
                }

                // Show toast notification if enabled and available
                if (this.settings.EnableToastNotifications)
                {
                    await this.ShowToastNotificationInternalAsync(notification);
                }

                // Fire event
                this.NotificationShown?.Invoke(this, new NotificationEventArgs(notification));

                this.logger.LogDebug("Notification shown: {Title}", notification.Title);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error showing notification: {Title}", notification.Title);
            }
        }

        public async Task ShowBalloonTipAsync(string title, string message, NotificationType type = NotificationType.Information, int timeoutMs = 3000)
        {
            var notification = new NotificationModel(title, message, type)
            {
                DurationMs = NotificationDisplayDurationMs,
                Category = "BalloonTip",
                SourceService = "NotificationService",
            };

            if (this.settings.EnableBalloonNotifications && this.AreNotificationsEnabled(type))
            {
                this.AddToHistory(notification);
                await this.ShowBalloonTipInternalAsync(notification);
                this.NotificationShown?.Invoke(this, new NotificationEventArgs(notification));
            }
        }

        public async Task ShowToastNotificationAsync(string title, string message, NotificationType type = NotificationType.Information)
        {
            var notification = new NotificationModel(title, message, type)
            {
                Category = "Toast",
                SourceService = "NotificationService",
            };

            if (this.settings.EnableToastNotifications && this.AreNotificationsEnabled(type))
            {
                this.AddToHistory(notification);
                await this.ShowToastNotificationInternalAsync(notification);
                this.NotificationShown?.Invoke(this, new NotificationEventArgs(notification));
            }
        }

        public async Task ShowPowerPlanChangeNotificationAsync(string oldPlan, string newPlan, string processName = "")
        {
            if (!this.settings.EnablePowerPlanChangeNotifications)
            {
                return;
            }

            var title = this.GetLocalizedString("Notification_PowerPlanChangedTitle");
            var message = string.IsNullOrEmpty(processName)
                ? string.Format(
                    this.GetLocalizedString("Notification_PowerPlanChangedFormat"),
                    oldPlan,
                    newPlan)
                : string.Format(
                    this.GetLocalizedString("Notification_PowerPlanChangedProcessFormat"),
                    newPlan,
                    processName);

            var notification = new NotificationModel(title, message, NotificationType.PowerPlanChange)
            {
                Category = "PowerPlan",
                SourceService = "PowerPlanService",
                Priority = NotificationPriority.Normal,
            };

            await this.ShowNotificationAsync(notification);
        }

        public async Task ShowProcessMonitoringNotificationAsync(string message, bool isEnabled)
        {
            if (!this.settings.EnableProcessMonitoringNotifications)
            {
                return;
            }

            var title = isEnabled
                ? this.GetLocalizedString("Notification_ProcessMonitoringEnabled")
                : this.GetLocalizedString("Notification_ProcessMonitoringDisabled");
            var type = isEnabled ? NotificationType.Success : NotificationType.Warning;

            var notification = new NotificationModel(title, message, type)
            {
                Category = "ProcessMonitoring",
                SourceService = "ProcessMonitorService",
                Priority = NotificationPriority.Normal,
            };

            await this.ShowNotificationAsync(notification);
        }

        public async Task ShowCpuAffinityNotificationAsync(string processName, string affinityInfo)
        {
            var title = this.GetLocalizedString("Notification_CpuAffinityAppliedTitle");
            var message = string.Format(
                this.GetLocalizedString("Notification_CpuAffinityAppliedFormat"),
                processName,
                affinityInfo);

            var notification = new NotificationModel(
                title,
                message,
                NotificationType.CpuAffinity)
            {
                Category = "CpuAffinity",
                SourceService = "ProcessService",
                Priority = NotificationPriority.Normal,
            };

            await this.ShowNotificationAsync(notification);
        }

        public async Task ShowErrorNotificationAsync(string title, string message, Exception? exception = null)
        {
            if (!this.settings.EnableErrorNotifications)
            {
                return;
            }

            var fullMessage = exception != null ? $"{message}\n\nError: {exception.Message}" : message;

            var notification = new NotificationModel(title, fullMessage, NotificationType.Error)
            {
                Category = "Error",
                SourceService = "System",
                Priority = NotificationPriority.High,
                IsPersistent = true,
            };

            await this.ShowNotificationAsync(notification);
        }

        public async Task ShowSuccessNotificationAsync(string title, string message)
        {
            if (!this.settings.EnableSuccessNotifications)
            {
                return;
            }

            var notification = new NotificationModel(title, message, NotificationType.Success)
            {
                Category = "Success",
                SourceService = "System",
                Priority = NotificationPriority.Normal,
            };

            await this.ShowNotificationAsync(notification);
        }

        public async Task DismissNotificationAsync(string notificationId)
        {
            var notification = this.notificationHistory.FirstOrDefault(n => n.Id == notificationId);
            if (notification != null)
            {
                this.NotificationDismissed?.Invoke(this, new NotificationEventArgs(notification));
                this.logger.LogDebug("Notification dismissed: {Id}", notificationId);
            }
            await Task.CompletedTask;
        }

        public async Task DismissAllNotificationsAsync()
        {
            foreach (var notification in this.notificationHistory.ToList())
            {
                this.NotificationDismissed?.Invoke(this, new NotificationEventArgs(notification));
            }
            this.logger.LogDebug("All notifications dismissed");
            await Task.CompletedTask;
        }

        public async Task ClearNotificationHistoryAsync()
        {
            this.notificationHistory.Clear();
            this.logger.LogInformation("Notification history cleared");
            await Task.CompletedTask;
        }

        public int GetUnreadNotificationCount()
        {
            return this.notificationHistory.Count(n => !n.IsRead);
        }

        public async Task MarkAllNotificationsAsReadAsync()
        {
            foreach (var notification in this.notificationHistory)
            {
                notification.MarkAsRead();
            }
            this.logger.LogDebug("All notifications marked as read");
            await Task.CompletedTask;
        }

        public bool AreNotificationsEnabled(NotificationType type)
        {
            if (!this.settings.EnableNotifications)
            {
                return false;
            }

            if (this.settings.NotificationLevel == NotificationLevelProfile.Silent)
            {
                return false;
            }

            if (this.settings.NotificationLevel == NotificationLevelProfile.WarningsAndErrorsOnly &&
                type != NotificationType.Warning &&
                type != NotificationType.Error)
            {
                return false;
            }

            return type switch
            {
                NotificationType.PowerPlanChange => this.settings.EnablePowerPlanChangeNotifications,
                NotificationType.ProcessMonitoring => this.settings.EnableProcessMonitoringNotifications,
                NotificationType.Error => this.settings.EnableErrorNotifications,
                NotificationType.Success => this.settings.EnableSuccessNotifications,
                _ => true,
            };
        }

        public void UpdateSettings(ApplicationSettingsModel settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.logger.LogDebug("Notification settings updated");
        }

        private void AddToHistory(NotificationModel notification)
        {
            if (!this.settings.EnableNotificationHistory)
            {
                return;
            }

            this.notificationHistory.Insert(0, notification);

            // Trim history if it exceeds max items
            while (this.notificationHistory.Count > this.settings.MaxNotificationHistoryItems)
            {
                this.notificationHistory.RemoveAt(this.notificationHistory.Count - 1);
            }
        }

        private async Task ShowBalloonTipInternalAsync(NotificationModel notification)
        {
            try
            {
                // Use the system tray service to show the actual balloon tip
                this.systemTrayService.ShowTrayNotification(
                    notification.Title,
                    notification.Message,
                    notification.Type,
                    notification.DurationMs);

                this.logger.LogDebug("Balloon tip shown via system tray: {Title} - {Message}", notification.Title, notification.Message);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error showing balloon tip");
            }
        }

        private async Task ShowToastNotificationInternalAsync(NotificationModel notification)
        {
            try
            {
                // Toast notifications would require Windows 10+ and additional setup
                // For now, we'll just log it
                this.logger.LogDebug("Toast notification: {Title} - {Message}", notification.Title, notification.Message);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error showing toast notification");
            }
        }

        private void OnSettingsChanged(object? sender, ApplicationSettingsChangedEventArgs e)
        {
            this.UpdateSettings(e.NewSettings);
        }

        private string GetLocalizedString(string key)
        {
            var localized = this.localizationService.GetString(key);
            return string.IsNullOrEmpty(localized) ? key : localized;
        }

        private string TryGetLocalizedNotificationString(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            var key = input switch
            {
                "Game Boost Activated" => "Notification_GameBoostActivatedTitle",
                "Game Boost Deactivated" => "Notification_GameBoostDeactivatedTitle",
                "Process Monitor Error" => "Notification_ProcessMonitorErrorTitle",
                "Affinity blocked" => "Notification_AffinityBlockedTitle",
                "Affinity applied" => "Notification_AffinityAppliedTitle",
                "Affinity adjusted" => "Notification_AffinityAdjustedTitle",
                "Affinity failed" => "Notification_AffinityFailedTitle",
                "Affinity error" => "Notification_AffinityErrorTitle",
                "Priority blocked" => "Notification_PriorityBlockedTitle",
                "Priority warning" => "Notification_PriorityWarningTitle",
                "Priority applied" => "Notification_PriorityAppliedTitle",
                "Priority adjusted" => "Notification_PriorityAdjustedTitle",
                "Priority error" => "Notification_PriorityErrorTitle",
                "Keyboard Shortcut" => "Notification_KeyboardShortcutTitle",
                "ThreadPilot Started" => "Notification_ThreadPilotStartedTitle",
                "Startup Error" => "Notification_StartupErrorTitle",
                "Automation Monitoring Error" => "Notification_AutomationMonitoringErrorTitle",
                "Settings Saved" => "Notification_SettingsSavedTitle",
                "Settings Saved with Warnings" => "Notification_SettingsSavedWarningsTitle",
                "Settings Error" => "Notification_SettingsErrorTitle",
                "Test Notification" => "SettingsView_TestNotification",
                _ => null,
            };

            if (key != null)
            {
                var localized = this.GetLocalizedString(key);
                if (!string.Equals(localized, key, StringComparison.Ordinal))
                {
                    return localized;
                }
            }

            const string GameBoostActivatedPrefix = "Game Boost mode activated for ";
            if (input.StartsWith(GameBoostActivatedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var processName = input[GameBoostActivatedPrefix.Length..];
                var format = this.GetLocalizedString("Notification_GameBoostActivatedFormat");
                if (!string.Equals(format, "Notification_GameBoostActivatedFormat", StringComparison.Ordinal))
                {
                    return string.Format(format, processName);
                }
            }

            const string GameBoostDeactivatedPrefix = "Game Boost mode deactivated after ";
            if (input.StartsWith(GameBoostDeactivatedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var duration = input[GameBoostDeactivatedPrefix.Length..];
                var format = this.GetLocalizedString("Notification_GameBoostDeactivatedFormat");
                if (!string.Equals(format, "Notification_GameBoostDeactivatedFormat", StringComparison.Ordinal))
                {
                    return string.Format(format, duration);
                }
            }

            key = input switch
            {
                "Toggle monitoring shortcut activated" => "Notification_ToggleMonitoringShortcut",
                "High Performance power plan shortcut activated" => "Notification_HighPerformanceShortcut",
                "Refresh process list shortcut activated" => "Notification_RefreshProcessListShortcut",
                "Process monitoring and power plan management is now active" => "Notification_ThreadPilotStartedMessage",
                "Failed to start process monitoring manager" => "Notification_ProcessMonitoringStartFailed",
                "Application settings have been saved successfully" => "Notification_SettingsSavedMessage",
                "Failed to save settings" => "Notification_SettingsSaveFailed",
                "This is a test notification to verify your settings are working correctly." => "Notification_TestNotificationMessage",
                _ => null,
            };

            if (key != null)
            {
                var localized = this.GetLocalizedString(key);
                if (!string.Equals(localized, key, StringComparison.Ordinal))
                {
                    return localized;
                }
            }

            return input;
        }

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            try
            {
                this.settingsService.SettingsChanged -= this.OnSettingsChanged;
                this.disposed = true;
                this.logger.LogInformation("Notification service disposed");
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error disposing notification service");
            }
        }
    }
}
