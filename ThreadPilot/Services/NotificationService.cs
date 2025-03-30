using System;
using System.Windows;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Implementation of notification service using MessageBox
    /// </summary>
    public class NotificationService : INotificationService
    {
        /// <summary>
        /// Shows success notification
        /// </summary>
        /// <param name="message">Message</param>
        public void ShowSuccess(string message)
        {
            MessageBox.Show(message, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
        /// <summary>
        /// Shows information notification
        /// </summary>
        /// <param name="message">Message</param>
        public void ShowInformation(string message)
        {
            MessageBox.Show(message, "Information", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
        /// <summary>
        /// Shows warning notification
        /// </summary>
        /// <param name="message">Message</param>
        public void ShowWarning(string message)
        {
            MessageBox.Show(message, "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        
        /// <summary>
        /// Shows error notification
        /// </summary>
        /// <param name="message">Message</param>
        public void ShowError(string message)
        {
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        
        /// <summary>
        /// Shows confirmation dialog
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="title">Title</param>
        /// <returns>True if confirmed, false otherwise</returns>
        public bool ShowConfirmation(string message, string title = "Confirmation")
        {
            var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            return result == MessageBoxResult.Yes;
        }
    }
}