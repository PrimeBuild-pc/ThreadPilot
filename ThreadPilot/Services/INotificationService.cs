using System;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Interface for the notification service
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// Event that is fired when the status message is updated
        /// </summary>
        event EventHandler<string> StatusMessageUpdated;
        
        /// <summary>
        /// Show a success notification
        /// </summary>
        void ShowSuccess(string message);
        
        /// <summary>
        /// Show an error notification
        /// </summary>
        void ShowError(string message);
        
        /// <summary>
        /// Show a warning notification
        /// </summary>
        void ShowWarning(string message);
        
        /// <summary>
        /// Show an information notification
        /// </summary>
        void ShowInfo(string message);
    }
}