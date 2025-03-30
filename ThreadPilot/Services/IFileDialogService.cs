namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for displaying file dialogs to the user
    /// </summary>
    public interface IFileDialogService
    {
        /// <summary>
        /// Show an open file dialog
        /// </summary>
        /// <param name="filter">File filter</param>
        /// <param name="title">Dialog title</param>
        /// <returns>Selected file path or null if dialog was canceled</returns>
        string OpenFile(string filter = null, string title = null);
        
        /// <summary>
        /// Show a save file dialog
        /// </summary>
        /// <param name="filter">File filter</param>
        /// <param name="defaultFileName">Default file name</param>
        /// <param name="title">Dialog title</param>
        /// <returns>Selected file path or null if dialog was canceled</returns>
        string SaveFile(string filter = null, string defaultFileName = null, string title = null);
        
        /// <summary>
        /// Show a folder browser dialog
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <returns>Selected folder path or null if dialog was canceled</returns>
        string BrowseFolder(string title = null);
    }
}