using Microsoft.Win32;
using System.Windows.Forms;

namespace ThreadPilot.Services
{
    /// <summary>
    /// File dialog service
    /// </summary>
    public class FileDialogService : IFileDialogService
    {
        /// <summary>
        /// Show open dialog
        /// </summary>
        public string ShowOpenDialog(string filter)
        {
            var dialog = new OpenFileDialog
            {
                Filter = filter,
                CheckFileExists = true,
                Multiselect = false
            };
            
            return dialog.ShowDialog() == true ? dialog.FileName : string.Empty;
        }
        
        /// <summary>
        /// Show save dialog
        /// </summary>
        public string ShowSaveDialog(string filter, string defaultFileName = "")
        {
            var dialog = new SaveFileDialog
            {
                Filter = filter,
                FileName = defaultFileName
            };
            
            return dialog.ShowDialog() == true ? dialog.FileName : string.Empty;
        }
        
        /// <summary>
        /// Show folder browser dialog
        /// </summary>
        public string ShowFolderBrowserDialog()
        {
            using var dialog = new FolderBrowserDialog
            {
                ShowNewFolderButton = true
            };
            
            return dialog.ShowDialog() == DialogResult.OK ? dialog.SelectedPath : string.Empty;
        }
    }
}