using System.Windows;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Implementation of the notification service interface
    /// </summary>
    public class NotificationService : INotificationService
    {
        /// <summary>
        /// Show an error notification message
        /// </summary>
        /// <param name="message">Message to display</param>
        public void ShowError(string message)
        {
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        /// <summary>
        /// Show an information notification message
        /// </summary>
        /// <param name="message">Message to display</param>
        public void ShowInfo(string message)
        {
            MessageBox.Show(message, "Information", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Show a success notification message
        /// </summary>
        /// <param name="message">Message to display</param>
        public void ShowSuccess(string message)
        {
            MessageBox.Show(message, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Show a warning notification message
        /// </summary>
        /// <param name="message">Message to display</param>
        public void ShowWarning(string message)
        {
            MessageBox.Show(message, "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}