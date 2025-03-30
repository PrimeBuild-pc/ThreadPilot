namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for displaying notifications to the user
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// Show a success notification
        /// </summary>
        /// <param name="message">Message to display</param>
        /// <param name="title">Notification title</param>
        void ShowSuccess(string message, string title = "Success");
        
        /// <summary>
        /// Show an error notification
        /// </summary>
        /// <param name="message">Message to display</param>
        /// <param name="title">Notification title</param>
        void ShowError(string message, string title = "Error");
        
        /// <summary>
        /// Show an information notification
        /// </summary>
        /// <param name="message">Message to display</param>
        /// <param name="title">Notification title</param>
        void ShowInfo(string message, string title = "Information");
        
        /// <summary>
        /// Show a warning notification
        /// </summary>
        /// <param name="message">Message to display</param>
        /// <param name="title">Notification title</param>
        void ShowWarning(string message, string title = "Warning");
    }
}