namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for displaying notifications to the user
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// Show a success notification message
        /// </summary>
        /// <param name="message">Message to display</param>
        void ShowSuccess(string message);
        
        /// <summary>
        /// Show an error notification message
        /// </summary>
        /// <param name="message">Message to display</param>
        void ShowError(string message);
        
        /// <summary>
        /// Show an information notification message
        /// </summary>
        /// <param name="message">Message to display</param>
        void ShowInfo(string message);
        
        /// <summary>
        /// Show a warning notification message
        /// </summary>
        /// <param name="message">Message to display</param>
        void ShowWarning(string message);
    }
}