using System;
using System.Windows;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Notification service
    /// </summary>
    public class NotificationService : INotificationService
    {
        /// <summary>
        /// Notification received event
        /// </summary>
        public event EventHandler<NotificationEventArgs>? NotificationReceived;
        
        /// <summary>
        /// Show notification
        /// </summary>
        public void Show(string message, NotificationType type = NotificationType.Info)
        {
            // Raise event
            NotificationReceived?.Invoke(this, new NotificationEventArgs(type, message));
            
            // Show messagebox for errors
            if (type == NotificationType.Error)
            {
                MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// Show information notification
        /// </summary>
        public void ShowInfo(string message)
        {
            Show(message, NotificationType.Info);
        }
        
        /// <summary>
        /// Show success notification
        /// </summary>
        public void ShowSuccess(string message)
        {
            Show(message, NotificationType.Success);
        }
        
        /// <summary>
        /// Show warning notification
        /// </summary>
        public void ShowWarning(string message)
        {
            Show(message, NotificationType.Warning);
        }
        
        /// <summary>
        /// Show error notification
        /// </summary>
        public void ShowError(string message)
        {
            Show(message, NotificationType.Error);
        }
    }
}