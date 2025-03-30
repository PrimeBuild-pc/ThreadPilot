using System;
using System.Windows;
using Microsoft.Win32;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Implementation of file dialog operations
    /// </summary>
    public class FileDialogService : IFileDialogService
    {
        /// <summary>
        /// Shows an open file dialog
        /// </summary>
        /// <param name="filter">The file filter</param>
        /// <param name="title">The dialog title</param>
        /// <param name="initialDirectory">The initial directory</param>
        /// <returns>The selected file path or null if canceled</returns>
        public string ShowOpenFileDialog(string filter, string title = "Open File", string initialDirectory = null)
        {
            var dialog = new OpenFileDialog
            {
                Filter = filter,
                Title = title,
                CheckFileExists = true,
                CheckPathExists = true,
                Multiselect = false
            };
            
            if (!string.IsNullOrWhiteSpace(initialDirectory))
            {
                dialog.InitialDirectory = initialDirectory;
            }
            
            var result = dialog.ShowDialog();
            return result == true ? dialog.FileName : null;
        }
        
        /// <summary>
        /// Shows a save file dialog
        /// </summary>
        /// <param name="filter">The file filter</param>
        /// <param name="title">The dialog title</param>
        /// <param name="initialDirectory">The initial directory</param>
        /// <param name="defaultFileName">The default file name</param>
        /// <returns>The selected file path or null if canceled</returns>
        public string ShowSaveFileDialog(string filter, string title = "Save File", string initialDirectory = null, string defaultFileName = null)
        {
            var dialog = new SaveFileDialog
            {
                Filter = filter,
                Title = title,
                CheckPathExists = true,
                OverwritePrompt = true
            };
            
            if (!string.IsNullOrWhiteSpace(initialDirectory))
            {
                dialog.InitialDirectory = initialDirectory;
            }
            
            if (!string.IsNullOrWhiteSpace(defaultFileName))
            {
                dialog.FileName = defaultFileName;
            }
            
            var result = dialog.ShowDialog();
            return result == true ? dialog.FileName : null;
        }
        
        /// <summary>
        /// Shows a folder browser dialog
        /// </summary>
        /// <param name="title">The dialog title</param>
        /// <param name="initialDirectory">The initial directory</param>
        /// <returns>The selected folder path or null if canceled</returns>
        public string ShowFolderBrowserDialog(string title = "Select Folder", string initialDirectory = null)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = title;
                dialog.ShowNewFolderButton = true;
                
                if (!string.IsNullOrWhiteSpace(initialDirectory))
                {
                    dialog.SelectedPath = initialDirectory;
                }
                
                var result = dialog.ShowDialog();
                return result == System.Windows.Forms.DialogResult.OK ? dialog.SelectedPath : null;
            }
        }
        
        /// <summary>
        /// Shows a custom file dialog
        /// </summary>
        /// <param name="options">The dialog options</param>
        /// <returns>The dialog result</returns>
        public object ShowCustomFileDialog(object options)
        {
            // This method would be implemented for more complex file dialog scenarios
            // For now, just return null
            return null;
        }
    }
}