namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for handling file dialogs
    /// </summary>
    public interface IFileDialogService
    {
        /// <summary>
        /// Show an open file dialog
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="filter">File filter</param>
        /// <returns>The selected file path, or null if canceled</returns>
        string ShowOpenFileDialog(string title, string filter);
        
        /// <summary>
        /// Show a save file dialog
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="filter">File filter</param>
        /// <param name="defaultFileName">Default file name</param>
        /// <returns>The selected file path, or null if canceled</returns>
        string ShowSaveFileDialog(string title, string filter, string defaultFileName = null);
    }
}