using System;
using System.Windows;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for showing notifications to the user
    /// </summary>
    public class NotificationService : INotificationService
    {
        /// <summary>
        /// Show a success notification
        /// </summary>
        /// <param name="message">Notification message</param>
        public void ShowSuccess(string message)
        {
            try
            {
                // TODO: Implement a more elegant notification system
                MessageBox.Show(message, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error showing success notification: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Show an error notification
        /// </summary>
        /// <param name="title">Error title</param>
        /// <param name="message">Error message</param>
        public void ShowError(string title, string message)
        {
            try
            {
                // TODO: Implement a more elegant notification system
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error showing error notification: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Show a warning notification
        /// </summary>
        /// <param name="title">Warning title</param>
        /// <param name="message">Warning message</param>
        public void ShowWarning(string title, string message)
        {
            try
            {
                // TODO: Implement a more elegant notification system
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error showing warning notification: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Show an information notification
        /// </summary>
        /// <param name="title">Information title</param>
        /// <param name="message">Information message</param>
        public void ShowInfo(string title, string message)
        {
            try
            {
                // TODO: Implement a more elegant notification system
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error showing info notification: {ex.Message}");
            }
        }
    }
}