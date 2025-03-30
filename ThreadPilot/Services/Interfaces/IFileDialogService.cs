namespace ThreadPilot.Services
{
    /// <summary>
    /// Interface for file dialog service
    /// </summary>
    public interface IFileDialogService
    {
        /// <summary>
        /// Show an open file dialog
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="filter">File filter</param>
        /// <param name="defaultExtension">Default file extension</param>
        /// <returns>Selected file path or null if cancelled</returns>
        string? ShowOpenFileDialog(string title, string filter, string defaultExtension);
        
        /// <summary>
        /// Show a save file dialog
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="filter">File filter</param>
        /// <param name="defaultFileName">Default file name</param>
        /// <returns>Selected file path or null if cancelled</returns>
        string? ShowSaveFileDialog(string title, string filter, string defaultFileName);
        
        /// <summary>
        /// Show a folder browser dialog
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <returns>Selected folder path or null if cancelled</returns>
        string? ShowFolderBrowserDialog(string title);
    }
}