namespace ThreadPilot.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using ThreadPilot.Models;

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
        private readonly SemaphoreSlim mutationGate = new(1, 1);

        private volatile ProcessMonitorConfiguration configuration = new();

        public event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;

        public ProcessMonitorConfiguration Configuration => this.configuration;

        public ProcessPowerPlanAssociationService()
            : this(StoragePaths.ConfigurationDirectory)
        {
            StoragePaths.EnsureAppDataDirectories();
            this.MigrateLegacyConfigurationIfNeeded();
        }

        internal ProcessPowerPlanAssociationService(string configurationDirectory)
        {
            this.configurationDirectory = configurationDirectory;
            this.configurationFilePath = Path.Combine(configurationDirectory, "ProcessPowerPlanAssociations.json");
        }

        public async Task<bool> LoadConfigurationAsync()
        {
            await this.mutationGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (File.Exists(this.configurationFilePath))
                {
                    var json = await File.ReadAllTextAsync(this.configurationFilePath).ConfigureAwait(false);
                    var loaded = JsonSerializer.Deserialize<ProcessMonitorConfiguration>(json, JsonOptions);
                    if (loaded == null)
                    {
                        return false;
                    }

                    loaded.Associations ??= new List<ProcessPowerPlanAssociation>();
                    this.configuration = loaded;
                }
                else
                {
                    await this.PersistAsync(new ProcessMonitorConfiguration()).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                this.OnConfigurationChanged("LoadError", null, $"Failed to load configuration: {ex.Message}");
                return false;
            }
            finally
            {
                this.mutationGate.Release();
            }

            this.OnConfigurationChanged("Loaded", null, "Configuration loaded");
            return true;
        }

        public Task<bool> SaveMonitoringSettingsAsync(bool preventDuplicateChanges, int delayMs) =>
            this.CommitAsync(
                config =>
                {
                    config.PreventDuplicatePowerPlanChanges = preventDuplicateChanges;
                    config.PowerPlanChangeDelayMs = delayMs;
                    return config;
                },
                "Saved");

        public Task<IEnumerable<ProcessPowerPlanAssociation>> GetAssociationsAsync() =>
            Task.FromResult<IEnumerable<ProcessPowerPlanAssociation>>(this.configuration.Associations.Select(association => association.CreateSnapshot()).ToList());

        public Task<IEnumerable<ProcessPowerPlanAssociation>> GetEnabledAssociationsAsync() =>
            Task.FromResult<IEnumerable<ProcessPowerPlanAssociation>>(this.configuration.GetEnabledAssociations().Select(association => association.CreateSnapshot()).ToList());

        public Task<bool> AddAssociationAsync(ProcessPowerPlanAssociation association) =>
            this.CommitAsync(
                config =>
                {
                    if (association == null || config.Associations.Any(existing => AreAssociationsConflicting(existing, association)))
                    {
                        return null;
                    }

                    config.AddOrUpdateAssociation(association.CreateSnapshot());
                    return config;
                },
                "Added",
                association);

        public Task<bool> UpdateAssociationAsync(ProcessPowerPlanAssociation association) =>
            this.CommitAsync(
                config =>
                {
                    if (association == null)
                    {
                        return null;
                    }

                    config.AddOrUpdateAssociation(association.CreateSnapshot());
                    return config;
                },
                "Updated",
                association);

        public Task<bool> RemoveAssociationAsync(string associationId) =>
            this.CommitAsync(config => config.RemoveAssociation(associationId) ? config : null, "Removed");

        public Task<ProcessPowerPlanAssociation?> FindMatchingAssociationAsync(ProcessModel process) =>
            Task.FromResult(this.configuration.FindMatchingAssociation(process)?.CreateSnapshot());

        public Task<ProcessPowerPlanAssociation?> FindAssociationByExecutableAsync(string executableName) =>
            Task.FromResult(this.configuration.FindAssociationByExecutable(executableName)?.CreateSnapshot());

        public Task<bool> SetDefaultPowerPlanAsync(string powerPlanGuid, string powerPlanName) =>
            this.CommitAsync(
                config =>
                {
                    config.DefaultPowerPlanGuid = powerPlanGuid;
                    config.DefaultPowerPlanName = powerPlanName;
                    return config;
                },
                "DefaultPowerPlanChanged");

        public Task<(string Guid, string Name)> GetDefaultPowerPlanAsync()
        {
            var config = this.configuration;
            return Task.FromResult((config.DefaultPowerPlanGuid, config.DefaultPowerPlanName));
        }

        public Task<IEnumerable<string>> ValidateConfigurationAsync() =>
            Task.FromResult<IEnumerable<string>>(this.configuration.Validate());

        public Task<bool> ResetConfigurationAsync() =>
            this.CommitAsync(_ => new ProcessMonitorConfiguration(), "Reset");

        public async Task<bool> ExportConfigurationAsync(string filePath)
        {
            try
            {
                var json = JsonSerializer.Serialize(this.configuration, JsonOptions);
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

        public Task<bool> ReplaceConfigurationAsync(ProcessMonitorConfiguration configuration) =>
            this.CommitAsync(_ => configuration == null ? null : CloneConfiguration(configuration), "Replaced");

        private async Task<bool> CommitAsync(
            Func<ProcessMonitorConfiguration, ProcessMonitorConfiguration?> change,
            string changeType,
            ProcessPowerPlanAssociation? association = null)
        {
            await this.mutationGate.WaitAsync().ConfigureAwait(false);
            try
            {
                var proposed = change(CloneConfiguration(this.configuration));
                if (proposed == null)
                {
                    return false;
                }

                await this.PersistAsync(proposed).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this.OnConfigurationChanged("SaveError", association, $"Failed to save configuration: {ex.Message}");
                return false;
            }
            finally
            {
                this.mutationGate.Release();
            }

            this.OnConfigurationChanged(changeType, association);
            return true;
        }

        private async Task PersistAsync(ProcessMonitorConfiguration configuration)
        {
            configuration.LastSavedDate = DateTime.Now;
            var json = JsonSerializer.Serialize(configuration, JsonOptions);
            await AtomicFileWriter.WriteAllTextAsync(this.configurationFilePath, json, Encoding.UTF8).ConfigureAwait(false);
            this.configuration = configuration;
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

