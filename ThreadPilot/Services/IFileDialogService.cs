namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for file dialogs
    /// </summary>
    public interface IFileDialogService
    {
        /// <summary>
        /// Show an open file dialog
        /// </summary>
        /// <param name="filter">File filter pattern</param>
        /// <param name="title">Dialog title</param>
        /// <returns>Selected file path or null if canceled</returns>
        string? OpenFile(string filter, string title);
        
        /// <summary>
        /// Show a save file dialog
        /// </summary>
        /// <param name="filter">File filter pattern</param>
        /// <param name="title">Dialog title</param>
        /// <param name="defaultFileName">Default file name</param>
        /// <returns>Selected file path or null if canceled</returns>
        string? SaveFile(string filter, string title, string defaultFileName = "");
        
        /// <summary>
        /// Show a folder browser dialog
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <returns>Selected folder path or null if canceled</returns>
        string? SelectFolder(string title);
    }
}