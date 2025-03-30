using System;

namespace ThreadPilot.Services
{
    /// <summary>
    /// File dialog service interface
    /// </summary>
    public interface IFileDialogService
    {
        /// <summary>
        /// Open file dialog
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="filter">File filter (e.g. "Text files (*.txt)|*.txt")</param>
        /// <returns>Selected file path or null if canceled</returns>
        string OpenFileDialog(string title, string filter);
        
        /// <summary>
        /// Save file dialog
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="filter">File filter (e.g. "Text files (*.txt)|*.txt")</param>
        /// <param name="defaultFileName">Default file name</param>
        /// <returns>Selected file path or null if canceled</returns>
        string SaveFileDialog(string title, string filter, string defaultFileName = "");
        
        /// <summary>
        /// Open folder dialog
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <returns>Selected folder path or null if canceled</returns>
        string OpenFolderDialog(string title);
    }
}