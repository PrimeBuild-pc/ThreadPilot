using System.Windows;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Default implementation of the notification service
    /// </summary>
    public class NotificationService : INotificationService
    {
        /// <summary>
        /// Show a success notification
        /// </summary>
        /// <param name="message">Message to display</param>
        /// <param name="title">Notification title</param>
        public void ShowSuccess(string message, string title = "Success")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
        /// <summary>
        /// Show an error notification
        /// </summary>
        /// <param name="message">Message to display</param>
        /// <param name="title">Notification title</param>
        public void ShowError(string message, string title = "Error")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        
        /// <summary>
        /// Show an information notification
        /// </summary>
        /// <param name="message">Message to display</param>
        /// <param name="title">Notification title</param>
        public void ShowInfo(string message, string title = "Information")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
        /// <summary>
        /// Show a warning notification
        /// </summary>
        /// <param name="message">Message to display</param>
        /// <param name="title">Notification title</param>
        public void ShowWarning(string message, string title = "Warning")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}