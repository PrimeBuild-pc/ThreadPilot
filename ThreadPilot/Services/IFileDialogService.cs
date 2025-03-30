namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for interacting with file dialogs
    /// </summary>
    public interface IFileDialogService
    {
        /// <summary>
        /// Show an open file dialog
        /// </summary>
        /// <param name="title">The dialog title</param>
        /// <param name="filter">The file filter (e.g., "Text files|*.txt")</param>
        /// <returns>The selected file path, or null if canceled</returns>
        string? ShowOpenFileDialog(string title, string filter);
        
        /// <summary>
        /// Show a save file dialog
        /// </summary>
        /// <param name="title">The dialog title</param>
        /// <param name="filter">The file filter (e.g., "Text files|*.txt")</param>
        /// <param name="defaultFileName">The default file name</param>
        /// <returns>The selected file path, or null if canceled</returns>
        string? ShowSaveFileDialog(string title, string filter, string defaultFileName = "");
    }
}