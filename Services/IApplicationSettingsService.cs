namespace ThreadPilot.Services
{
    using System;
    using System.Threading.Tasks;
    using ThreadPilot.Models;

    public interface IApplicationSettingsService
    {
        event EventHandler<ApplicationSettingsChangedEventArgs>? SettingsChanged;

        ApplicationSettingsModel Settings { get; }

        Task LoadSettingsAsync();

        Task SaveSettingsAsync();

        // Replaces the full profile for import and backup restore.
        Task UpdateSettingsAsync(ApplicationSettingsModel newSettings);

        /// <summary>Applies an edit to the latest committed profile and publishes it after persistence succeeds.</summary>
        /// <param name="edit">A synchronous edit that receives a detached draft.</param>
        /// <returns>A task that finishes when the edit has been persisted and published.</returns>
        Task EditSettingsAsync(Action<ApplicationSettingsModel> edit);

        Task ResetToDefaultsAsync();

        string GetSettingsFilePath();

        Task ExportSettingsAsync(string filePath);

        Task ImportSettingsAsync(string filePath);
    }

    public class ApplicationSettingsChangedEventArgs : EventArgs
    {
        public ApplicationSettingsModel OldSettings { get; }

        public ApplicationSettingsModel NewSettings { get; }

        public string[] ChangedProperties { get; }

        public ApplicationSettingsChangedEventArgs(
            ApplicationSettingsModel oldSettings,
            ApplicationSettingsModel newSettings,
            string[] changedProperties)
        {
            this.OldSettings = oldSettings;
            this.NewSettings = newSettings;
            this.ChangedProperties = changedProperties;
        }
    }
}

