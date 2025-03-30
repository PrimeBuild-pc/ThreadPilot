namespace ThreadPilot.Services
{
    /// <summary>
    /// File dialog service interface
    /// </summary>
    public interface IFileDialogService
    {
        /// <summary>
        /// Show open dialog
        /// </summary>
        string ShowOpenDialog(string filter);
        
        /// <summary>
        /// Show save dialog
        /// </summary>
        string ShowSaveDialog(string filter, string defaultFileName = "");
        
        /// <summary>
        /// Show folder browser dialog
        /// </summary>
        string ShowFolderBrowserDialog();
    }
}