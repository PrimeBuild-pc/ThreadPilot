/*
 * ThreadPilot - Advanced Windows Process and Power Plan Manager
 * Copyright (C) 2025 Prime Build
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, version 3 only.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ThreadPilot.Models;
using ThreadPilot.Services;

namespace ThreadPilot.ViewModels
{
    /// <summary>
    /// ViewModel for application settings
    /// </summary>
    public partial class SettingsViewModel : BaseViewModel
    {
        private readonly IApplicationSettingsService _settingsService;
        private readonly INotificationService _notificationService;
        private readonly IAutostartService _autostartService;
        private readonly IPowerPlanService _powerPlanService;
        private readonly IProcessPowerPlanAssociationService _associationService;
        private readonly IProcessMonitorManagerService _processMonitorManagerService;
        private readonly IThemeService _themeService;
        private bool _isSyncingFromService = false;
        private string _cachedDefaultPowerPlanGuid = string.Empty;
        private string _cachedDefaultPowerPlanName = string.Empty;

        [ObservableProperty]
        private ApplicationSettingsModel settings;

        [ObservableProperty]
        private bool hasUnsavedChanges = false;

        [ObservableProperty]
        private bool isLoading = false;

        public bool CanSaveSettings => HasUnsavedChanges && !IsLoading;

        public bool HasPendingChanges => HasUnsavedChanges;

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
            IEnhancedLoggingService? enhancedLoggingService = null)
            : base(logger, enhancedLoggingService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _autostartService = autostartService ?? throw new ArgumentNullException(nameof(autostartService));
            _powerPlanService = powerPlanService ?? throw new ArgumentNullException(nameof(powerPlanService));
            _associationService = associationService ?? throw new ArgumentNullException(nameof(associationService));
            _processMonitorManagerService = processMonitorManagerService ?? throw new ArgumentNullException(nameof(processMonitorManagerService));
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));

            // Get version and strip the git commit hash (everything after '+')
            var rawVersion = typeof(App).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion
                ?? typeof(App).Assembly.GetName().Version?.ToString()
                ?? "0.0.0";

            // Remove commit hash suffix and add 'v' prefix
            var cleanVersion = rawVersion.Split('+')[0];
            ApplicationVersion = $"v{cleanVersion}";

            // Initialize with current settings
            settings = (ApplicationSettingsModel)_settingsService.Settings.Clone();

            // Initialize commands
            SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
            ResetToDefaultsCommand = new AsyncRelayCommand(ResetToDefaultsAsync);
            ExportSettingsCommand = new AsyncRelayCommand(ExportSettingsAsync);
            ImportSettingsCommand = new AsyncRelayCommand(ImportSettingsAsync);
            TestNotificationCommand = new AsyncRelayCommand(TestNotificationAsync);
            RefreshPowerPlansCommand = new AsyncRelayCommand(RefreshPowerPlansAsync);
            CheckUpdatesCommand = new AsyncRelayCommand(CheckUpdatesAsync);

            // Subscribe to property changes to track unsaved changes
            Settings.PropertyChanged += OnSettingsPropertyChanged;

            // Keep viewmodel in sync with persisted settings
            _settingsService.SettingsChanged += OnSettingsServiceSettingsChanged;

            // Ensure we load the latest persisted settings on startup
            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(async () => await RefreshSettingsAsync());

            // Initialize data - marshal to UI thread to prevent cross-thread access exceptions
            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(async () => await RefreshPowerPlansAsync());

            Logger.LogInformation("Settings ViewModel initialized");
        }

        private void OnSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_isSyncingFromService)
            {
                return;
            }

            if (string.Equals(e.PropertyName, nameof(ApplicationSettingsModel.UseDarkTheme), StringComparison.Ordinal))
            {
                Settings.HasUserThemePreference = true;
            }

            HasUnsavedChanges = true;
            StatusMessage = "Settings have been modified";
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
                IsLoading = true;
                StatusMessage = "Saving settings...";
                var warnings = new List<string>();

                previousDefaultPowerPlanGuid = Settings.DefaultPowerPlanId;
                previousDefaultPowerPlanName = Settings.DefaultPowerPlanName;

                // Handle autostart setting
                var currentAutostartState = await _autostartService.CheckAutostartStatusAsync();
                if (Settings.AutostartWithWindows != currentAutostartState)
                {
                    bool autostartUpdated;
                    if (Settings.AutostartWithWindows)
                    {
                        autostartUpdated = await _autostartService.EnableAutostartAsync(Settings.StartMinimized);
                    }
                    else
                    {
                        autostartUpdated = await _autostartService.DisableAutostartAsync();
                    }

                    if (!autostartUpdated)
                    {
                        warnings.Add("Failed to update Windows autostart. Keeping previous autostart state.");
                        Settings.AutostartWithWindows = currentAutostartState;
                    }
                    else
                    {
                        Settings.AutostartWithWindows = await _autostartService.CheckAutostartStatusAsync();
                    }
                }

                await _settingsService.UpdateSettingsAsync(Settings);

                var useDarkTheme = Settings.HasUserThemePreference
                    ? Settings.UseDarkTheme
                    : _themeService.GetSystemUsesDarkTheme();

                _isSyncingFromService = true;
                Settings.UseDarkTheme = useDarkTheme;
                _isSyncingFromService = false;
                _themeService.ApplyTheme(useDarkTheme);

                // Update monitoring services with new settings
                _processMonitorManagerService.UpdateSettings();

                HasUnsavedChanges = false;
                if (warnings.Count > 0)
                {
                    StatusMessage = $"Settings saved with warnings: {string.Join(" ", warnings)}";
                    await _notificationService.ShowNotificationAsync(
                        "Settings Saved with Warnings",
                        string.Join(" ", warnings),
                        NotificationType.Warning);
                }
                else
                {
                    StatusMessage = "Settings saved and applied successfully.";
                    await _notificationService.ShowSuccessNotificationAsync(
                        "Settings Saved",
                        "Application settings have been saved successfully");
                }

                Logger.LogInformation("Settings saved successfully");
            }
            catch (Exception ex)
            {
                Settings.DefaultPowerPlanId = previousDefaultPowerPlanGuid;
                Settings.DefaultPowerPlanName = previousDefaultPowerPlanName;

                StatusMessage = $"Error saving settings: {ex.Message}";
                Logger.LogError(ex, "Error saving settings");

                await _notificationService.ShowErrorNotificationAsync(
                    "Settings Error", 
                    "Failed to save settings", 
                    ex);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ResetToDefaultsAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Resetting to defaults...";

                var defaultSettings = new ApplicationSettingsModel();
                Settings.CopyFrom(defaultSettings);

                HasUnsavedChanges = true;
                StatusMessage = "Settings reset to defaults (not saved yet)";

                Logger.LogInformation("Settings reset to defaults");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error resetting settings: {ex.Message}";
                Logger.LogError(ex, "Error resetting settings");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ExportSettingsAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Exporting settings...";

                // In a real implementation, you would show a file dialog
                var exportPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    $"ThreadPilot_Settings_{DateTime.Now:yyyyMMdd_HHmmss}.json");

                await _settingsService.ExportSettingsAsync(exportPath);

                StatusMessage = $"Settings exported to: {exportPath}";

                await _notificationService.ShowSuccessNotificationAsync(
                    "Settings Exported", 
                    $"Settings exported to {System.IO.Path.GetFileName(exportPath)}");

                Logger.LogInformation("Settings exported to {Path}", exportPath);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error exporting settings: {ex.Message}";
                Logger.LogError(ex, "Error exporting settings");

                await _notificationService.ShowErrorNotificationAsync(
                    "Export Error", 
                    "Failed to export settings", 
                    ex);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ImportSettingsAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Importing settings...";

                // In a real implementation, you would show a file dialog
                // For now, we'll just show a message
                StatusMessage = "Import feature requires file dialog implementation";

                Logger.LogInformation("Import settings requested");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error importing settings: {ex.Message}";
                Logger.LogError(ex, "Error importing settings");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task TestNotificationAsync()
        {
            try
            {
                await _notificationService.ShowNotificationAsync(
                    "Test Notification", 
                    "This is a test notification to verify your settings are working correctly.",
                    NotificationType.Information);

                StatusMessage = "Test notification sent";
                Logger.LogInformation("Test notification sent");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error sending test notification: {ex.Message}";
                Logger.LogError(ex, "Error sending test notification");
            }
        }

        /// <summary>
        /// Refreshes settings from the service
        /// </summary>
        public async Task RefreshSettingsAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Loading settings...";

                await _settingsService.LoadSettingsAsync();
                await _associationService.LoadConfigurationAsync();

                var settingsSnapshot = _settingsService.Settings;
                var (defaultPowerPlanGuid, defaultPowerPlanName) = await _associationService.GetDefaultPowerPlanAsync();
                _cachedDefaultPowerPlanGuid = defaultPowerPlanGuid;
                _cachedDefaultPowerPlanName = defaultPowerPlanName;
                if (!string.IsNullOrWhiteSpace(defaultPowerPlanGuid))
                {
                    settingsSnapshot.DefaultPowerPlanId = defaultPowerPlanGuid;
                    settingsSnapshot.DefaultPowerPlanName = defaultPowerPlanName;
                }

                _isSyncingFromService = true;
                Settings.CopyFrom(settingsSnapshot);
                _isSyncingFromService = false;

                var useDarkTheme = Settings.HasUserThemePreference
                    ? Settings.UseDarkTheme
                    : _themeService.GetSystemUsesDarkTheme();

                _isSyncingFromService = true;
                Settings.UseDarkTheme = useDarkTheme;
                _isSyncingFromService = false;
                _themeService.ApplyTheme(useDarkTheme);

                HasUnsavedChanges = false;
                StatusMessage = "Settings loaded";

                Logger.LogInformation("Settings refreshed");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading settings: {ex.Message}";
                Logger.LogError(ex, "Error loading settings");
            }
            finally
            {
                _isSyncingFromService = false;
                IsLoading = false;
            }
        }

        /// <summary>
        /// Checks if there are unsaved changes
        /// </summary>
        public bool CanClose()
        {
            return !HasUnsavedChanges;
        }

        private void OnSettingsServiceSettingsChanged(object? sender, ApplicationSettingsChangedEventArgs e)
        {
            // Marshal to UI thread to avoid cross-thread property change issues
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _isSyncingFromService = true;
                try
                {
                    Settings.CopyFrom(e.NewSettings);
                    if (!string.IsNullOrWhiteSpace(_cachedDefaultPowerPlanGuid))
                    {
                        Settings.DefaultPowerPlanId = _cachedDefaultPowerPlanGuid;
                        Settings.DefaultPowerPlanName = _cachedDefaultPowerPlanName;
                    }
                    HasUnsavedChanges = false;
                    StatusMessage = "Settings synchronized";
                }
                finally
                {
                    _isSyncingFromService = false;
                }
            });
        }

        private async Task RefreshPowerPlansAsync()
        {
            try
            {
                var powerPlans = await _powerPlanService.GetPowerPlansAsync();

                AvailablePowerPlans.Clear();
                foreach (var plan in powerPlans)
                {
                    AvailablePowerPlans.Add(plan);
                }

                Logger.LogDebug("Refreshed {Count} power plans", AvailablePowerPlans.Count);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to refresh power plans");
            }
        }

        private async Task CheckUpdatesAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Checking for updates...";

                var currentVersion = ParseVersion(ApplicationVersion);
                var (latest, releaseUrl) = await GitHubUpdateChecker.GetLatestVersionAsync("PrimeBuild-pc", "ThreadPilot");

                if (latest is null)
                {
                    StatusMessage = "Unable to determine the latest version.";
                    await _notificationService.ShowErrorNotificationAsync(
                        "Update Check",
                        "Unable to retrieve latest release information.");
                    return;
                }

                if (latest > currentVersion)
                {
                    StatusMessage = $"New version available: {latest}";

                    var result = System.Windows.MessageBox.Show(
                        $"Update available\nInstalled version: {ApplicationVersion}\nNew version: {latest}\n\nDo you want to open the download page?",
                        "Update available",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                    {
                        var url = releaseUrl ?? "https://github.com/PrimeBuild-pc/ThreadPilot/releases/latest";
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = url,
                            UseShellExecute = true
                        });
                    }
                }
                else
                {
                    StatusMessage = $"Application is up to date. Installed version: {ApplicationVersion}";
                    await _notificationService.ShowSuccessNotificationAsync(
                        "Application up to date",
                        $"Installed version: {ApplicationVersion}");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error while checking updates: {ex.Message}";
                Logger.LogError(ex, "Error checking for updates");

                await _notificationService.ShowErrorNotificationAsync(
                    "Update check error",
                    "Unable to verify updates",
                    ex);
            }
            finally
            {
                IsLoading = false;
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
            if (!HasUnsavedChanges)
            {
                return true;
            }

            await SaveSettingsAsync();
            return !HasUnsavedChanges;
        }

        public async Task DiscardPendingChangesAsync()
        {
            if (!HasUnsavedChanges)
            {
                return;
            }

            await RefreshSettingsAsync();
        }
    }
}

