/*
 * ThreadPilot - Advanced Windows Process and Power Plan Manager
 * Copyright (C) 2025 Prime Build
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, version 3 only.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
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

    /// <summary>
    /// Service for managing notifications with balloon tips and toast support.
    /// </summary>
    public class NotificationService : INotificationService, IDisposable
    {
        private const int NotificationDisplayDurationMs = 2000;

        private readonly ILogger<NotificationService> logger;
        private readonly IApplicationSettingsService settingsService;
        private readonly ISystemTrayService systemTrayService;
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
            ISystemTrayService systemTrayService)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            this.systemTrayService = systemTrayService ?? throw new ArgumentNullException(nameof(systemTrayService));

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

            var message = string.IsNullOrEmpty(processName)
                ? $"Power plan changed from '{oldPlan}' to '{newPlan}'"
                : $"Power plan changed to '{newPlan}' for process '{processName}'";

            var notification = new NotificationModel("Power Plan Changed", message, NotificationType.PowerPlanChange)
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

            var title = isEnabled ? "Process Monitoring Enabled" : "Process Monitoring Disabled";
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
            var notification = new NotificationModel(
                "CPU Affinity Applied",
                $"CPU affinity set for '{processName}': {affinityInfo}",
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

