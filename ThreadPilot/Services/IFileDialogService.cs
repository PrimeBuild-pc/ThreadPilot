using System;
using System.Collections.Generic;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Interface for file dialog service
    /// </summary>
    public interface IFileDialogService
    {
        /// <summary>
        /// Opens a file dialog to select a file
        /// </summary>
        /// <param name="filter">The file filter (e.g., "Power Profile|*.pow")</param>
        /// <param name="title">The title of the dialog</param>
        /// <returns>The selected file path, or null if cancelled</returns>
        string OpenFileDialog(string filter, string title = "Select a file");

        /// <summary>
        /// Opens a file dialog to save a file
        /// </summary>
        /// <param name="filter">The file filter (e.g., "Power Profile|*.pow")</param>
        /// <param name="defaultFileName">The default file name</param>
        /// <param name="title">The title of the dialog</param>
        /// <returns>The selected file path, or null if cancelled</returns>
        string SaveFileDialog(string filter, string defaultFileName = "", string title = "Save file");

        /// <summary>
        /// Opens a folder browser dialog
        /// </summary>
        /// <param name="title">The title of the dialog</param>
        /// <returns>The selected folder path, or null if cancelled</returns>
        string OpenFolderBrowserDialog(string title = "Select a folder");

        /// <summary>
        /// Gets the extension filters for power profile files
        /// </summary>
        /// <returns>Power profile file filter string</returns>
        string GetPowerProfileFilter();

        /// <summary>
        /// Gets common file dialog filters
        /// </summary>
        /// <returns>Dictionary of file type names and their filter strings</returns>
        Dictionary<string, string> GetCommonFileFilters();
    }
}