using System;
using System.IO;
using System.Windows.Forms;

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
        public string ShowOpenFileDialog(string title, string filter)
        {
            using var dialog = new OpenFileDialog
            {
                Title = title,
                Filter = filter,
                CheckFileExists = true,
                CheckPathExists = true,
                Multiselect = false
            };
            
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                return dialog.FileName;
            }
            
            return string.Empty;
        }
        
        /// <summary>
        /// Show a save file dialog
        /// </summary>
        public string ShowSaveFileDialog(string title, string filter, string? defaultFileName = null)
        {
            using var dialog = new SaveFileDialog
            {
                Title = title,
                Filter = filter,
                CheckPathExists = true,
                OverwritePrompt = true
            };
            
            if (!string.IsNullOrEmpty(defaultFileName))
            {
                dialog.FileName = defaultFileName;
            }
            
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                return dialog.FileName;
            }
            
            return string.Empty;
        }
        
        /// <summary>
        /// Show a folder browser dialog
        /// </summary>
        public string ShowFolderBrowserDialog(string description)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = description,
                ShowNewFolderButton = true,
                UseDescriptionForTitle = true
            };
            
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                return dialog.SelectedPath;
            }
            
            return string.Empty;
        }
    }
}