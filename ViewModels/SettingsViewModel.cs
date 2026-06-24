namespace ThreadPilot.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
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
        private readonly IUpdateService updateService;
        private readonly IApplicationVersionProvider versionProvider;
        private readonly ILocalizationService localizationService;
        private ApplicationSettingsModel savedSettingsSnapshot;
        private bool isSyncingFromService = false;
        private bool? appliedThemePreference;
        private string cachedDefaultPowerPlanGuid = string.Empty;
        private string cachedDefaultPowerPlanName = string.Empty;
        private UpdateReleaseInfo? availableUpdate;
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

        public IAsyncRelayCommand DownloadAndInstallUpdateCommand { get; }

        [ObservableProperty]
        private string latestUpdateVersion = string.Empty;

        [ObservableProperty]
        private string lastUpdateCheckText = string.Empty;

        [ObservableProperty]
        private bool isUpdateAvailable = false;

        public bool CanDownloadAndInstallUpdate => this.IsUpdateAvailable && !this.IsLoading;

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
            IUpdateService updateService,
            IApplicationVersionProvider versionProvider,
            ILocalizationService localizationService,
            IEnhancedLoggingService? enhancedLoggingService = null,
            IActivityAuditService? activityAuditService = null)
            : base(logger, enhancedLoggingService, activityAuditService)
        {
            this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            this.notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            this.autostartService = autostartService ?? throw new ArgumentNullException(nameof(autostartService));
            this.powerPlanService = powerPlanService ?? throw new ArgumentNullException(nameof(powerPlanService));
            this.associationService = associationService ?? throw new ArgumentNullException(nameof(associationService));
            this.processMonitorManagerService = processMonitorManagerService ?? throw new ArgumentNullException(nameof(processMonitorManagerService));
            this.themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            this.systemTrayService = systemTrayService ?? throw new ArgumentNullException(nameof(systemTrayService));
            this.updateService = updateService ?? throw new ArgumentNullException(nameof(updateService));
            this.versionProvider = versionProvider ?? throw new ArgumentNullException(nameof(versionProvider));
            this.localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));

            this.ApplicationVersion = this.versionProvider.DisplayVersion;

            // Initialize with current settings
            this.settings = (ApplicationSettingsModel)this.settingsService.Settings.Clone();
            this.savedSettingsSnapshot = (ApplicationSettingsModel)this.settings.Clone();
            this.appliedThemePreference = this.settings.UseDarkTheme;
            this.LatestUpdateVersion = this.GetLocalizedString("Settings_UpdateNotChecked", "Not checked");
            this.UpdateLastCheckedText();

            // Initialize commands
            this.SaveSettingsCommand = new AsyncRelayCommand(this.SaveSettingsAsync);
            this.ResetToDefaultsCommand = new AsyncRelayCommand(this.ResetToDefaultsAsync);
            this.ExportSettingsCommand = new AsyncRelayCommand(this.ExportSettingsAsync);
            this.ImportSettingsCommand = new AsyncRelayCommand(this.ImportSettingsAsync);
            this.TestNotificationCommand = new AsyncRelayCommand(this.TestNotificationAsync);
            this.RefreshPowerPlansCommand = new AsyncRelayCommand(this.RefreshPowerPlansAsync);
            this.CheckUpdatesCommand = new AsyncRelayCommand(this.CheckUpdatesAsync);
            this.DownloadAndInstallUpdateCommand = new AsyncRelayCommand(
                this.DownloadAndInstallUpdateAsync,
                () => this.CanDownloadAndInstallUpdate);

            // Subscribe to property changes to track unsaved changes
            this.Settings.PropertyChanged += this.OnSettingsPropertyChanged;

            // Keep viewmodel in sync with persisted settings
            this.settingsService.SettingsChanged += this.OnSettingsServiceSettingsChanged;

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null)
            {
                // Ensure we load the latest persisted settings on startup.
                _ = dispatcher.InvokeAsync(async () => await this.RefreshSettingsAsync());

                // Initialize data - marshal to UI thread to prevent cross-thread access exceptions.
                _ = dispatcher.InvokeAsync(async () => await this.RefreshPowerPlansAsync());
            }

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
                this.UpdatePendingChangesState();
                this.ApplyThemePreference(this.Settings.UseDarkTheme, logUserAction: true);
                return;
            }

            if (string.Equals(e.PropertyName, nameof(ApplicationSettingsModel.Language), StringComparison.Ordinal))
            {
                this.UpdatePendingChangesState();
                this.ApplyLanguagePreference(this.Settings.Language, logUserAction: true);
                return;
            }

            if (string.Equals(e.PropertyName, nameof(ApplicationSettingsModel.ApplyPersistentRulesOnProcessStart), StringComparison.Ordinal))
            {
                this.UpdatePendingChangesState();
                var state = this.Settings.ApplyPersistentRulesOnProcessStart ? "enabled" : "disabled";
                _ = this.LogUserActionAsync(
                    "SettingsChanged",
                    $"[Settings] Apply saved rules at process start {state}.");
                return;
            }

            this.UpdatePendingChangesState();
        }

        private void ApplyThemePreference(bool useDarkTheme, bool logUserAction)
        {
            if (this.appliedThemePreference == useDarkTheme)
            {
                return;
            }

            var themeName = useDarkTheme
                ? this.GetLocalizedString("Settings_ThemeDark", "Dark")
                : this.GetLocalizedString("Settings_ThemeLight", "Light");
            try
            {
                this.themeService.ApplyTheme(useDarkTheme);
                this.systemTrayService.ApplyTheme(useDarkTheme);
                this.appliedThemePreference = useDarkTheme;
                this.StatusMessage = this.GetLocalizedString("Settings_StatusThemeChangedFormat", "Theme changed to {0}.", themeName);

                if (logUserAction)
                {
                    _ = this.LogUserActionAsync("ThemeChanged", $"Theme changed to {themeName}");
                }
            }
            catch (Exception ex)
            {
                this.StatusMessage = this.GetLocalizedString("Settings_StatusThemeChangeFailedFormat", "Failed to change theme to {0}.", themeName);
                this.Logger.LogError(ex, "Failed to apply theme preference {ThemeName}", themeName);
                _ = this.LogUserActionAsync("ThemeChangeFailed", $"Failed to change theme to {themeName}: {ex.Message}");
            }
        }

        private void ApplyLanguagePreference(string language, bool logUserAction)
        {
            var normalizedLanguage = LocalizationService.NormalizeLanguage(language);
            try
            {
                this.localizationService.ApplyLanguage(normalizedLanguage);
                this.Settings.Language = normalizedLanguage;
                var languageName = normalizedLanguage == LocalizationService.SimplifiedChineseLanguage
                    ? this.GetLocalizedString("Settings_LanguageSimplifiedChinese", "Simplified Chinese")
                    : this.GetLocalizedString("Settings_LanguageEnglish", "English");
                this.StatusMessage = this.GetLocalizedString("Settings_StatusLanguageChangedFormat", "Language changed to {0}.", languageName);

                if (logUserAction)
                {
                    _ = this.LogUserActionAsync("LanguageChanged", $"Language changed to {languageName}");
                }
            }
            catch (Exception ex)
            {
                this.StatusMessage = this.GetLocalizedString("Settings_StatusLanguageChangeFailed", "Failed to change language.");
                this.Logger.LogError(ex, "Failed to apply language preference {Language}", normalizedLanguage);
                _ = this.LogUserActionAsync("LanguageChangeFailed", $"Failed to change language to {normalizedLanguage}: {ex.Message}");
            }
        }

        partial void OnHasUnsavedChangesChanged(bool value)
        {
            OnPropertyChanged(nameof(CanSaveSettings));
        }

        partial void OnIsLoadingChanged(bool value)
        {
            OnPropertyChanged(nameof(CanSaveSettings));
            OnPropertyChanged(nameof(CanDownloadAndInstallUpdate));
            this.DownloadAndInstallUpdateCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsUpdateAvailableChanged(bool value)
        {
            OnPropertyChanged(nameof(CanDownloadAndInstallUpdate));
            this.DownloadAndInstallUpdateCommand.NotifyCanExecuteChanged();
        }

        private async Task SaveSettingsAsync()
        {
            string previousDefaultPowerPlanGuid = string.Empty;
            string previousDefaultPowerPlanName = string.Empty;

            try
            {
                this.IsLoading = true;
                this.StatusMessage = this.GetLocalizedString("Settings_StatusSaving", "Saving settings...");
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
                        warnings.Add(this.GetLocalizedString(
                            "Settings_WarningAutostartFailed",
                            "Failed to update Windows autostart. Keeping previous autostart state."));
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
                this.ApplyThemePreference(useDarkTheme, logUserAction: false);
                this.ApplyLanguagePreference(this.Settings.Language, logUserAction: false);

                // Update monitoring services with new settings
                this.processMonitorManagerService.UpdateSettings();

                this.SetSavedSettingsSnapshot(this.Settings);
                if (warnings.Count > 0)
                {
                    this.StatusMessage = this.GetLocalizedString(
                        "Settings_StatusSavedWarningsFormat",
                        "Settings saved with warnings: {0}",
                        string.Join(" ", warnings));
                    await this.notificationService.ShowNotificationAsync(
                        "Settings Saved with Warnings",
                        string.Join(" ", warnings),
                        NotificationType.Warning);
                }
                else
                {
                    this.StatusMessage = this.GetLocalizedString("Settings_StatusSavedApplied", "Settings saved and applied successfully.");
                    await this.notificationService.ShowSuccessNotificationAsync(
                        "Settings Saved",
                        "Application settings have been saved successfully");
                }

                await this.LogUserActionAsync("SettingsChanged", "Settings saved and applied");
                this.Logger.LogInformation("Settings saved successfully");
            }
            catch (Exception ex)
            {
                this.Settings.DefaultPowerPlanId = previousDefaultPowerPlanGuid;
                this.Settings.DefaultPowerPlanName = previousDefaultPowerPlanName;

                this.StatusMessage = this.GetLocalizedString("Settings_StatusErrorSavingFormat", "Error saving settings: {0}", ex.Message);
                this.Logger.LogError(ex, "Error saving settings");

                await this.notificationService.ShowErrorNotificationAsync(
                    "Settings Error",
                    "Failed to save settings",
                    ex);
                await this.LogUserActionAsync("SettingsChangeFailed", $"Failed to save settings: {ex.Message}");
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
                this.StatusMessage = this.GetLocalizedString("Settings_StatusResetting", "Resetting to defaults...");

                var defaultSettings = new ApplicationSettingsModel();
                this.Settings.CopyFrom(defaultSettings);

                this.UpdatePendingChangesState();
                this.StatusMessage = this.GetLocalizedString("Settings_StatusResetPending", "Settings reset to defaults (not saved yet)");

                await this.LogUserActionAsync("SettingsChanged", "Settings reset to defaults pending save");
                this.Logger.LogInformation("Settings reset to defaults");
            }
            catch (Exception ex)
            {
                this.StatusMessage = this.GetLocalizedString("Settings_StatusErrorResettingFormat", "Error resetting settings: {0}", ex.Message);
                this.Logger.LogError(ex, "Error resetting settings");
                await this.LogUserActionAsync("SettingsChangeFailed", $"Failed to reset settings: {ex.Message}");
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
                this.StatusMessage = this.GetLocalizedString("Settings_StatusExporting", "Exporting configuration bundle...");

                var saveDialog = new SaveFileDialog
                {
                    Title = this.GetLocalizedString("Settings_DialogExportTitle", "Export ThreadPilot Configuration"),
                    Filter = "ThreadPilot configuration (*.json)|*.json|All files (*.*)|*.*",
                    DefaultExt = ".json",
                    FileName = $"ThreadPilot_Configuration_{DateTime.Now:yyyyMMdd_HHmmss}.json",
                    OverwritePrompt = true,
                    AddExtension = true,
                };

                if (saveDialog.ShowDialog() != true)
                {
                    this.StatusMessage = this.GetLocalizedString("Settings_StatusExportCanceled", "Export canceled");
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

                this.StatusMessage = this.GetLocalizedString("Settings_StatusExportedFormat", "Configuration exported to: {0}", saveDialog.FileName);

                await this.notificationService.ShowSuccessNotificationAsync(
                    "Configuration Exported",
                    $"Settings and rules exported to {Path.GetFileName(saveDialog.FileName)}");

                this.Logger.LogInformation("Configuration bundle exported to {Path}", saveDialog.FileName);
                await this.LogUserActionAsync("SettingsChanged", "Configuration exported", Path.GetFileName(saveDialog.FileName));
            }
            catch (Exception ex)
            {
                this.StatusMessage = this.GetLocalizedString("Settings_StatusErrorExportingFormat", "Error exporting settings: {0}", ex.Message);
                this.Logger.LogError(ex, "Error exporting settings");

                await this.notificationService.ShowErrorNotificationAsync(
                    "Export Error",
                    "Failed to export settings",
                    ex);
                await this.LogUserActionAsync("SettingsChangeFailed", $"Failed to export settings: {ex.Message}");
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
                this.StatusMessage = this.GetLocalizedString("Settings_StatusImporting", "Importing configuration...");

                var openDialog = new OpenFileDialog
                {
                    Title = this.GetLocalizedString("Settings_DialogImportTitle", "Import ThreadPilot Configuration"),
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    Multiselect = false,
                    CheckFileExists = true,
                };

                if (openDialog.ShowDialog() != true)
                {
                    this.StatusMessage = this.GetLocalizedString("Settings_StatusImportCanceled", "Import canceled");
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

                    this.StatusMessage = this.GetLocalizedString("Settings_StatusImportedApplied", "Configuration bundle imported and applied");
                    await this.notificationService.ShowSuccessNotificationAsync(
                        "Configuration Imported",
                        $"Settings and rules imported from {Path.GetFileName(importPath)}");

                    this.Logger.LogInformation("Configuration bundle imported from {Path}", importPath);
                    await this.LogUserActionAsync("SettingsChanged", "Configuration bundle imported", Path.GetFileName(importPath));
                    return;
                }

                await this.settingsService.ImportSettingsAsync(importPath);
                this.processMonitorManagerService.UpdateSettings();
                await this.RefreshSettingsAsync();
                this.HasUnsavedChanges = false;

                this.StatusMessage = this.GetLocalizedString("Settings_StatusLegacyImported", "Legacy settings imported (rules unchanged)");
                await this.notificationService.ShowNotificationAsync(
                    "Legacy Import Completed",
                    $"Imported settings from {Path.GetFileName(importPath)}. Rules were not modified.",
                    NotificationType.Information);

                this.Logger.LogInformation("Legacy settings imported from {Path}", importPath);
                await this.LogUserActionAsync("SettingsChanged", "Legacy settings imported", Path.GetFileName(importPath));
            }
            catch (Exception ex)
            {
                this.StatusMessage = this.GetLocalizedString("Settings_StatusErrorImportingFormat", "Error importing settings: {0}", ex.Message);
                this.Logger.LogError(ex, "Error importing settings");

                await this.notificationService.ShowErrorNotificationAsync(
                    "Import Error",
                    "Failed to import configuration",
                    ex);
                await this.LogUserActionAsync("SettingsChangeFailed", $"Failed to import settings: {ex.Message}");
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

                this.StatusMessage = this.GetLocalizedString("Settings_StatusTestSent", "Test notification sent");
                this.Logger.LogInformation("Test notification sent");
            }
            catch (Exception ex)
            {
                this.StatusMessage = this.GetLocalizedString("Settings_StatusErrorTestFormat", "Error sending test notification: {0}", ex.Message);
                this.Logger.LogError(ex, "Error sending test notification");
            }
        }

        public async Task RefreshSettingsAsync()
        {
            try
            {
                this.IsLoading = true;
                this.StatusMessage = this.GetLocalizedString("Settings_StatusLoading", "Loading settings...");

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
                this.ApplyThemePreference(useDarkTheme, logUserAction: false);
                this.ApplyLanguagePreference(this.Settings.Language, logUserAction: false);

                this.SetSavedSettingsSnapshot(this.Settings);
                this.StatusMessage = this.GetLocalizedString("Settings_StatusLoaded", "Settings loaded");

                this.Logger.LogInformation("Settings refreshed");
            }
            catch (Exception ex)
            {
                this.StatusMessage = this.GetLocalizedString("Settings_StatusErrorLoadingFormat", "Error loading settings: {0}", ex.Message);
                this.Logger.LogError(ex, "Error loading settings");
            }
            finally
            {
                this.isSyncingFromService = false;
                this.IsLoading = false;
            }
        }

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
                    this.SetSavedSettingsSnapshot(this.Settings);
                    this.ApplyLanguagePreference(this.Settings.Language, logUserAction: false);
                    this.StatusMessage = this.GetLocalizedString("Settings_StatusSynchronized", "Settings synchronized");
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
                this.StatusMessage = this.GetLocalizedString("Settings_StatusCheckingUpdates", "Checking for updates...");

                var result = await this.updateService.CheckForUpdatesAsync(new UpdateCheckRequest(UpdateCheckTrigger.Manual));
                this.UpdateLastCheckedText();

                if (result.Status == UpdateCheckStatus.Failed)
                {
                    this.StatusMessage = this.GetLocalizedString("Settings_StatusLatestUnknown", "Unable to determine the latest version.");
                    await this.notificationService.ShowErrorNotificationAsync(
                        "Update Check",
                        result.Message);
                    return;
                }

                if (result.IsUpdateAvailable && result.Release != null)
                {
                    this.availableUpdate = result.Release;
                    this.LatestUpdateVersion = $"v{result.Release.Version}";
                    this.IsUpdateAvailable = true;
                    this.StatusMessage = this.GetLocalizedString("Settings_StatusNewVersionFormat", "New version available: {0}", result.Release.Version);
                    await this.notificationService.ShowNotificationAsync(
                        "Update available",
                        $"ThreadPilot {result.Release.Version} is available.",
                        NotificationType.Information);
                }
                else
                {
                    this.availableUpdate = null;
                    this.LatestUpdateVersion = result.Release != null ? $"v{result.Release.Version}" : this.GetLocalizedString("Settings_UpdateLatestUnknown", "Unknown");
                    this.IsUpdateAvailable = false;
                    this.StatusMessage = this.GetLocalizedString("Settings_StatusUpToDateFormat", "Application is up to date. Installed version: {0}", this.ApplicationVersion);
                    await this.notificationService.ShowSuccessNotificationAsync(
                        "Application up to date",
                        $"Installed version: {this.ApplicationVersion}");
                }
            }
            catch (Exception ex)
            {
                this.StatusMessage = this.GetLocalizedString("Settings_StatusUpdateErrorFormat", "Error while checking updates: {0}", ex.Message);
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

        private async Task DownloadAndInstallUpdateAsync()
        {
            if (this.availableUpdate == null)
            {
                this.StatusMessage = this.GetLocalizedString("Settings_StatusLatestUnknown", "Unable to determine the latest version.");
                return;
            }

            var message = this.GetLocalizedString(
                "Settings_UpdateConfirmMessageFormat",
                "ThreadPilot will download and verify version {0}, then ask Windows for permission to run the installer. Continue?",
                this.availableUpdate.Version);
            var confirmation = System.Windows.MessageBox.Show(
                message,
                this.GetLocalizedString("Settings_UpdateConfirmTitle", "Install ThreadPilot update"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (confirmation != MessageBoxResult.Yes)
            {
                this.StatusMessage = this.GetLocalizedString("Settings_StatusUpdateCanceled", "Update canceled.");
                return;
            }

            try
            {
                this.IsLoading = true;
                this.StatusMessage = this.GetLocalizedString("Settings_StatusDownloadingUpdate", "Downloading and verifying update...");

                var result = await this.updateService.DownloadAndInstallAsync(this.availableUpdate);
                if (result.Status == UpdateInstallStatus.Started)
                {
                    this.StatusMessage = this.GetLocalizedString("Settings_StatusUpdateInstallerStarted", "Update installer started.");
                    await this.notificationService.ShowNotificationAsync(
                        "Update installer started",
                        "ThreadPilot will close while the installer runs.",
                        NotificationType.Information);
                }
                else
                {
                    this.StatusMessage = this.GetLocalizedString("Settings_StatusUpdateInstallFailedFormat", "Update install failed: {0}", result.Message);
                    await this.notificationService.ShowErrorNotificationAsync(
                        "Update install failed",
                        result.Message);
                }
            }
            catch (Exception ex)
            {
                this.StatusMessage = this.GetLocalizedString("Settings_StatusUpdateInstallFailedFormat", "Update install failed: {0}", ex.Message);
                this.Logger.LogError(ex, "Error downloading or installing update");
                await this.notificationService.ShowErrorNotificationAsync(
                    "Update install failed",
                    "Unable to download or start the update installer",
                    ex);
            }
            finally
            {
                this.IsLoading = false;
            }
        }

        private void UpdateLastCheckedText()
        {
            var lastCheck = this.settingsService.Settings.LastUpdateCheckUtc;
            this.LastUpdateCheckText = lastCheck.HasValue
                ? lastCheck.Value.LocalDateTime.ToString("g", System.Globalization.CultureInfo.CurrentCulture)
                : this.GetLocalizedString("Settings_UpdateLastCheckedNever", "Never");
        }

        private string GetLocalizedString(string key, string fallback, params object[] args)
        {
            var localized = this.localizationService.GetString(key);
            var format = string.IsNullOrWhiteSpace(localized) || string.Equals(localized, key, StringComparison.Ordinal)
                ? fallback
                : localized;

            return args.Length == 0 ? format : string.Format(format, args);
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

        private void UpdatePendingChangesState()
        {
            this.HasUnsavedChanges = !this.Settings.HasSameUserSettingsAs(this.savedSettingsSnapshot);
            this.StatusMessage = this.HasUnsavedChanges
                ? this.GetLocalizedString("Settings_StatusModified", "Settings have been modified")
                : this.GetLocalizedString("Settings_StatusMatchSaved", "Settings match the saved configuration");
        }

        private void SetSavedSettingsSnapshot(ApplicationSettingsModel settingsSnapshot)
        {
            this.savedSettingsSnapshot = (ApplicationSettingsModel)settingsSnapshot.Clone();
            this.HasUnsavedChanges = false;
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
