using System;
using System.Windows.Forms;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Implementation of file dialog service using Windows Forms dialogs
    /// </summary>
    public class FileDialogService : IFileDialogService
    {
        /// <summary>
        /// Shows open file dialog
        /// </summary>
        /// <param name="filter">File filter</param>
        /// <param name="title">Dialog title</param>
        /// <returns>Selected file path or null if dialog was canceled</returns>
        public string ShowOpenFileDialog(string filter, string title = "Open File")
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = filter;
                dialog.Title = title;
                dialog.CheckFileExists = true;
                dialog.CheckPathExists = true;
                
                return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : null;
            }
        }
        
        /// <summary>
        /// Shows save file dialog
        /// </summary>
        /// <param name="filter">File filter</param>
        /// <param name="title">Dialog title</param>
        /// <returns>Selected file path or null if dialog was canceled</returns>
        public string ShowSaveFileDialog(string filter, string title = "Save File")
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = filter;
                dialog.Title = title;
                dialog.CheckPathExists = true;
                dialog.OverwritePrompt = true;
                
                return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : null;
            }
        }
        
        /// <summary>
        /// Shows folder browser dialog
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <returns>Selected folder path or null if dialog was canceled</returns>
        public string ShowFolderBrowserDialog(string title = "Select Folder")
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = title;
                dialog.ShowNewFolderButton = true;
                
                return dialog.ShowDialog() == DialogResult.OK ? dialog.SelectedPath : null;
            }
        }
    }
}