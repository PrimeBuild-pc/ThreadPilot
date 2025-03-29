using Microsoft.Win32;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Implementation of the file dialog service
    /// </summary>
    public class FileDialogService : IFileDialogService
    {
        /// <summary>
        /// Show an open file dialog
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="filter">File filter</param>
        /// <returns>The selected file path, or null if canceled</returns>
        public string ShowOpenFileDialog(string title, string filter)
        {
            var dialog = new OpenFileDialog
            {
                Title = title,
                Filter = filter,
                CheckFileExists = true
            };
            
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }
        
        /// <summary>
        /// Show a save file dialog
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="filter">File filter</param>
        /// <param name="defaultFileName">Default file name</param>
        /// <returns>The selected file path, or null if canceled</returns>
        public string ShowSaveFileDialog(string title, string filter, string defaultFileName = null)
        {
            var dialog = new SaveFileDialog
            {
                Title = title,
                Filter = filter,
                OverwritePrompt = true
            };
            
            if (!string.IsNullOrEmpty(defaultFileName))
            {
                dialog.FileName = defaultFileName;
            }
            
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }
    }
}