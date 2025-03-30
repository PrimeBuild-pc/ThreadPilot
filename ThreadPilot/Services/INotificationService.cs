using System;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Notification event arguments
    /// </summary>
    public class NotificationEventArgs : EventArgs
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public NotificationEventArgs(NotificationType type, string message)
        {
            Type = type;
            Message = message;
        }
        
        /// <summary>
        /// Notification type
        /// </summary>
        public NotificationType Type { get; }
        
        /// <summary>
        /// Message
        /// </summary>
        public string Message { get; }
    }
    
    /// <summary>
    /// Notification type
    /// </summary>
    public enum NotificationType
    {
        /// <summary>
        /// Information
        /// </summary>
        Info,
        
        /// <summary>
        /// Success
        /// </summary>
        Success,
        
        /// <summary>
        /// Warning
        /// </summary>
        Warning,
        
        /// <summary>
        /// Error
        /// </summary>
        Error
    }
    
    /// <summary>
    /// Notification service interface
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// Notification received event
        /// </summary>
        event EventHandler<NotificationEventArgs> NotificationReceived;
        
        /// <summary>
        /// Show notification
        /// </summary>
        void Show(string message, NotificationType type = NotificationType.Info);
        
        /// <summary>
        /// Show information notification
        /// </summary>
        void ShowInfo(string message);
        
        /// <summary>
        /// Show success notification
        /// </summary>
        void ShowSuccess(string message);
        
        /// <summary>
        /// Show warning notification
        /// </summary>
        void ShowWarning(string message);
        
        /// <summary>
        /// Show error notification
        /// </summary>
        void ShowError(string message);
    }
}