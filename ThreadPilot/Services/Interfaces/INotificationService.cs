namespace ThreadPilot.Services
{
    /// <summary>
    /// Interface for notification service
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// Show an information message
        /// </summary>
        /// <param name="message">Message to show</param>
        void ShowInfo(string message);
        
        /// <summary>
        /// Show a success message
        /// </summary>
        /// <param name="message">Message to show</param>
        void ShowSuccess(string message);
        
        /// <summary>
        /// Show a warning message
        /// </summary>
        /// <param name="message">Message to show</param>
        void ShowWarning(string message);
        
        /// <summary>
        /// Show an error message
        /// </summary>
        /// <param name="message">Message to show</param>
        void ShowError(string message);
    }
}