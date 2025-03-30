using System.Windows;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Implementation of notification service
    /// </summary>
    public class NotificationService : INotificationService
    {
        /// <summary>
        /// Show an information message
        /// </summary>
        /// <param name="message">Message to show</param>
        public void ShowInfo(string message)
        {
            MessageBox.Show(
                message,
                "Information",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        
        /// <summary>
        /// Show a success message
        /// </summary>
        /// <param name="message">Message to show</param>
        public void ShowSuccess(string message)
        {
            MessageBox.Show(
                message,
                "Success",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        
        /// <summary>
        /// Show a warning message
        /// </summary>
        /// <param name="message">Message to show</param>
        public void ShowWarning(string message)
        {
            MessageBox.Show(
                message,
                "Warning",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        
        /// <summary>
        /// Show an error message
        /// </summary>
        /// <param name="message">Message to show</param>
        public void ShowError(string message)
        {
            MessageBox.Show(
                message,
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}