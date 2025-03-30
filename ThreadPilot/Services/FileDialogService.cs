using Microsoft.Win32;
using System.IO;
using System.Windows.Forms;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Implementation of the file dialog service interface
    /// </summary>
    public class FileDialogService : IFileDialogService
    {
        /// <summary>
        /// Show an open file dialog
        /// </summary>
        /// <param name="filter">File filter pattern</param>
        /// <param name="title">Dialog title</param>
        /// <returns>Selected file path or null if canceled</returns>
        public string? OpenFile(string filter, string title)
        {
            var dialog = new OpenFileDialog
            {
                Filter = filter,
                Title = title,
                Multiselect = false,
                CheckFileExists = true
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        /// <summary>
        /// Show a save file dialog
        /// </summary>
        /// <param name="filter">File filter pattern</param>
        /// <param name="title">Dialog title</param>
        /// <param name="defaultFileName">Default file name</param>
        /// <returns>Selected file path or null if canceled</returns>
        public string? SaveFile(string filter, string title, string defaultFileName = "")
        {
            var dialog = new SaveFileDialog
            {
                Filter = filter,
                Title = title,
                FileName = defaultFileName,
                OverwritePrompt = true
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        /// <summary>
        /// Show a folder browser dialog
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <returns>Selected folder path or null if canceled</returns>
        public string? SelectFolder(string title)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = title,
                ShowNewFolderButton = true
            };

            return dialog.ShowDialog() == DialogResult.OK ? dialog.SelectedPath : null;
        }
    }
}