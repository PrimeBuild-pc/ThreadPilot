using System;
using System.Windows;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Implementation of the notification service
    /// </summary>
    public class NotificationService : INotificationService
    {
        /// <summary>
        /// Event that is fired when the status message is updated
        /// </summary>
        public event EventHandler<string>? StatusMessageUpdated;
        
        /// <summary>
        /// Show a success notification
        /// </summary>
        public void ShowSuccess(string message)
        {
            OnStatusMessageUpdated($"✓ {message}");
            MessageBox.Show(message, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
        /// <summary>
        /// Show an error notification
        /// </summary>
        public void ShowError(string message)
        {
            OnStatusMessageUpdated($"✕ Error: {message}");
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        
        /// <summary>
        /// Show a warning notification
        /// </summary>
        public void ShowWarning(string message)
        {
            OnStatusMessageUpdated($"⚠ Warning: {message}");
            MessageBox.Show(message, "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        
        /// <summary>
        /// Show an information notification
        /// </summary>
        public void ShowInfo(string message)
        {
            OnStatusMessageUpdated($"ℹ {message}");
            MessageBox.Show(message, "Information", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
        /// <summary>
        /// Raise the StatusMessageUpdated event
        /// </summary>
        protected virtual void OnStatusMessageUpdated(string message)
        {
            StatusMessageUpdated?.Invoke(this, message);
        }
    }
}