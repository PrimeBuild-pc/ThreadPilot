namespace ThreadPilot.Services
{
    /// <summary>
    /// Interface for the file dialog service
    /// </summary>
    public interface IFileDialogService
    {
        /// <summary>
        /// Show an open file dialog
        /// </summary>
        string ShowOpenFileDialog(string title, string filter);
        
        /// <summary>
        /// Show a save file dialog
        /// </summary>
        string ShowSaveFileDialog(string title, string filter, string? defaultFileName = null);
        
        /// <summary>
        /// Show a folder browser dialog
        /// </summary>
        string ShowFolderBrowserDialog(string description);
    }
}