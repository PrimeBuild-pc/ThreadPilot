namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for file open/save dialogs
    /// </summary>
    public interface IFileDialogService
    {
        /// <summary>
        /// Opens a file dialog to select a file
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="filter">File filter</param>
        /// <returns>The selected file path or null if canceled</returns>
        string OpenFile(string title, string filter);

        /// <summary>
        /// Opens a file dialog to save a file
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="filter">File filter</param>
        /// <param name="initialDirectory">Initial directory to show</param>
        /// <param name="defaultFileName">Default file name</param>
        /// <returns>The selected file path or null if canceled</returns>
        string SaveFile(string title, string filter, string initialDirectory = null, string defaultFileName = null);
    }
}