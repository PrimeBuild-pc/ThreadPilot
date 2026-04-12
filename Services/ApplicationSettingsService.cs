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
namespace ThreadPilot.Services
{
    using System;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using ThreadPilot.Models;

    /// <summary>
    /// Service for managing application settings with JSON persistence.
    /// </summary>
    public class ApplicationSettingsService : IApplicationSettingsService
    {
        private readonly ILogger<ApplicationSettingsService> logger;
        private readonly string settingsFilePath;
        private ApplicationSettingsModel settings;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        public event EventHandler<ApplicationSettingsChangedEventArgs>? SettingsChanged;

        public ApplicationSettingsModel Settings => (ApplicationSettingsModel)this.settings.Clone();

        public ApplicationSettingsService(ILogger<ApplicationSettingsService> logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

            StoragePaths.EnsureAppDataDirectories();
            this.settingsFilePath = StoragePaths.SettingsFilePath;

            this.MigrateLegacySettingsIfNeeded();

            this.settings = new ApplicationSettingsModel();
        }

        public async Task LoadSettingsAsync()
        {
            try
            {
                this.logger.LogInformation("Loading application settings from {FilePath}", this.settingsFilePath);

                if (!File.Exists(this.settingsFilePath))
                {
                    this.logger.LogInformation("Settings file not found, using defaults");
                    this.settings = new ApplicationSettingsModel();
                    await this.SaveSettingsAsync();
                    return;
                }

                var json = await File.ReadAllTextAsync(this.settingsFilePath);
                var legacyThemePreferenceDetected = false;

                try
                {
                    using var document = JsonDocument.Parse(json);
                    if (document.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        var hasThemePreferenceFlag = document.RootElement.TryGetProperty("hasUserThemePreference", out _);
                        var hasUseDarkThemeFlag = document.RootElement.TryGetProperty("useDarkTheme", out _);
                        legacyThemePreferenceDetected = !hasThemePreferenceFlag && hasUseDarkThemeFlag;
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

                    var oldSettings = (ApplicationSettingsModel)this.settings.Clone();
                    this.settings.CopyFrom(loadedSettings);
                    this.ValidateAndFixSettings();

                    this.logger.LogInformation("Settings loaded successfully");
                    this.OnSettingsChanged(oldSettings, this.settings);
                }
                else
                {
                    this.logger.LogWarning("Failed to deserialize settings, using defaults");
                    this.settings = new ApplicationSettingsModel();
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error loading settings, using defaults");
                this.settings = new ApplicationSettingsModel();
            }
        }

        public async Task SaveSettingsAsync()
        {
            try
            {
                this.logger.LogDebug("Saving application settings to {FilePath}", this.settingsFilePath);

                this.ValidateAndFixSettings();

                var json = JsonSerializer.Serialize(this.settings, JsonOptions);
                await AtomicFileWriter.WriteAllTextAsync(this.settingsFilePath, json, Encoding.UTF8);

                this.logger.LogDebug("Settings saved successfully");
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error saving settings");
                throw;
            }
        }

        public async Task UpdateSettingsAsync(ApplicationSettingsModel newSettings)
        {
            if (newSettings == null)
            {
                throw new ArgumentNullException(nameof(newSettings));
            }

            try
            {
                var oldSettings = (ApplicationSettingsModel)this.settings.Clone();
                this.settings.CopyFrom(newSettings);

                await this.SaveSettingsAsync();

                this.OnSettingsChanged(oldSettings, this.settings);
                this.logger.LogInformation("Settings updated successfully");
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error updating settings");
                throw;
            }
        }

        public async Task ResetToDefaultsAsync()
        {
            try
            {
                this.logger.LogInformation("Resetting settings to defaults");

                var oldSettings = (ApplicationSettingsModel)this.settings.Clone();
                this.settings = new ApplicationSettingsModel();

                await this.SaveSettingsAsync();

                this.OnSettingsChanged(oldSettings, this.settings);
                this.logger.LogInformation("Settings reset to defaults");
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error resetting settings to defaults");
                throw;
            }
        }

        public string GetSettingsFilePath()
        {
            return this.settingsFilePath;
        }

        public void ValidateAndFixSettings()
        {
            // Validate and fix notification durations
            if (this.settings.NotificationDisplayDurationMs < 1000)
            {
                this.settings.NotificationDisplayDurationMs = 1000;
            }

            if (this.settings.NotificationDisplayDurationMs > 30000)
            {
                this.settings.NotificationDisplayDurationMs = 30000;
            }

            if (this.settings.BalloonNotificationTimeoutMs < 1000)
            {
                this.settings.BalloonNotificationTimeoutMs = 1000;
            }

            if (this.settings.BalloonNotificationTimeoutMs > 60000)
            {
                this.settings.BalloonNotificationTimeoutMs = 60000;
            }

            // Validate notification history
            if (this.settings.MaxNotificationHistoryItems < 10)
            {
                this.settings.MaxNotificationHistoryItems = 10;
            }

            if (this.settings.MaxNotificationHistoryItems > 1000)
            {
                this.settings.MaxNotificationHistoryItems = 1000;
            }

            // Validate custom icon path
            if (this.settings.UseCustomTrayIcon && !string.IsNullOrEmpty(this.settings.CustomTrayIconPath))
            {
                if (!File.Exists(this.settings.CustomTrayIconPath))
                {
                    this.logger.LogWarning("Custom tray icon file not found: {Path}", this.settings.CustomTrayIconPath);
                    this.settings.UseCustomTrayIcon = false;
                }
            }
        }

        public async Task ExportSettingsAsync(string filePath)
        {
            try
            {
                this.logger.LogInformation("Exporting settings to {FilePath}", filePath);

                var json = JsonSerializer.Serialize(this.settings, JsonOptions);
                await AtomicFileWriter.WriteAllTextAsync(filePath, json, Encoding.UTF8);

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

                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"Settings file not found: {filePath}");
                }

                var json = await File.ReadAllTextAsync(filePath);
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

                var oldSnapshot = (ApplicationSettingsModel)oldSettings.Clone();
                var newSnapshot = (ApplicationSettingsModel)newSettings.Clone();

                this.SettingsChanged?.Invoke(this, new ApplicationSettingsChangedEventArgs(
                    oldSnapshot, newSnapshot, changedProperties));
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error firing settings changed event");
            }
        }

        private void MigrateLegacySettingsIfNeeded()
        {
            try
            {
                var legacySettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
                if (File.Exists(legacySettingsPath) && !File.Exists(this.settingsFilePath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(this.settingsFilePath)!);
                    File.Copy(legacySettingsPath, this.settingsFilePath);
                    this.logger.LogInformation("Migrated legacy settings file to AppData storage");
                }
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Failed to migrate legacy settings file");
            }
        }
    }
}

