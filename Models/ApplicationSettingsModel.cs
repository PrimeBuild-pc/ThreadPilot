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
namespace ThreadPilot.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using CommunityToolkit.Mvvm.ComponentModel;
    using ThreadPilot.Models.Core;
    using ThreadPilot.Services;

    /// <summary>
    /// Model for application settings including notifications and tray preferences.
    /// </summary>
    public partial class ApplicationSettingsModel : ObservableObject, IModel
    {
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

        /// <summary>
        /// When true, all applied CPU masks are cleared when exiting the application
        /// (processes return to using all cores).
        /// </summary>
        [ObservableProperty]
        private bool clearMasksOnClose = true;

        [ObservableProperty]
        private bool useDarkTheme = false;

        [ObservableProperty]
        private bool hasUserThemePreference = false;

        // Monitoring Settings
        [ObservableProperty]
        private int pollingIntervalMs = 5000;

        [ObservableProperty]
        private int fallbackPollingIntervalMs = 10000;

        [ObservableProperty]
        private bool enableWmiMonitoring = true;

        [ObservableProperty]
        private bool enableFallbackPolling = true;

        // Advanced Settings
        [ObservableProperty]
        private bool enableDebugLogging = false;

        [ObservableProperty]
        private bool enablePerformanceCounters = false;

        [ObservableProperty]
        private bool hasSeenPerformanceIntro = false;

        [ObservableProperty]
        private int maxLogFileSizeMb = 10;

        [ObservableProperty]
        private int logRetentionDays = 7;

        /// <summary>
        /// Keyboard shortcuts configuration.
        /// </summary>
        [ObservableProperty]
        private List<KeyboardShortcut> keyboardShortcuts = new();

        /// <summary>
        /// Copies settings from another instance.
        /// </summary>
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

            // Monitoring Settings
            this.PollingIntervalMs = other.PollingIntervalMs;
            this.FallbackPollingIntervalMs = other.FallbackPollingIntervalMs;
            this.EnableWmiMonitoring = other.EnableWmiMonitoring;
            this.EnableFallbackPolling = other.EnableFallbackPolling;

            // Advanced Settings
            this.EnableDebugLogging = other.EnableDebugLogging;
            this.EnablePerformanceCounters = other.EnablePerformanceCounters;
            this.HasSeenPerformanceIntro = other.HasSeenPerformanceIntro;
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
    }

    /// <summary>
    /// Notification level profile options.
    /// </summary>
    public enum NotificationLevelProfile
    {
        All,
        WarningsAndErrorsOnly,
        Silent,
    }

    /// <summary>
    /// Notification position options.
    /// </summary>
    public enum NotificationPosition
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        Center,
    }

    /// <summary>
    /// Notification sound options.
    /// </summary>
    public enum NotificationSound
    {
        None,
        Default,
        Information,
        Warning,
        Error,
        Custom,
    }

    /// <summary>
    /// Tray icon style options.
    /// </summary>
    public enum TrayIconStyle
    {
        Default,
        Monochrome,
        Colored,
        Custom,
    }
}

