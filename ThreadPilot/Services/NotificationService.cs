using System;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Notification types
    /// </summary>
    public enum NotificationType
    {
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>
    /// Service for showing notifications
    /// </summary>
    public class NotificationService
    {
        private readonly SettingsService _settingsService;
        private TaskbarIcon _taskbarIcon;

        /// <summary>
        /// Constructor for NotificationService
        /// </summary>
        /// <param name="settingsService">The settings service</param>
        public NotificationService(SettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        /// <summary>
        /// Initialize the notification service with a taskbar icon
        /// </summary>
        /// <param name="taskbarIcon">The taskbar icon to use for notifications</param>
        public void Initialize(TaskbarIcon taskbarIcon)
        {
            _taskbarIcon = taskbarIcon;
        }

        /// <summary>
        /// Show a notification
        /// </summary>
        /// <param name="title">The notification title</param>
        /// <param name="message">The notification message</param>
        /// <param name="type">The notification type</param>
        public void ShowNotification(string title, string message, NotificationType type = NotificationType.Info)
        {
            // Check if notifications are enabled for this type
            if (!ShouldShowNotification(type))
                return;

            try
            {
                if (_taskbarIcon != null)
                {
                    var baloonIcon = GetBalloonIcon(type);
                    _taskbarIcon.ShowBalloonTip(title, message, baloonIcon);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing notification: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Show a success notification
        /// </summary>
        /// <param name="message">The notification message</param>
        public void ShowSuccess(string message)
        {
            ShowNotification("Success", message, NotificationType.Success);
        }
        
        /// <summary>
        /// Show an error notification
        /// </summary>
        /// <param name="message">The notification message</param>
        public void ShowError(string message)
        {
            ShowNotification("Error", message, NotificationType.Error);
        }
        
        /// <summary>
        /// Show a warning notification
        /// </summary>
        /// <param name="message">The notification message</param>
        public void ShowWarning(string message)
        {
            ShowNotification("Warning", message, NotificationType.Warning);
        }
        
        /// <summary>
        /// Show an info notification
        /// </summary>
        /// <param name="message">The notification message</param>
        public void ShowInfo(string message)
        {
            ShowNotification("Information", message, NotificationType.Info);
        }

        private bool ShouldShowNotification(NotificationType type)
        {
            // For process-related notifications, check the setting
            if (type == NotificationType.Info || type == NotificationType.Success)
            {
                return _settingsService.ShowProcessNotifications;
            }

            // Always show warnings and errors
            return true;
        }

        private BalloonIcon GetBalloonIcon(NotificationType type)
        {
            return type switch
            {
                NotificationType.Success => BalloonIcon.Info,
                NotificationType.Warning => BalloonIcon.Warning,
                NotificationType.Error => BalloonIcon.Error,
                _ => BalloonIcon.Info
            };
        }
    }
}
