namespace ThreadPilot.Services
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using ThreadPilot.Helpers;
    using ThreadPilot.Models;
    using ThreadPilot.Services.Abstractions;

    public class ApplicationSettingsService : IApplicationSettingsService
    {
        private readonly ILogger<ApplicationSettingsService> logger;
        private readonly ISettingsStorage settingsStorage;
        private readonly string settingsFilePath;
        private readonly string? legacySettingsPath;
        private readonly Func<CultureInfo> systemUiCultureProvider;
        private readonly SemaphoreSlim settingsGate = new(1, 1);
        private volatile ApplicationSettingsModel settings;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        public event EventHandler<ApplicationSettingsChangedEventArgs>? SettingsChanged;

        public ApplicationSettingsModel Settings => this.settings.Clone();

        public ApplicationSettingsService(ILogger<ApplicationSettingsService> logger)
            : this(
                logger,
                CreateDefaultStorage(),
                StoragePaths.SettingsFilePath,
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json"),
                () => CultureInfo.CurrentUICulture)
        {
        }

        public ApplicationSettingsService(
            ILogger<ApplicationSettingsService> logger,
            ISettingsStorage settingsStorage,
            string settingsFilePath,
            string? legacySettingsPath,
            Func<CultureInfo>? systemUiCultureProvider = null)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.settingsStorage = settingsStorage ?? throw new ArgumentNullException(nameof(settingsStorage));
            this.settingsFilePath = settingsFilePath ?? throw new ArgumentNullException(nameof(settingsFilePath));
            this.legacySettingsPath = legacySettingsPath;
            this.systemUiCultureProvider = systemUiCultureProvider ?? (() => CultureInfo.CurrentUICulture);

            this.MigrateLegacySettingsIfNeeded();

            this.settings = this.CreateDefaultSettings();
        }

        public async Task LoadSettingsAsync()
        {
            await this.settingsGate.WaitAsync();
            try
            {
                await this.LoadSettingsCoreAsync();
            }
            finally
            {
                this.settingsGate.Release();
            }
        }

        private async Task LoadSettingsCoreAsync()
        {
            try
            {
                this.logger.LogInformation("Loading application settings from {FilePath}", this.settingsFilePath);

                if (!this.settingsStorage.Exists(this.settingsFilePath))
                {
                    this.logger.LogInformation("Settings file not found, using defaults");
                    var defaults = this.CreateDefaultSettings();
                    await this.PersistAsync(defaults);
                    this.settings = defaults;
                    return;
                }

                var json = await this.settingsStorage.ReadAsync(this.settingsFilePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    this.logger.LogWarning("Settings file was empty, using defaults");
                    var defaults = this.CreateDefaultSettings();
                    await this.PersistAsync(defaults);
                    this.settings = defaults;
                    return;
                }

                var legacyThemePreferenceDetected = false;
                var hasLanguagePreference = false;

                try
                {
                    using var document = JsonDocument.Parse(json);
                    if (document.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        var hasThemePreferenceFlag = document.RootElement.TryGetProperty("hasUserThemePreference", out _);
                        var hasUseDarkThemeFlag = document.RootElement.TryGetProperty("useDarkTheme", out _);
                        legacyThemePreferenceDetected = !hasThemePreferenceFlag && hasUseDarkThemeFlag;
                        hasLanguagePreference = document.RootElement.TryGetProperty("language", out var languageElement) &&
                            languageElement.ValueKind == JsonValueKind.String &&
                            !string.IsNullOrWhiteSpace(languageElement.GetString());
                    }
                }
                catch (JsonException ex)
                {
                    this.logger.LogWarning(ex, "Unable to parse settings JSON metadata, continuing with standard deserialization");
                }

                var loadedSettings = JsonSerializer.Deserialize<ApplicationSettingsModel>(json, JsonOptions);

                if (loadedSettings != null)
                {
                    if (legacyThemePreferenceDetected)
                    {
                        loadedSettings.HasUserThemePreference = true;
                    }

                    if (!hasLanguagePreference)
                    {
                        loadedSettings.Language = string.Empty;
                    }

                    var languageIsSupported = LocalizationService.TryNormalizeSupportedLanguage(
                        loadedSettings.Language,
                        out var normalizedLanguage);
                    var requiresLanguageRepair = !languageIsSupported ||
                        !string.Equals(loadedSettings.Language, normalizedLanguage, StringComparison.Ordinal);
                    loadedSettings.Language = LocalizationService.ResolveLanguagePreference(
                        loadedSettings.Language,
                        this.systemUiCultureProvider());

                    var oldSettings = this.settings;
                    this.ValidateAndFixSettings(loadedSettings);

                    var migrationChangedSettings = CpuAssignmentModeMigrationPolicy.Apply(loadedSettings);
                    if (CpuAssignmentModeMigrationPolicy.ShouldShowNotice(loadedSettings))
                    {
                        this.logger.LogInformation(
                            "Migrated the default CPU assignment mode from Automatic to AffinityMask. Automatic applies CPU Sets only, which does not change the affinity mask Windows enforces.");
                    }

                    if (requiresLanguageRepair || migrationChangedSettings)
                    {
                        try
                        {
                            await this.PersistAsync(loadedSettings);
                        }
                        catch (Exception ex)
                        {
                            // Losing the write is recoverable - it is retried on the next launch.
                            // Falling into the outer catch is not: it would replace the profile we
                            // just loaded with defaults and swallow the pending migration notice.
                            this.logger.LogWarning(ex, "Could not persist repaired or migrated settings; keeping the loaded profile for this session");
                        }
                    }

                    this.settings = loadedSettings;
                    this.logger.LogInformation("Settings loaded successfully");
                    this.OnSettingsChanged(oldSettings, loadedSettings);
                }
                else
                {
                    this.logger.LogWarning("Failed to deserialize settings, using defaults");
                    var defaults = this.CreateDefaultSettings();
                    await this.PersistAsync(defaults);
                    this.settings = defaults;
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error loading settings, using defaults");
                this.settings = this.CreateDefaultSettings();
            }
        }

        public Task SaveSettingsAsync() =>
            this.CommitAsync(current => current.Clone(), notify: false);

        public Task UpdateSettingsAsync(ApplicationSettingsModel newSettings)
        {
            ArgumentNullException.ThrowIfNull(newSettings);
            var replacement = newSettings.Clone();
            replacement.Language = LocalizationService.ResolveLanguagePreference(newSettings.Language, this.systemUiCultureProvider());
            return this.CommitAsync(_ => replacement);
        }

        public Task EditSettingsAsync(Action<ApplicationSettingsModel> edit)
        {
            ArgumentNullException.ThrowIfNull(edit);
            return this.CommitAsync(current =>
            {
                var draft = current.Clone();
                edit(draft);
                return draft.Clone();
            });
        }

        public Task ResetToDefaultsAsync() =>
            this.CommitAsync(_ => this.CreateDefaultSettings());

        private async Task CommitAsync(
            Func<ApplicationSettingsModel, ApplicationSettingsModel> createDraft,
            bool notify = true)
        {
            await this.settingsGate.WaitAsync();
            try
            {
                var previous = this.settings;
                var draft = createDraft(previous);
                this.ValidateAndFixSettings(draft);
                await this.PersistAsync(draft);
                this.settings = draft;

                if (notify)
                {
                    this.OnSettingsChanged(previous, draft);
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error saving application settings");
                throw;
            }
            finally
            {
                this.settingsGate.Release();
            }
        }

        private Task PersistAsync(ApplicationSettingsModel draft)
        {
            var json = JsonSerializer.Serialize(draft, JsonOptions);
            return this.settingsStorage.WriteAsync(this.settingsFilePath, json);
        }

        public string GetSettingsFilePath()
        {
            return this.settingsFilePath;
        }

        private void ValidateAndFixSettings(ApplicationSettingsModel draft)
        {
            // Validate and fix notification durations
            if (draft.NotificationDisplayDurationMs < 1000)
            {
                draft.NotificationDisplayDurationMs = 1000;
            }

            if (draft.NotificationDisplayDurationMs > 30000)
            {
                draft.NotificationDisplayDurationMs = 30000;
            }

            if (draft.BalloonNotificationTimeoutMs < 1000)
            {
                draft.BalloonNotificationTimeoutMs = 1000;
            }

            if (draft.BalloonNotificationTimeoutMs > 60000)
            {
                draft.BalloonNotificationTimeoutMs = 60000;
            }

            // Validate notification history
            if (draft.MaxNotificationHistoryItems < 10)
            {
                draft.MaxNotificationHistoryItems = 10;
            }

            if (draft.MaxNotificationHistoryItems > 1000)
            {
                draft.MaxNotificationHistoryItems = 1000;
            }

            // Validate custom icon path
            if (draft.UseCustomTrayIcon && !string.IsNullOrEmpty(draft.CustomTrayIconPath))
            {
                if (!File.Exists(draft.CustomTrayIconPath))
                {
                    this.logger.LogWarning("Custom tray icon file not found: {Path}", draft.CustomTrayIconPath);
                    draft.UseCustomTrayIcon = false;
                }
            }

            draft.Language = LocalizationService.ResolveLanguagePreference(
                draft.Language,
                this.systemUiCultureProvider());

            if (!Enum.IsDefined(draft.DefaultCpuAssignmentMode))
            {
                // Repair to the shipped default rather than Automatic; see CopyFrom.
                draft.DefaultCpuAssignmentMode = CpuAssignmentMode.AffinityMask;
            }
        }

        public async Task ExportSettingsAsync(string filePath)
        {
            try
            {
                this.logger.LogInformation("Exporting settings to {FilePath}", filePath);

                var json = JsonSerializer.Serialize(this.settings, JsonOptions);
                await this.settingsStorage.WriteAsync(filePath, json);

                this.logger.LogInformation("Settings exported successfully");
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error exporting settings");
                throw;
            }
        }

        public async Task ImportSettingsAsync(string filePath)
        {
            try
            {
                this.logger.LogInformation("Importing settings from {FilePath}", filePath);

                if (!this.settingsStorage.Exists(filePath))
                {
                    throw new FileNotFoundException($"Settings file not found: {filePath}");
                }

                var json = await this.settingsStorage.ReadAsync(filePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    throw new InvalidOperationException("Imported settings file was empty");
                }

                var importedSettings = JsonSerializer.Deserialize<ApplicationSettingsModel>(json, JsonOptions);

                if (importedSettings == null)
                {
                    throw new InvalidOperationException("Failed to deserialize imported settings");
                }

                await this.UpdateSettingsAsync(importedSettings);
                this.logger.LogInformation("Settings imported successfully");
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error importing settings");
                throw;
            }
        }

        private void OnSettingsChanged(ApplicationSettingsModel oldSettings, ApplicationSettingsModel newSettings)
        {
            try
            {
                // For simplicity, we'll just indicate that settings changed
                // In a more sophisticated implementation, we could track specific property changes
                var changedProperties = new[] { "Settings" };

                var oldSnapshot = oldSettings.Clone();
                var newSnapshot = newSettings.Clone();

                this.SettingsChanged?.Invoke(this, new ApplicationSettingsChangedEventArgs(
                    oldSnapshot, newSnapshot, changedProperties));
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error firing settings changed event");
            }
        }

        private ApplicationSettingsModel CreateDefaultSettings()
        {
            var defaults = new ApplicationSettingsModel
            {
                Language = LocalizationService.ResolveSystemLanguage(this.systemUiCultureProvider()),

                // A brand new profile already ships with AffinityMask, so there is nothing to
                // migrate and nothing to announce.
                HasMigratedCpuAssignmentModeDefault = true,
                HasSeenCpuAssignmentModeChangeNotice = true,
            };
            return defaults;
        }

        private void MigrateLegacySettingsIfNeeded()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(this.legacySettingsPath) &&
                    this.settingsStorage.Exists(this.legacySettingsPath) &&
                    !this.settingsStorage.Exists(this.settingsFilePath))
                {
                    this.settingsStorage.EnsureDirectoryForFile(this.settingsFilePath);
                    this.settingsStorage.Copy(this.legacySettingsPath, this.settingsFilePath, overwrite: false);
                    this.logger.LogInformation("Migrated legacy settings file to AppData storage");
                }
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Failed to migrate legacy settings file");
            }
        }

        private static FileSettingsStorage CreateDefaultStorage()
        {
            StoragePaths.EnsureAppDataDirectories();
            return new FileSettingsStorage();
        }
    }
}

