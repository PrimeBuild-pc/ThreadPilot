using Microsoft.Win32;
using System.Windows.Forms;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Default implementation of the file dialog service
    /// </summary>
    public class FileDialogService : IFileDialogService
    {
        /// <summary>
        /// Show an open file dialog
        /// </summary>
        /// <param name="filter">File filter</param>
        /// <param name="title">Dialog title</param>
        /// <returns>Selected file path or null if dialog was canceled</returns>
        public string OpenFile(string filter = null, string title = null)
        {
            var dialog = new OpenFileDialog
            {
                Filter = filter ?? "All Files (*.*)|*.*",
                Title = title ?? "Open File"
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
        /// <param name="filter">File filter</param>
        /// <param name="defaultFileName">Default file name</param>
        /// <param name="title">Dialog title</param>
        /// <returns>Selected file path or null if dialog was canceled</returns>
        public string SaveFile(string filter = null, string defaultFileName = null, string title = null)
        {
            var dialog = new SaveFileDialog
            {
                Filter = filter ?? "All Files (*.*)|*.*",
                Title = title ?? "Save File",
                FileName = defaultFileName ?? ""
            };
            
            if (dialog.ShowDialog() == true)
            {
                return dialog.FileName;
            }
            
            return null;
        }
        
        /// <summary>
        /// Show a folder browser dialog
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <returns>Selected folder path or null if dialog was canceled</returns>
        public string BrowseFolder(string title = null)
        {
            using (var dialog = new FolderBrowserDialog
            {
                Description = title ?? "Select Folder",
                ShowNewFolderButton = true
            })
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    return dialog.SelectedPath;
                }
                
                return null;
            }
        }
    }
}