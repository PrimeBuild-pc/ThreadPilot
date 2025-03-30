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
        /// <param name="message">The message to display</param>
        void ShowSuccess(string message);
        
        /// <summary>
        /// Show an error notification
        /// </summary>
        /// <param name="message">The message to display</param>
        /// <param name="detail">Optional detailed error information</param>
        void ShowError(string message, string detail = null);
    }
}