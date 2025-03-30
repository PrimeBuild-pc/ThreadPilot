using System;
using System.Threading.Tasks;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Interface for the notification service.
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// Shows an information notification.
        /// </summary>
        /// <param name="title">The notification title.</param>
        /// <param name="message">The notification message.</param>
        /// <param name="durationInSeconds">The notification duration in seconds (0 = no timeout).</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ShowInfoAsync(string title, string message, int durationInSeconds = 3);

        /// <summary>
        /// Shows a success notification.
        /// </summary>
        /// <param name="title">The notification title.</param>
        /// <param name="message">The notification message.</param>
        /// <param name="durationInSeconds">The notification duration in seconds (0 = no timeout).</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ShowSuccessAsync(string title, string message, int durationInSeconds = 3);

        /// <summary>
        /// Shows a warning notification.
        /// </summary>
        /// <param name="title">The notification title.</param>
        /// <param name="message">The notification message.</param>
        /// <param name="durationInSeconds">The notification duration in seconds (0 = no timeout).</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ShowWarningAsync(string title, string message, int durationInSeconds = 5);

        /// <summary>
        /// Shows an error notification.
        /// </summary>
        /// <param name="title">The notification title.</param>
        /// <param name="message">The notification message.</param>
        /// <param name="durationInSeconds">The notification duration in seconds (0 = no timeout).</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ShowErrorAsync(string title, string message, int durationInSeconds = 5);

        /// <summary>
        /// Shows a notification with a custom icon.
        /// </summary>
        /// <param name="title">The notification title.</param>
        /// <param name="message">The notification message.</param>
        /// <param name="iconPath">The path to the icon.</param>
        /// <param name="durationInSeconds">The notification duration in seconds (0 = no timeout).</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ShowCustomAsync(string title, string message, string iconPath, int durationInSeconds = 3);

        /// <summary>
        /// Shows a confirmation dialog.
        /// </summary>
        /// <param name="title">The dialog title.</param>
        /// <param name="message">The dialog message.</param>
        /// <param name="okButtonText">The OK button text.</param>
        /// <param name="cancelButtonText">The Cancel button text.</param>
        /// <returns>True if the user clicked OK, false otherwise.</returns>
        Task<bool> ShowConfirmationAsync(string title, string message, string okButtonText = "OK", string cancelButtonText = "Cancel");

        /// <summary>
        /// Shows an input dialog.
        /// </summary>
        /// <param name="title">The dialog title.</param>
        /// <param name="message">The dialog message.</param>
        /// <param name="defaultValue">The default input value.</param>
        /// <param name="okButtonText">The OK button text.</param>
        /// <param name="cancelButtonText">The Cancel button text.</param>
        /// <returns>The user input, or null if the user cancelled.</returns>
        Task<string?> ShowInputAsync(string title, string message, string defaultValue = "", string okButtonText = "OK", string cancelButtonText = "Cancel");

        /// <summary>
        /// Shows a toast notification in the Windows notification area.
        /// </summary>
        /// <param name="title">The notification title.</param>
        /// <param name="message">The notification message.</param>
        /// <param name="silent">A value indicating whether the notification should be silent.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ShowToastAsync(string title, string message, bool silent = false);

        /// <summary>
        /// Clears all active notifications.
        /// </summary>
        void ClearAll();

        /// <summary>
        /// Event that is raised when a notification is shown.
        /// </summary>
        event EventHandler<NotificationEventArgs>? NotificationShown;

        /// <summary>
        /// Event that is raised when a notification is closed.
        /// </summary>
        event EventHandler<NotificationEventArgs>? NotificationClosed;
    }

    /// <summary>
    /// Represents notification event arguments.
    /// </summary>
    public class NotificationEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the notification ID.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets the notification title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the notification message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the notification type.
        /// </summary>
        public NotificationType Type { get; set; }

        /// <summary>
        /// Gets or sets the notification timestamp.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Represents the notification type.
    /// </summary>
    public enum NotificationType
    {
        /// <summary>
        /// Information notification.
        /// </summary>
        Info,

        /// <summary>
        /// Success notification.
        /// </summary>
        Success,

        /// <summary>
        /// Warning notification.
        /// </summary>
        Warning,

        /// <summary>
        /// Error notification.
        /// </summary>
        Error,

        /// <summary>
        /// Custom notification.
        /// </summary>
        Custom
    }
}