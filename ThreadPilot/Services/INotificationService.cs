using System;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Notification service interface
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// Show success notification
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="title">Title</param>
        void ShowSuccess(string message, string title = "Success");
        
        /// <summary>
        /// Show error notification
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="title">Title</param>
        void ShowError(string message, string title = "Error");
        
        /// <summary>
        /// Show information notification
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="title">Title</param>
        void ShowInformation(string message, string title = "Information");
        
        /// <summary>
        /// Show warning notification
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="title">Title</param>
        void ShowWarning(string message, string title = "Warning");
        
        /// <summary>
        /// Show confirmation dialog
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="title">Title</param>
        /// <returns>True if confirmed, false otherwise</returns>
        bool ShowConfirmation(string message, string title = "Confirmation");
    }
}