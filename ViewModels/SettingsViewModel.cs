/*
 * ThreadPilot - Advanced Windows Process and Power Plan Manager
 * Copyright (C) 2025 Prime Build
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, version 3 only.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
namespace ThreadPilot.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Input;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using Microsoft.Extensions.Logging;
    using Microsoft.Win32;
    using ThreadPilot.Models;
    using ThreadPilot.Services;

    /// <summary>
    /// ViewModel for application settings.
    /// </summary>
    public partial class SettingsViewModel : BaseViewModel
    {
        private readonly IApplicationSettingsService settingsService;
        private readonly INotificationService notificationService;
        private readonly IAutostartService autostartService;
        private readonly IPowerPlanService powerPlanService;
        private readonly IProcessPowerPlanAssociationService associationService;
        private readonly IProcessMonitorManagerService processMonitorManagerService;
        private readonly IThemeService themeService;
        private readonly ISystemTrayService systemTrayService;
        private readonly GitHubUpdateChecker gitHubUpdateChecker;
        private bool isSyncingFromService = false;
        private string cachedDefaultPowerPlanGuid = string.Empty;
        private string cachedDefaultPowerPlanName = string.Empty;
        private static readonly JsonSerializerOptions ImportExportJsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        [ObservableProperty]
        private ApplicationSettingsModel settings;

        [ObservableProperty]
        private bool hasUnsavedChanges = false;

        [ObservableProperty]
        private bool isLoading = false;

        public bool CanSaveSettings => this.HasUnsavedChanges && !this.IsLoading;

        public bool HasPendingChanges => this.HasUnsavedChanges;

        [ObservableProperty]
        private ObservableCollection<PowerPlanModel> availablePowerPlans = new();

        public string ApplicationVersion { get; }

        public ICommand SaveSettingsCommand { get; }

        public ICommand ResetToDefaultsCommand { get; }

        public ICommand ExportSettingsCommand { get; }

        public ICommand ImportSettingsCommand { get; }

        public ICommand TestNotificationCommand { get; }

        public ICommand RefreshPowerPlansCommand { get; }

        public ICommand CheckUpdatesCommand { get; }

        public SettingsViewModel(
            ILogger<SettingsViewModel> logger,
            IApplicationSettingsService settingsService,
            INotificationService notificationService,
            IAutostartService autostartService,
            IPowerPlanService powerPlanService,
            IProcessPowerPlanAssociationService associationService,
            IProcessMonitorManagerService processMonitorManagerService,
            IThemeService themeService,
            ISystemTrayService systemTrayService,
            GitHubUpdateChecker gitHubUpdateChecker,
            IEnhancedLoggingService? enhancedLoggingService = null)
            : base(logger, enhancedLoggingService)
        {
            this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            this.notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            this.autostartService = autostartService ?? throw new ArgumentNullException(nameof(autostartService));
            this.powerPlanService = powerPlanService ?? throw new ArgumentNullException(nameof(powerPlanService));
            this.associationService = associationService ?? throw new ArgumentNullException(nameof(associationService));
            this.processMonitorManagerService = processMonitorManagerService ?? throw new ArgumentNullException(nameof(processMonitorManagerService));
            this.themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            this.systemTrayService = systemTrayService ?? throw new ArgumentNullException(nameof(systemTrayService));
            this.gitHubUpdateChecker = gitHubUpdateChecker ?? throw new ArgumentNullException(nameof(gitHubUpdateChecker));

            // Get version and strip the git commit hash (everything after '+')
            var rawVersion = typeof(App).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion
                ?? typeof(App).Assembly.GetName().Version?.ToString()
                ?? "0.0.0";

            // Remove commit hash suffix and add 'v' prefix
            var cleanVersion = rawVersion.Split('+')[0];
            this.ApplicationVersion = $"v{cleanVersion}";

            // Initialize with current settings
            this.settings = (ApplicationSettingsModel)this.settingsService.Settings.Clone();

            // Initialize commands
            this.SaveSettingsCommand = new AsyncRelayCommand(this.SaveSettingsAsync);
            this.ResetToDefaultsCommand = new AsyncRelayCommand(this.ResetToDefaultsAsync);
            this.ExportSettingsCommand = new AsyncRelayCommand(this.ExportSettingsAsync);
            this.ImportSettingsCommand = new AsyncRelayCommand(this.ImportSettingsAsync);
            this.TestNotificationCommand = new AsyncRelayCommand(this.TestNotificationAsync);
            this.RefreshPowerPlansCommand = new AsyncRelayCommand(this.RefreshPowerPlansAsync);
            this.CheckUpdatesCommand = new AsyncRelayCommand(this.CheckUpdatesAsync);

            // Subscribe to property changes to track unsaved changes
            this.Settings.PropertyChanged += this.OnSettingsPropertyChanged;

            // Keep viewmodel in sync with persisted settings
            this.settingsService.SettingsChanged += this.OnSettingsServiceSettingsChanged;

            // Ensure we load the latest persisted settings on startup
            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(async () => await this.RefreshSettingsAsync());

            // Initialize data - marshal to UI thread to prevent cross-thread access exceptions
            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(async () => await this.RefreshPowerPlansAsync());

            this.Logger.LogInformation("Settings ViewModel initialized");
        }

        private void OnSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (this.isSyncingFromService)
            {
                return;
            }

            if (string.Equals(e.PropertyName, nameof(ApplicationSettingsModel.UseDarkTheme), StringComparison.Ordinal))
            {
                this.Settings.HasUserThemePreference = true;

                var useDarkTheme = this.Settings.UseDarkTheme;
                this.themeService.ApplyTheme(useDarkTheme);
                this.systemTrayService.ApplyTheme(useDarkTheme);
            }

            this.HasUnsavedChanges = true;
            this.StatusMessage = "Settings have been modified";
        }

        partial void OnHasUnsavedChangesChanged(bool value)
        {
            OnPropertyChanged(nameof(CanSaveSettings));
        }

        partial void OnIsLoadingChanged(bool value)
        {
            OnPropertyChanged(nameof(CanSaveSettings));
        }

        private async Task SaveSettingsAsync()
        {
            string previousDefaultPowerPlanGuid = string.Empty;
            string previousDefaultPowerPlanName = string.Empty;

            try
            {
                this.IsLoading = true;
                this.StatusMessage = "Saving settings...";
                var warnings = new List<string>();

                previousDefaultPowerPlanGuid = this.Settings.DefaultPowerPlanId;
                previousDefaultPowerPlanName = this.Settings.DefaultPowerPlanName;

                // Handle autostart setting
                var currentAutostartState = await this.autostartService.CheckAutostartStatusAsync();
                if (this.Settings.AutostartWithWindows != currentAutostartState)
                {
                    bool autostartUpdated;
                    if (this.Settings.AutostartWithWindows)
                    {
                        autostartUpdated = await this.autostartService.EnableAutostartAsync(this.Settings.StartMinimized);
                    }
                    else
                    {
                        autostartUpdated = await this.autostartService.DisableAutostartAsync();
                    }

                    if (!autostartUpdated)
                    {
                        warnings.Add("Failed to update Windows autostart. Keeping previous autostart state.");
                        this.Settings.AutostartWithWindows = currentAutostartState;
                    }
                    else
                    {
                        this.Settings.AutostartWithWindows = await this.autostartService.CheckAutostartStatusAsync();
                    }
                }

                await this.settingsService.UpdateSettingsAsync(this.Settings);

                var useDarkTheme = this.Settings.HasUserThemePreference
                    ? this.Settings.UseDarkTheme
                    : this.themeService.GetSystemUsesDarkTheme();

                this.isSyncingFromService = true;
                this.Settings.UseDarkTheme = useDarkTheme;
                this.isSyncingFromService = false;
                this.themeService.ApplyTheme(useDarkTheme);
                this.systemTrayService.ApplyTheme(useDarkTheme);

                // Update monitoring services with new settings
                this.processMonitorManagerService.UpdateSettings();

                this.HasUnsavedChanges = false;
                if (warnings.Count > 0)
                {
                    this.StatusMessage = $"Settings saved with warnings: {string.Join(" ", warnings)}";
                    await this.notificationService.ShowNotificationAsync(
                        "Settings Saved with Warnings",
                        string.Join(" ", warnings),
                        NotificationType.Warning);
                }
                else
                {
                    this.StatusMessage = "Settings saved and applied successfully.";
                    await this.notificationService.ShowSuccessNotificationAsync(
                        "Settings Saved",
                        "Application settings have been saved successfully");
                }

                this.Logger.LogInformation("Settings saved successfully");
            }
            catch (Exception ex)
            {
                this.Settings.DefaultPowerPlanId = previousDefaultPowerPlanGuid;
                this.Settings.DefaultPowerPlanName = previousDefaultPowerPlanName;

                this.StatusMessage = $"Error saving settings: {ex.Message}";
                this.Logger.LogError(ex, "Error saving settings");

                await this.notificationService.ShowErrorNotificationAsync(
                    "Settings Error",
                    "Failed to save settings",
                    ex);
            }
            finally
            {
                this.IsLoading = false;
            }
        }

        private async Task ResetToDefaultsAsync()
        {
            try
            {
                this.IsLoading = true;
                this.StatusMessage = "Resetting to defaults...";

                var defaultSettings = new ApplicationSettingsModel();
                this.Settings.CopyFrom(defaultSettings);

                this.HasUnsavedChanges = true;
                this.StatusMessage = "Settings reset to defaults (not saved yet)";

                this.Logger.LogInformation("Settings reset to defaults");
            }
            catch (Exception ex)
            {
                this.StatusMessage = $"Error resetting settings: {ex.Message}";
                this.Logger.LogError(ex, "Error resetting settings");
            }
            finally
            {
                this.IsLoading = false;
            }
        }

        private async Task ExportSettingsAsync()
        {
            try
            {
                this.IsLoading = true;
                this.StatusMessage = "Exporting configuration bundle...";

                var saveDialog = new SaveFileDialog
                {
                    Title = "Export ThreadPilot Configuration",
                    Filter = "ThreadPilot configuration (*.json)|*.json|All files (*.*)|*.*",
                    DefaultExt = ".json",
                    FileName = $"ThreadPilot_Configuration_{DateTime.Now:yyyyMMdd_HHmmss}.json",
                    OverwritePrompt = true,
                    AddExtension = true,
                };

                if (saveDialog.ShowDialog() != true)
                {
                    this.StatusMessage = "Export canceled";
                    return;
                }

                var settingsSnapshot = (ApplicationSettingsModel)this.Settings.Clone();
                var rulesSnapshot = CloneConfiguration(this.associationService.Configuration);

                var bundle = new ConfigurationBundle
                {
                    SchemaVersion = "2.0",
                    ExportedAtUtc = DateTime.UtcNow,
                    Settings = settingsSnapshot,
                    ProcessMonitorConfiguration = rulesSnapshot,
                };

                var json = JsonSerializer.Serialize(bundle, ImportExportJsonOptions);
                await AtomicFileWriter.WriteAllTextAsync(saveDialog.FileName, json, Encoding.UTF8);

                this.StatusMessage = $"Configuration exported to: {saveDialog.FileName}";

                await this.notificationService.ShowSuccessNotificationAsync(
                    "Configuration Exported",
                    $"Settings and rules exported to {Path.GetFileName(saveDialog.FileName)}");

                this.Logger.LogInformation("Configuration bundle exported to {Path}", saveDialog.FileName);
            }
            catch (Exception ex)
            {
                this.StatusMessage = $"Error exporting settings: {ex.Message}";
                this.Logger.LogError(ex, "Error exporting settings");

                await this.notificationService.ShowErrorNotificationAsync(
                    "Export Error",
                    "Failed to export settings",
                    ex);
            }
            finally
            {
                this.IsLoading = false;
            }
        }

        private async Task ImportSettingsAsync()
        {
            try
            {
                this.IsLoading = true;
                this.StatusMessage = "Importing configuration...";

                var openDialog = new OpenFileDialog
                {
                    Title = "Import ThreadPilot Configuration",
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    Multiselect = false,
                    CheckFileExists = true,
                };

                if (openDialog.ShowDialog() != true)
                {
                    this.StatusMessage = "Import canceled";
                    return;
                }

                var importPath = openDialog.FileName;
                var json = await File.ReadAllTextAsync(importPath);

                if (TryParseBundle(json, out var bundle))
                {
                    await this.settingsService.UpdateSettingsAsync(bundle.Settings);

                    var importedConfiguration = bundle.ProcessMonitorConfiguration ?? new ProcessMonitorConfiguration();
                    var replaced = await this.associationService.ReplaceConfigurationAsync(importedConfiguration);
                    if (!replaced)
                    {
                        throw new InvalidOperationException("Failed to apply imported rules configuration");
                    }

                    await this.processMonitorManagerService.RefreshConfigurationAsync();
                    this.processMonitorManagerService.UpdateSettings();
                    await this.RefreshSettingsAsync();
                    this.HasUnsavedChanges = false;

                    this.StatusMessage = "Configuration bundle imported and applied";
                    await this.notificationService.ShowSuccessNotificationAsync(
                        "Configuration Imported",
                        $"Settings and rules imported from {Path.GetFileName(importPath)}");

                    this.Logger.LogInformation("Configuration bundle imported from {Path}", importPath);
                    return;
                }

                await this.settingsService.ImportSettingsAsync(importPath);
                this.processMonitorManagerService.UpdateSettings();
                await this.RefreshSettingsAsync();
                this.HasUnsavedChanges = false;

                this.StatusMessage = "Legacy settings imported (rules unchanged)";
                await this.notificationService.ShowNotificationAsync(
                    "Legacy Import Completed",
                    $"Imported settings from {Path.GetFileName(importPath)}. Rules were not modified.",
                    NotificationType.Information);

                this.Logger.LogInformation("Legacy settings imported from {Path}", importPath);
            }
            catch (Exception ex)
            {
                this.StatusMessage = $"Error importing settings: {ex.Message}";
                this.Logger.LogError(ex, "Error importing settings");

                await this.notificationService.ShowErrorNotificationAsync(
                    "Import Error",
                    "Failed to import configuration",
                    ex);
            }
            finally
            {
                this.IsLoading = false;
            }
        }

        private async Task TestNotificationAsync()
        {
            try
            {
                await this.notificationService.ShowNotificationAsync(
                    "Test Notification",
                    "This is a test notification to verify your settings are working correctly.",
                    NotificationType.Information);

                this.StatusMessage = "Test notification sent";
                this.Logger.LogInformation("Test notification sent");
            }
            catch (Exception ex)
            {
                this.StatusMessage = $"Error sending test notification: {ex.Message}";
                this.Logger.LogError(ex, "Error sending test notification");
            }
        }

        /// <summary>
        /// Refreshes settings from the service.
        /// </summary>
        public async Task RefreshSettingsAsync()
        {
            try
            {
                this.IsLoading = true;
                this.StatusMessage = "Loading settings...";

                await this.settingsService.LoadSettingsAsync();
                await this.associationService.LoadConfigurationAsync();

                var settingsSnapshot = this.settingsService.Settings;
                var (defaultPowerPlanGuid, defaultPowerPlanName) = await this.associationService.GetDefaultPowerPlanAsync();
                this.cachedDefaultPowerPlanGuid = defaultPowerPlanGuid;
                this.cachedDefaultPowerPlanName = defaultPowerPlanName;
                if (!string.IsNullOrWhiteSpace(defaultPowerPlanGuid))
                {
                    settingsSnapshot.DefaultPowerPlanId = defaultPowerPlanGuid;
                    settingsSnapshot.DefaultPowerPlanName = defaultPowerPlanName;
                }

                this.isSyncingFromService = true;
                this.Settings.CopyFrom(settingsSnapshot);
                this.isSyncingFromService = false;

                var useDarkTheme = this.Settings.HasUserThemePreference
                    ? this.Settings.UseDarkTheme
                    : this.themeService.GetSystemUsesDarkTheme();

                this.isSyncingFromService = true;
                this.Settings.UseDarkTheme = useDarkTheme;
                this.isSyncingFromService = false;
                this.themeService.ApplyTheme(useDarkTheme);
                this.systemTrayService.ApplyTheme(useDarkTheme);

                this.HasUnsavedChanges = false;
                this.StatusMessage = "Settings loaded";

                this.Logger.LogInformation("Settings refreshed");
            }
            catch (Exception ex)
            {
                this.StatusMessage = $"Error loading settings: {ex.Message}";
                this.Logger.LogError(ex, "Error loading settings");
            }
            finally
            {
                this.isSyncingFromService = false;
                this.IsLoading = false;
            }
        }

        /// <summary>
        /// Checks if there are unsaved changes.
        /// </summary>
        public bool CanClose()
        {
            return !this.HasUnsavedChanges;
        }

        private void OnSettingsServiceSettingsChanged(object? sender, ApplicationSettingsChangedEventArgs e)
        {
            // Marshal to UI thread to avoid cross-thread property change issues
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                this.isSyncingFromService = true;
                try
                {
                    this.Settings.CopyFrom(e.NewSettings);
                    if (!string.IsNullOrWhiteSpace(this.cachedDefaultPowerPlanGuid))
                    {
                        this.Settings.DefaultPowerPlanId = this.cachedDefaultPowerPlanGuid;
                        this.Settings.DefaultPowerPlanName = this.cachedDefaultPowerPlanName;
                    }
                    this.HasUnsavedChanges = false;
                    this.StatusMessage = "Settings synchronized";
                }
                finally
                {
                    this.isSyncingFromService = false;
                }
            });
        }

        private async Task RefreshPowerPlansAsync()
        {
            try
            {
                var powerPlans = await this.powerPlanService.GetPowerPlansAsync();

                this.AvailablePowerPlans.Clear();
                foreach (var plan in powerPlans)
                {
                    this.AvailablePowerPlans.Add(plan);
                }

                this.Logger.LogDebug("Refreshed {Count} power plans", this.AvailablePowerPlans.Count);
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "Failed to refresh power plans");
            }
        }

        private async Task CheckUpdatesAsync()
        {
            try
            {
                this.IsLoading = true;
                this.StatusMessage = "Checking for updates...";

                var currentVersion = ParseVersion(this.ApplicationVersion);
                var (latest, releaseUrl) = await this.gitHubUpdateChecker.GetLatestVersionAsync("PrimeBuild-pc", "ThreadPilot");

                if (latest is null)
                {
                    this.StatusMessage = "Unable to determine the latest version.";
                    await this.notificationService.ShowErrorNotificationAsync(
                        "Update Check",
                        "Unable to retrieve latest release information.");
                    return;
                }

                if (latest > currentVersion)
                {
                    this.StatusMessage = $"New version available: {latest}";

                    var result = System.Windows.MessageBox.Show(
                        $"Update available\nInstalled version: {this.ApplicationVersion}\nNew version: {latest}\n\nDo you want to open the download page?",
                        "Update available",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                    {
                        var url = releaseUrl ?? "https://github.com/PrimeBuild-pc/ThreadPilot/releases/latest";
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = url,
                            UseShellExecute = true,
                        });
                    }
                }
                else
                {
                    this.StatusMessage = $"Application is up to date. Installed version: {this.ApplicationVersion}";
                    await this.notificationService.ShowSuccessNotificationAsync(
                        "Application up to date",
                        $"Installed version: {this.ApplicationVersion}");
                }
            }
            catch (Exception ex)
            {
                this.StatusMessage = $"Error while checking updates: {ex.Message}";
                this.Logger.LogError(ex, "Error checking for updates");

                await this.notificationService.ShowErrorNotificationAsync(
                    "Update check error",
                    "Unable to verify updates",
                    ex);
            }
            finally
            {
                this.IsLoading = false;
            }
        }

        private static Version ParseVersion(string versionString)
        {
            if (string.IsNullOrWhiteSpace(versionString))
            {
                return new Version(0, 0, 0);
            }

            var sanitized = versionString.Trim();
            if (sanitized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                sanitized = sanitized[1..];
            }

            sanitized = sanitized.Split('-', '+')[0];

            return Version.TryParse(sanitized, out var parsed)
                ? parsed
                : new Version(0, 0, 0);
        }

        public async Task<bool> SaveIfDirtyAsync()
        {
            if (!this.HasUnsavedChanges)
            {
                return true;
            }

            await this.SaveSettingsAsync();
            return !this.HasUnsavedChanges;
        }

        public async Task DiscardPendingChangesAsync()
        {
            if (!this.HasUnsavedChanges)
            {
                return;
            }

            await this.RefreshSettingsAsync();
        }

        private static bool TryParseBundle(string json, out ConfigurationBundle bundle)
        {
            bundle = new ConfigurationBundle();

            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }

                if (!document.RootElement.TryGetProperty("settings", out var settingsElement))
                {
                    return false;
                }

                if (!document.RootElement.TryGetProperty("processMonitorConfiguration", out var rulesElement) &&
                    !document.RootElement.TryGetProperty("rulesConfiguration", out rulesElement))
                {
                    return false;
                }

                var parsedBundle = JsonSerializer.Deserialize<ConfigurationBundle>(json, ImportExportJsonOptions);
                if (parsedBundle?.Settings == null)
                {
                    return false;
                }

                parsedBundle.ProcessMonitorConfiguration =
                    parsedBundle.ProcessMonitorConfiguration
                    ?? parsedBundle.RulesConfiguration
                    ?? JsonSerializer.Deserialize<ProcessMonitorConfiguration>(rulesElement.GetRawText(), ImportExportJsonOptions)
                    ?? new ProcessMonitorConfiguration();

                parsedBundle.ProcessMonitorConfiguration.Associations ??= new List<ProcessPowerPlanAssociation>();
                bundle = parsedBundle;
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static ProcessMonitorConfiguration CloneConfiguration(ProcessMonitorConfiguration source)
        {
            var serialized = JsonSerializer.Serialize(source, ImportExportJsonOptions);
            var clone = JsonSerializer.Deserialize<ProcessMonitorConfiguration>(serialized, ImportExportJsonOptions)
                ?? new ProcessMonitorConfiguration();
            clone.Associations ??= new List<ProcessPowerPlanAssociation>();
            return clone;
        }

        private sealed class ConfigurationBundle
        {
            public string SchemaVersion { get; set; } = "2.0";

            public DateTime ExportedAtUtc { get; set; } = DateTime.UtcNow;

            public ApplicationSettingsModel Settings { get; set; } = new ApplicationSettingsModel();

            public ProcessMonitorConfiguration? ProcessMonitorConfiguration { get; set; }

            public ProcessMonitorConfiguration? RulesConfiguration { get; set; }
        }
    }
}

