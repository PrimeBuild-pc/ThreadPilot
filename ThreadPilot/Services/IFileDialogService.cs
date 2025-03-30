using System.Collections.Generic;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Interface for the file dialog service.
    /// </summary>
    public interface IFileDialogService
    {
        /// <summary>
        /// Shows an open file dialog.
        /// </summary>
        /// <param name="title">The dialog title.</param>
        /// <param name="filter">The file filter (e.g., "Text Files (*.txt)|*.txt|All Files (*.*)|*.*").</param>
        /// <param name="initialDirectory">The initial directory.</param>
        /// <returns>The selected file path, or null if the user cancelled.</returns>
        string? ShowOpenFileDialog(string title, string filter, string initialDirectory = "");

        /// <summary>
        /// Shows an open file dialog that allows multiple selection.
        /// </summary>
        /// <param name="title">The dialog title.</param>
        /// <param name="filter">The file filter (e.g., "Text Files (*.txt)|*.txt|All Files (*.*)|*.*").</param>
        /// <param name="initialDirectory">The initial directory.</param>
        /// <returns>The selected file paths, or an empty array if the user cancelled.</returns>
        string[] ShowOpenFilesDialog(string title, string filter, string initialDirectory = "");

        /// <summary>
        /// Shows a save file dialog.
        /// </summary>
        /// <param name="title">The dialog title.</param>
        /// <param name="filter">The file filter (e.g., "Text Files (*.txt)|*.txt|All Files (*.*)|*.*").</param>
        /// <param name="initialDirectory">The initial directory.</param>
        /// <param name="defaultFileName">The default file name.</param>
        /// <returns>The selected file path, or null if the user cancelled.</returns>
        string? ShowSaveFileDialog(string title, string filter, string initialDirectory = "", string defaultFileName = "");

        /// <summary>
        /// Shows a folder browser dialog.
        /// </summary>
        /// <param name="title">The dialog title.</param>
        /// <param name="initialDirectory">The initial directory.</param>
        /// <returns>The selected folder path, or null if the user cancelled.</returns>
        string? ShowFolderBrowserDialog(string title, string initialDirectory = "");

        /// <summary>
        /// Shows a folder browser dialog that allows multiple selection.
        /// </summary>
        /// <param name="title">The dialog title.</param>
        /// <param name="initialDirectory">The initial directory.</param>
        /// <returns>The selected folder paths, or an empty array if the user cancelled.</returns>
        string[] ShowFoldersBrowserDialog(string title, string initialDirectory = "");

        /// <summary>
        /// Gets the common file filters.
        /// </summary>
        /// <returns>A dictionary of file filters.</returns>
        Dictionary<string, string> GetCommonFileFilters();

        /// <summary>
        /// Gets the power profile file filter.
        /// </summary>
        /// <returns>The power profile file filter.</returns>
        string GetPowerProfileFileFilter();

        /// <summary>
        /// Gets a combined file filter.
        /// </summary>
        /// <param name="filters">The file filters to combine.</param>
        /// <returns>The combined file filter.</returns>
        string GetCombinedFileFilter(IEnumerable<string> filters);
    }
}