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
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using ThreadPilot.Models;

    /// <summary>
    /// Service for managing process-power plan associations with persistence.
    /// </summary>
    public class ProcessPowerPlanAssociationService : IProcessPowerPlanAssociationService
    {
        private static string LegacyConfigurationDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configuration");

        private static string LegacyConfigurationFilePath => Path.Combine(LegacyConfigurationDirectory, "ProcessPowerPlanAssociations.json");

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        private readonly string configurationDirectory;
        private readonly string configurationFilePath;
        private readonly object lockObject = new();

        private ProcessMonitorConfiguration configuration;

        public event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;

        public ProcessMonitorConfiguration Configuration => this.configuration;

        public ProcessPowerPlanAssociationService()
        {
            StoragePaths.EnsureAppDataDirectories();

            this.configurationDirectory = StoragePaths.ConfigurationDirectory;
            this.configurationFilePath = Path.Combine(this.configurationDirectory, "ProcessPowerPlanAssociations.json");
            this.configuration = new ProcessMonitorConfiguration();

            this.EnsureConfigurationDirectoryExists();
            this.MigrateLegacyConfigurationIfNeeded();
        }

        public async Task<bool> LoadConfigurationAsync()
        {
            try
            {
                if (!File.Exists(this.configurationFilePath))
                {
                    // Create default configuration
                    this.configuration = new ProcessMonitorConfiguration();
                    await this.SaveConfigurationAsync().ConfigureAwait(false);
                    return true;
                }

                var json = await File.ReadAllTextAsync(this.configurationFilePath).ConfigureAwait(false);
                var loadedConfig = JsonSerializer.Deserialize<ProcessMonitorConfiguration>(json, JsonOptions);

                if (loadedConfig != null)
                {
                    lock (this.lockObject)
                    {
                        loadedConfig.Associations ??= new List<ProcessPowerPlanAssociation>();
                        this.configuration = loadedConfig;
                    }

                    this.OnConfigurationChanged("Loaded", null, "Configuration loaded from file");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                this.OnConfigurationChanged("LoadError", null, $"Failed to load configuration: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SaveConfigurationAsync()
        {
            try
            {
                ProcessMonitorConfiguration configToSave;
                lock (this.lockObject)
                {
                    configToSave = this.configuration;
                    configToSave.LastSavedDate = DateTime.Now;
                }

                var json = JsonSerializer.Serialize(configToSave, JsonOptions);
                await AtomicFileWriter.WriteAllTextAsync(this.configurationFilePath, json, Encoding.UTF8).ConfigureAwait(false);

                this.OnConfigurationChanged("Saved", null, "Configuration saved to file");
                return true;
            }
            catch (Exception ex)
            {
                this.OnConfigurationChanged("SaveError", null, $"Failed to save configuration: {ex.Message}");
                return false;
            }
        }

        public async Task<IEnumerable<ProcessPowerPlanAssociation>> GetAssociationsAsync()
        {
            await Task.CompletedTask.ConfigureAwait(false);
            lock (this.lockObject)
            {
                return this.configuration.Associations.ToList();
            }
        }

        public async Task<IEnumerable<ProcessPowerPlanAssociation>> GetEnabledAssociationsAsync()
        {
            await Task.CompletedTask.ConfigureAwait(false);
            lock (this.lockObject)
            {
                return this.configuration.GetEnabledAssociations().ToList();
            }
        }

        public async Task<bool> AddAssociationAsync(ProcessPowerPlanAssociation association)
        {
            try
            {
                if (association == null)
                {
                    return false;
                }

                lock (this.lockObject)
                {
                    // Check for duplicates
                    var existing = this.configuration.Associations
                        .FirstOrDefault(a => AreAssociationsConflicting(a, association));

                    if (existing != null)
                    {
                        return false; // Duplicate found
                    }

                    this.configuration.AddOrUpdateAssociation(association);
                }

                await this.SaveConfigurationAsync().ConfigureAwait(false);
                this.OnConfigurationChanged("Added", association, $"Association added for {association.ExecutableName}");
                return true;
            }
            catch (Exception ex)
            {
                this.OnConfigurationChanged("AddError", association, $"Failed to add association: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateAssociationAsync(ProcessPowerPlanAssociation association)
        {
            try
            {
                if (association == null)
                {
                    return false;
                }

                lock (this.lockObject)
                {
                    this.configuration.AddOrUpdateAssociation(association);
                }

                await this.SaveConfigurationAsync().ConfigureAwait(false);
                this.OnConfigurationChanged("Updated", association, $"Association updated for {association.ExecutableName}");
                return true;
            }
            catch (Exception ex)
            {
                this.OnConfigurationChanged("UpdateError", association, $"Failed to update association: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> RemoveAssociationAsync(string associationId)
        {
            try
            {
                ProcessPowerPlanAssociation? removedAssociation = null;
                bool removed;

                lock (this.lockObject)
                {
                    removedAssociation = this.configuration.Associations.FirstOrDefault(a => a.Id == associationId);
                    removed = this.configuration.RemoveAssociation(associationId);
                }

                if (removed)
                {
                    await this.SaveConfigurationAsync().ConfigureAwait(false);
                    this.OnConfigurationChanged("Removed", removedAssociation,
                        $"Association removed for {removedAssociation?.ExecutableName}");
                }

                return removed;
            }
            catch (Exception ex)
            {
                this.OnConfigurationChanged("RemoveError", null, $"Failed to remove association: {ex.Message}");
                return false;
            }
        }

        public async Task<ProcessPowerPlanAssociation?> FindMatchingAssociationAsync(ProcessModel process)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            lock (this.lockObject)
            {
                return this.configuration.FindMatchingAssociation(process);
            }
        }

        public async Task<ProcessPowerPlanAssociation?> FindAssociationByExecutableAsync(string executableName)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            lock (this.lockObject)
            {
                return this.configuration.FindAssociationByExecutable(executableName);
            }
        }

        public async Task<bool> SetDefaultPowerPlanAsync(string powerPlanGuid, string powerPlanName)
        {
            try
            {
                lock (this.lockObject)
                {
                    this.configuration.DefaultPowerPlanGuid = powerPlanGuid;
                    this.configuration.DefaultPowerPlanName = powerPlanName;
                }

                await this.SaveConfigurationAsync().ConfigureAwait(false);
                this.OnConfigurationChanged("DefaultPowerPlanChanged", null, $"Default power plan set to {powerPlanName}");
                return true;
            }
            catch (Exception ex)
            {
                this.OnConfigurationChanged("DefaultPowerPlanError", null, $"Failed to set default power plan: {ex.Message}");
                return false;
            }
        }

        public async Task<(string Guid, string Name)> GetDefaultPowerPlanAsync()
        {
            await Task.CompletedTask.ConfigureAwait(false);
            lock (this.lockObject)
            {
                return (this.configuration.DefaultPowerPlanGuid, this.configuration.DefaultPowerPlanName);
            }
        }

        public async Task<IEnumerable<string>> ValidateConfigurationAsync()
        {
            await Task.CompletedTask.ConfigureAwait(false);
            lock (this.lockObject)
            {
                return this.configuration.Validate();
            }
        }

        public async Task ResetConfigurationAsync()
        {
            lock (this.lockObject)
            {
                this.configuration = new ProcessMonitorConfiguration();
            }

            await this.SaveConfigurationAsync().ConfigureAwait(false);
            this.OnConfigurationChanged("Reset", null, "Configuration reset to defaults");
        }

        public async Task<bool> ExportConfigurationAsync(string filePath)
        {
            try
            {
                ProcessMonitorConfiguration configToExport;
                lock (this.lockObject)
                {
                    configToExport = this.configuration;
                }

                var json = JsonSerializer.Serialize(configToExport, JsonOptions);
                await AtomicFileWriter.WriteAllTextAsync(filePath, json, Encoding.UTF8).ConfigureAwait(false);

                this.OnConfigurationChanged("Exported", null, $"Configuration exported to {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                this.OnConfigurationChanged("ExportError", null, $"Failed to export configuration: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ImportConfigurationAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return false;
                }

                var json = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
                var importedConfig = JsonSerializer.Deserialize<ProcessMonitorConfiguration>(json, JsonOptions);

                if (importedConfig != null)
                {
                    var replaced = await this.ReplaceConfigurationAsync(importedConfig).ConfigureAwait(false);
                    if (replaced)
                    {
                        this.OnConfigurationChanged("Imported", null, $"Configuration imported from {filePath}");
                    }

                    return replaced;
                }

                return false;
            }
            catch (Exception ex)
            {
                this.OnConfigurationChanged("ImportError", null, $"Failed to import configuration: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ReplaceConfigurationAsync(ProcessMonitorConfiguration configuration)
        {
            if (configuration == null)
            {
                return false;
            }

            try
            {
                var cloned = CloneConfiguration(configuration);

                lock (this.lockObject)
                {
                    this.configuration = cloned;
                }

                await this.SaveConfigurationAsync().ConfigureAwait(false);
                this.OnConfigurationChanged("Replaced", null, "Configuration replaced from imported bundle");
                return true;
            }
            catch (Exception ex)
            {
                this.OnConfigurationChanged("ReplaceError", null, $"Failed to replace configuration: {ex.Message}");
                return false;
            }
        }

        private void EnsureConfigurationDirectoryExists()
        {
            if (!Directory.Exists(this.configurationDirectory))
            {
                Directory.CreateDirectory(this.configurationDirectory);
            }
        }

        private void MigrateLegacyConfigurationIfNeeded()
        {
            try
            {
                if (!File.Exists(LegacyConfigurationFilePath) || File.Exists(this.configurationFilePath))
                {
                    return;
                }

                Directory.CreateDirectory(this.configurationDirectory);
                File.Copy(LegacyConfigurationFilePath, this.configurationFilePath);
            }
            catch
            {
                // Ignore migration failures and continue with current storage path.
            }
        }

        private void OnConfigurationChanged(string changeType, ProcessPowerPlanAssociation? association = null, string? details = null)
        {
            this.ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs(changeType, association, details));
        }

        private static bool AreAssociationsConflicting(ProcessPowerPlanAssociation existing, ProcessPowerPlanAssociation candidate)
        {
            var existingName = NormalizeExecutableName(existing.ExecutableName);
            var candidateName = NormalizeExecutableName(candidate.ExecutableName);

            if (!string.Equals(existingName, candidateName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (existing.MatchByPath || candidate.MatchByPath)
            {
                var existingPath = existing.ExecutablePath?.Trim() ?? string.Empty;
                var candidatePath = candidate.ExecutablePath?.Trim() ?? string.Empty;

                return string.Equals(existingPath, candidatePath, StringComparison.OrdinalIgnoreCase) &&
                       existing.MatchByPath == candidate.MatchByPath;
            }

            // Name-only associations conflict by executable name
            return true;
        }

        private static string NormalizeExecutableName(string? executableName)
        {
            if (string.IsNullOrWhiteSpace(executableName))
            {
                return string.Empty;
            }

            return Path.GetFileNameWithoutExtension(executableName.Trim());
        }

        private static ProcessMonitorConfiguration CloneConfiguration(ProcessMonitorConfiguration source)
        {
            var serialized = JsonSerializer.Serialize(source, JsonOptions);
            var cloned = JsonSerializer.Deserialize<ProcessMonitorConfiguration>(serialized, JsonOptions)
                ?? new ProcessMonitorConfiguration();
            cloned.Associations ??= new List<ProcessPowerPlanAssociation>();
            return cloned;
        }
    }
}

