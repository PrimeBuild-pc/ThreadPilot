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
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for managing application settings with JSON persistence
    /// </summary>
    public class ApplicationSettingsService : IApplicationSettingsService
    {
        private readonly ILogger<ApplicationSettingsService> _logger;
        private readonly string _settingsFilePath;
        private ApplicationSettingsModel _settings;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        public event EventHandler<ApplicationSettingsChangedEventArgs>? SettingsChanged;

        public ApplicationSettingsModel Settings => (ApplicationSettingsModel)_settings.Clone();

        public ApplicationSettingsService(ILogger<ApplicationSettingsService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            StoragePaths.EnsureAppDataDirectories();
            _settingsFilePath = StoragePaths.SettingsFilePath;

            MigrateLegacySettingsIfNeeded();

            _settings = new ApplicationSettingsModel();
        }

        public async Task LoadSettingsAsync()
        {
            try
            {
                _logger.LogInformation("Loading application settings from {FilePath}", _settingsFilePath);

                if (!File.Exists(_settingsFilePath))
                {
                    _logger.LogInformation("Settings file not found, using defaults");
                    _settings = new ApplicationSettingsModel();
                    await SaveSettingsAsync();
                    return;
                }

                var json = await File.ReadAllTextAsync(_settingsFilePath);
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
                    _logger.LogWarning(ex, "Unable to parse settings JSON metadata, continuing with standard deserialization");
                }

                var loadedSettings = JsonSerializer.Deserialize<ApplicationSettingsModel>(json, JsonOptions);

                if (loadedSettings != null)
                {
                    if (legacyThemePreferenceDetected)
                    {
                        loadedSettings.HasUserThemePreference = true;
                    }

                    var oldSettings = (ApplicationSettingsModel)_settings.Clone();
                    _settings.CopyFrom(loadedSettings);
                    ValidateAndFixSettings();

                    _logger.LogInformation("Settings loaded successfully");
                    OnSettingsChanged(oldSettings, _settings);
                }
                else
                {
                    _logger.LogWarning("Failed to deserialize settings, using defaults");
                    _settings = new ApplicationSettingsModel();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading settings, using defaults");
                _settings = new ApplicationSettingsModel();
            }
        }

        public async Task SaveSettingsAsync()
        {
            try
            {
                _logger.LogDebug("Saving application settings to {FilePath}", _settingsFilePath);

                ValidateAndFixSettings();

                var json = JsonSerializer.Serialize(_settings, JsonOptions);
                await AtomicFileWriter.WriteAllTextAsync(_settingsFilePath, json, Encoding.UTF8);

                _logger.LogDebug("Settings saved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving settings");
                throw;
            }
        }

        public async Task UpdateSettingsAsync(ApplicationSettingsModel newSettings)
        {
            if (newSettings == null)
                throw new ArgumentNullException(nameof(newSettings));

            try
            {
                var oldSettings = (ApplicationSettingsModel)_settings.Clone();
                _settings.CopyFrom(newSettings);
                
                await SaveSettingsAsync();
                
                OnSettingsChanged(oldSettings, _settings);
                _logger.LogInformation("Settings updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating settings");
                throw;
            }
        }

        public async Task ResetToDefaultsAsync()
        {
            try
            {
                _logger.LogInformation("Resetting settings to defaults");
                
                var oldSettings = (ApplicationSettingsModel)_settings.Clone();
                _settings = new ApplicationSettingsModel();
                
                await SaveSettingsAsync();
                
                OnSettingsChanged(oldSettings, _settings);
                _logger.LogInformation("Settings reset to defaults");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting settings to defaults");
                throw;
            }
        }

        public string GetSettingsFilePath()
        {
            return _settingsFilePath;
        }

        public void ValidateAndFixSettings()
        {
            // Validate and fix notification durations
            if (_settings.NotificationDisplayDurationMs < 1000)
                _settings.NotificationDisplayDurationMs = 1000;
            if (_settings.NotificationDisplayDurationMs > 30000)
                _settings.NotificationDisplayDurationMs = 30000;

            if (_settings.BalloonNotificationTimeoutMs < 1000)
                _settings.BalloonNotificationTimeoutMs = 1000;
            if (_settings.BalloonNotificationTimeoutMs > 60000)
                _settings.BalloonNotificationTimeoutMs = 60000;

            // Validate notification history
            if (_settings.MaxNotificationHistoryItems < 10)
                _settings.MaxNotificationHistoryItems = 10;
            if (_settings.MaxNotificationHistoryItems > 1000)
                _settings.MaxNotificationHistoryItems = 1000;

            // Validate custom icon path
            if (_settings.UseCustomTrayIcon && !string.IsNullOrEmpty(_settings.CustomTrayIconPath))
            {
                if (!File.Exists(_settings.CustomTrayIconPath))
                {
                    _logger.LogWarning("Custom tray icon file not found: {Path}", _settings.CustomTrayIconPath);
                    _settings.UseCustomTrayIcon = false;
                }
            }
        }

        public async Task ExportSettingsAsync(string filePath)
        {
            try
            {
                _logger.LogInformation("Exporting settings to {FilePath}", filePath);

                var json = JsonSerializer.Serialize(_settings, JsonOptions);
                await AtomicFileWriter.WriteAllTextAsync(filePath, json, Encoding.UTF8);

                _logger.LogInformation("Settings exported successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting settings");
                throw;
            }
        }

        public async Task ImportSettingsAsync(string filePath)
        {
            try
            {
                _logger.LogInformation("Importing settings from {FilePath}", filePath);

                if (!File.Exists(filePath))
                    throw new FileNotFoundException($"Settings file not found: {filePath}");

                var json = await File.ReadAllTextAsync(filePath);
                var importedSettings = JsonSerializer.Deserialize<ApplicationSettingsModel>(json, JsonOptions);

                if (importedSettings == null)
                    throw new InvalidOperationException("Failed to deserialize imported settings");

                await UpdateSettingsAsync(importedSettings);
                _logger.LogInformation("Settings imported successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing settings");
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
                
                SettingsChanged?.Invoke(this, new ApplicationSettingsChangedEventArgs(
                    oldSnapshot, newSnapshot, changedProperties));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error firing settings changed event");
            }
        }

        private void MigrateLegacySettingsIfNeeded()
        {
            try
            {
                var legacySettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
                if (File.Exists(legacySettingsPath) && !File.Exists(_settingsFilePath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_settingsFilePath)!);
                    File.Copy(legacySettingsPath, _settingsFilePath);
                    _logger.LogInformation("Migrated legacy settings file to AppData storage");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to migrate legacy settings file");
            }
        }
    }
}

