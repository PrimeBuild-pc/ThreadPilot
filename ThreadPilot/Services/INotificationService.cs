using System;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Interface for notification operations
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// Shows a success notification
        /// </summary>
        /// <param name="message">The notification message</param>
        /// <param name="title">The notification title</param>
        void ShowSuccess(string message, string title = "Success");
        
        /// <summary>
        /// Shows an error notification
        /// </summary>
        /// <param name="message">The notification message</param>
        /// <param name="title">The notification title</param>
        void ShowError(string message, string title = "Error");
        
        /// <summary>
        /// Shows a warning notification
        /// </summary>
        /// <param name="message">The notification message</param>
        /// <param name="title">The notification title</param>
        void ShowWarning(string message, string title = "Warning");
        
        /// <summary>
        /// Shows an information notification
        /// </summary>
        /// <param name="message">The notification message</param>
        /// <param name="title">The notification title</param>
        void ShowInfo(string message, string title = "Information");
        
        /// <summary>
        /// Shows a confirmation dialog
        /// </summary>
        /// <param name="message">The dialog message</param>
        /// <param name="title">The dialog title</param>
        /// <returns>True if confirmed, false otherwise</returns>
        bool ShowConfirmation(string message, string title = "Confirmation");
        
        /// <summary>
        /// Shows a custom dialog
        /// </summary>
        /// <param name="content">The dialog content</param>
        /// <param name="title">The dialog title</param>
        /// <returns>True if confirmed, false otherwise</returns>
        bool ShowDialog(object content, string title);
        
        /// <summary>
        /// Shows a toast notification
        /// </summary>
        /// <param name="message">The toast message</param>
        /// <param name="duration">The duration in milliseconds</param>
        void ShowToast(string message, int duration = 3000);
    }
}