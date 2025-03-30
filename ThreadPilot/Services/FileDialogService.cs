using Microsoft.Win32;
using System.IO;

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
        /// <param name="title">The dialog title</param>
        /// <param name="filter">The file filter (e.g., "Text files|*.txt")</param>
        /// <returns>The selected file path, or null if canceled</returns>
        public string? ShowOpenFileDialog(string title, string filter)
        {
            var dialog = new OpenFileDialog
            {
                Title = title,
                Filter = filter,
                CheckFileExists = true
            };
            
            if (dialog.ShowDialog() == true)
            {
                return dialog.FileName;
            }
            
            return null;
        }
        
        /// <summary>
        /// Show a save file dialog
        /// </summary>
        /// <param name="title">The dialog title</param>
        /// <param name="filter">The file filter (e.g., "Text files|*.txt")</param>
        /// <param name="defaultFileName">The default file name</param>
        /// <returns>The selected file path, or null if canceled</returns>
        public string? ShowSaveFileDialog(string title, string filter, string defaultFileName = "")
        {
            var dialog = new SaveFileDialog
            {
                Title = title,
                Filter = filter,
                FileName = SanitizeFileName(defaultFileName)
            };
            
            if (dialog.ShowDialog() == true)
            {
                return dialog.FileName;
            }
            
            return null;
        }
        
        /// <summary>
        /// Sanitize a filename to remove invalid characters
        /// </summary>
        /// <param name="fileName">The file name to sanitize</param>
        /// <returns>The sanitized file name</returns>
        private string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return string.Empty;
            }
            
            // Remove invalid characters from the filename
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                fileName = fileName.Replace(c, '_');
            }
            
            return fileName;
        }
    }
}