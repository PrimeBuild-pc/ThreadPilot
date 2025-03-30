namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for displaying notifications to the user
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// Shows a success notification
        /// </summary>
        /// <param name="message">Success message</param>
        void ShowSuccess(string message);

        /// <summary>
        /// Shows an error notification
        /// </summary>
        /// <param name="message">Error message</param>
        void ShowError(string message);

        /// <summary>
        /// Shows a confirmation dialog
        /// </summary>
        /// <param name="message">Confirmation message</param>
        /// <param name="title">Dialog title</param>
        /// <returns>True if confirmed, false otherwise</returns>
        bool ShowConfirmation(string message, string title);
    }
}