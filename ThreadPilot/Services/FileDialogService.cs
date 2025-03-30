using Microsoft.Win32;
using System;
using System.IO;
using System.Windows.Forms;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Implementation of file dialog service
    /// </summary>
    public class FileDialogService : IFileDialogService
    {
        /// <summary>
        /// Show an open file dialog
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="filter">File filter</param>
        /// <param name="defaultExtension">Default file extension</param>
        /// <returns>Selected file path or null if cancelled</returns>
        public string? ShowOpenFileDialog(string title, string filter, string defaultExtension)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = title,
                Filter = filter,
                DefaultExt = defaultExtension,
                CheckFileExists = true
            };
            
            bool? result = dialog.ShowDialog();
            
            if (result == true)
            {
                return dialog.FileName;
            }
            
            return null;
        }
        
        /// <summary>
        /// Show a save file dialog
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="filter">File filter</param>
        /// <param name="defaultFileName">Default file name</param>
        /// <returns>Selected file path or null if cancelled</returns>
        public string? ShowSaveFileDialog(string title, string filter, string defaultFileName)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = title,
                Filter = filter,
                FileName = defaultFileName,
                OverwritePrompt = true
            };
            
            bool? result = dialog.ShowDialog();
            
            if (result == true)
            {
                return dialog.FileName;
            }
            
            return null;
        }
        
        /// <summary>
        /// Show a folder browser dialog
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <returns>Selected folder path or null if cancelled</returns>
        public string? ShowFolderBrowserDialog(string title)
        {
            using (var dialog = new FolderBrowserDialog
            {
                Description = title,
                ShowNewFolderButton = true
            })
            {
                DialogResult result = dialog.ShowDialog();
                
                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
                {
                    return dialog.SelectedPath;
                }
            }
            
            return null;
        }
    }
}