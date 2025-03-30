using System;
using System.Threading.Tasks;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Interface for displaying notifications to the user
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// Shows a notification message to the user
        /// </summary>
        Task ShowAsync(string message, NotificationType type = NotificationType.Information);
        
        /// <summary>
        /// Shows a success notification message to the user
        /// </summary>
        Task ShowSuccessAsync(string message);
        
        /// <summary>
        /// Shows an error notification message to the user
        /// </summary>
        Task ShowErrorAsync(string message);
    }

    /// <summary>
    /// Notification type enumeration
    /// </summary>
    public enum NotificationType
    {
        Information,
        Success,
        Warning,
        Error
    }

    /// <summary>
    /// Service for displaying notifications to the user
    /// </summary>
    public class NotificationService : INotificationService
    {
        /// <summary>
        /// Shows a notification message to the user
        /// </summary>
        public async Task ShowAsync(string message, NotificationType type = NotificationType.Information)
        {
            // In a real application, this would display a toast notification
            // in the UI. For our demo environment, we'll just log to console.
            await Task.Delay(1); // Simulate async operation
            
            string prefix = type switch
            {
                NotificationType.Information => "[INFO]",
                NotificationType.Success => "[SUCCESS]",
                NotificationType.Warning => "[WARNING]",
                NotificationType.Error => "[ERROR]",
                _ => "[INFO]"
            };
            
            Console.WriteLine($"{prefix} {message}");
            
            // In a real application, this would trigger a UI element to show a notification
            // For example, a toast notification or a snackbar at the bottom of the screen
        }
        
        /// <summary>
        /// Shows a success notification message to the user
        /// </summary>
        public Task ShowSuccessAsync(string message)
        {
            return ShowAsync(message, NotificationType.Success);
        }
        
        /// <summary>
        /// Shows an error notification message to the user
        /// </summary>
        public Task ShowErrorAsync(string message)
        {
            return ShowAsync(message, NotificationType.Error);
        }
    }
}