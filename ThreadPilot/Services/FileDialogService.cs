using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for handling file dialogs
    /// </summary>
    public class FileDialogService : IFileDialogService
    {
        /// <summary>
        /// Show an open file dialog
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="filter">File filter</param>
        /// <param name="defaultExtension">Default file extension</param>
        /// <returns>Selected file path or null if canceled</returns>
        public string ShowOpenFileDialog(string title, string filter, string defaultExtension)
        {
            try
            {
                var dialog = new OpenFileDialog
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
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error showing open file dialog: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            
            return null;
        }
        
        /// <summary>
        /// Show a save file dialog
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="filter">File filter</param>
        /// <param name="defaultExtension">Default file extension</param>
        /// <param name="defaultFileName">Default file name</param>
        /// <returns>Selected file path or null if canceled</returns>
        public string ShowSaveFileDialog(string title, string filter, string defaultExtension, string defaultFileName)
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Title = title,
                    Filter = filter,
                    DefaultExt = defaultExtension,
                    FileName = defaultFileName,
                    AddExtension = true,
                    CreatePrompt = false,
                    OverwritePrompt = true
                };
                
                bool? result = dialog.ShowDialog();
                
                if (result == true)
                {
                    return dialog.FileName;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error showing save file dialog: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            
            return null;
        }
    }
}