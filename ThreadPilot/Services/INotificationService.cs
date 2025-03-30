using System;
using System.Threading.Tasks;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Interface for notification services in ThreadPilot
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// Shows a notification
        /// </summary>
        /// <param name="message">The message to display</param>
        /// <param name="title">The title of the notification</param>
        /// <param name="duration">Duration in milliseconds, or null for default</param>
        void ShowNotification(string message, string title = "ThreadPilot", int? duration = null);

        /// <summary>
        /// Shows a success notification
        /// </summary>
        /// <param name="message">The message to display</param>
        /// <param name="title">The title of the notification</param>
        /// <param name="duration">Duration in milliseconds, or null for default</param>
        void ShowSuccess(string message, string title = "Success", int? duration = null);

        /// <summary>
        /// Shows an error notification
        /// </summary>
        /// <param name="message">The message to display</param>
        /// <param name="title">The title of the notification</param>
        /// <param name="duration">Duration in milliseconds, or null for default</param>
        void ShowError(string message, string title = "Error", int? duration = null);

        /// <summary>
        /// Shows a warning notification
        /// </summary>
        /// <param name="message">The message to display</param>
        /// <param name="title">The title of the notification</param>
        /// <param name="duration">Duration in milliseconds, or null for default</param>
        void ShowWarning(string message, string title = "Warning", int? duration = null);

        /// <summary>
        /// Shows a confirmation dialog
        /// </summary>
        /// <param name="message">The message to display</param>
        /// <param name="title">The title of the dialog</param>
        /// <returns>True if confirmed, otherwise false</returns>
        Task<bool> ShowConfirmation(string message, string title = "Confirm");

        /// <summary>
        /// Shows an input dialog
        /// </summary>
        /// <param name="message">The message to display</param>
        /// <param name="title">The title of the dialog</param>
        /// <param name="defaultValue">The default value</param>
        /// <returns>The input value, or null if cancelled</returns>
        Task<string> ShowInputDialog(string message, string title = "Input", string defaultValue = "");
    }
}