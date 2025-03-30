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
        /// Show a success notification
        /// </summary>
        /// <param name="message">The message to display</param>
        public void ShowSuccess(string message)
        {
            // In a real app, this would show a non-blocking toast notification
            // For now, we'll use a simple message box
            MessageBox.Show(
                message,
                "Success",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        
        /// <summary>
        /// Show an error notification
        /// </summary>
        /// <param name="message">The message to display</param>
        /// <param name="detail">Optional detailed error information</param>
        public void ShowError(string message, string detail = null)
        {
            // For errors, we'll show a message box with an error icon
            string displayMessage = message;
            
            if (!string.IsNullOrEmpty(detail))
            {
                displayMessage += Environment.NewLine + Environment.NewLine + detail;
            }
            
            MessageBox.Show(
                displayMessage,
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}