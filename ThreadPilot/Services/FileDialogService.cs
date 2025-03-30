using System;
using System.Collections.Generic;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Forms;
using System.IO;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Implementation of IFileDialogService
    /// </summary>
    public class FileDialogService : IFileDialogService
    {
        private readonly Window _mainWindow;

        /// <summary>
        /// Constructs a new instance of the FileDialogService
        /// </summary>
        /// <param name="mainWindow">The main window to anchor dialogs to</param>
        public FileDialogService(Window mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }

        /// <summary>
        /// Opens a file dialog to select a file
        /// </summary>
        /// <param name="filter">The file filter (e.g., "Power Profile|*.pow")</param>
        /// <param name="title">The title of the dialog</param>
        /// <returns>The selected file path, or null if cancelled</returns>
        public string OpenFileDialog(string filter, string title = "Select a file")
        {
            var dialog = new OpenFileDialog
            {
                Filter = filter,
                Title = title,
                CheckFileExists = true,
                CheckPathExists = true
            };

            if (dialog.ShowDialog(_mainWindow) == true)
            {
                return dialog.FileName;
            }

            return null;
        }

        /// <summary>
        /// Opens a file dialog to save a file
        /// </summary>
        /// <param name="filter">The file filter (e.g., "Power Profile|*.pow")</param>
        /// <param name="defaultFileName">The default file name</param>
        /// <param name="title">The title of the dialog</param>
        /// <returns>The selected file path, or null if cancelled</returns>
        public string SaveFileDialog(string filter, string defaultFileName = "", string title = "Save file")
        {
            var dialog = new SaveFileDialog
            {
                Filter = filter,
                Title = title,
                FileName = defaultFileName,
                OverwritePrompt = true
            };

            if (dialog.ShowDialog(_mainWindow) == true)
            {
                return dialog.FileName;
            }

            return null;
        }

        /// <summary>
        /// Opens a folder browser dialog
        /// </summary>
        /// <param name="title">The title of the dialog</param>
        /// <returns>The selected folder path, or null if cancelled</returns>
        public string OpenFolderBrowserDialog(string title = "Select a folder")
        {
            using (var dialog = new FolderBrowserDialog
            {
                Description = title,
                ShowNewFolderButton = true
            })
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    return dialog.SelectedPath;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the extension filters for power profile files
        /// </summary>
        /// <returns>Power profile file filter string</returns>
        public string GetPowerProfileFilter()
        {
            return "Power Profile (*.pow)|*.pow|All Files (*.*)|*.*";
        }

        /// <summary>
        /// Gets common file dialog filters
        /// </summary>
        /// <returns>Dictionary of file type names and their filter strings</returns>
        public Dictionary<string, string> GetCommonFileFilters()
        {
            return new Dictionary<string, string>
            {
                { "Power Profile", "Power Profile (*.pow)|*.pow" },
                { "Text Files", "Text Files (*.txt)|*.txt" },
                { "All Files", "All Files (*.*)|*.*" },
                { "Executables", "Executable (*.exe)|*.exe" },
                { "XML Files", "XML Files (*.xml)|*.xml" },
                { "JSON Files", "JSON Files (*.json)|*.json" },
                { "Config Files", "Config Files (*.config)|*.config" }
            };
        }
    }
}