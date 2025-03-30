namespace ThreadPilot.Services
{
    /// <summary>
    /// Interface for file dialog operations
    /// </summary>
    public interface IFileDialogService
    {
        /// <summary>
        /// Shows an open file dialog
        /// </summary>
        /// <param name="filter">The file filter</param>
        /// <param name="title">The dialog title</param>
        /// <param name="initialDirectory">The initial directory</param>
        /// <returns>The selected file path or null if canceled</returns>
        string ShowOpenFileDialog(string filter, string title = "Open File", string initialDirectory = null);
        
        /// <summary>
        /// Shows a save file dialog
        /// </summary>
        /// <param name="filter">The file filter</param>
        /// <param name="title">The dialog title</param>
        /// <param name="initialDirectory">The initial directory</param>
        /// <param name="defaultFileName">The default file name</param>
        /// <returns>The selected file path or null if canceled</returns>
        string ShowSaveFileDialog(string filter, string title = "Save File", string initialDirectory = null, string defaultFileName = null);
        
        /// <summary>
        /// Shows a folder browser dialog
        /// </summary>
        /// <param name="title">The dialog title</param>
        /// <param name="initialDirectory">The initial directory</param>
        /// <returns>The selected folder path or null if canceled</returns>
        string ShowFolderBrowserDialog(string title = "Select Folder", string initialDirectory = null);
        
        /// <summary>
        /// Shows a custom file dialog
        /// </summary>
        /// <param name="options">The dialog options</param>
        /// <returns>The dialog result</returns>
        object ShowCustomFileDialog(object options);
    }
}