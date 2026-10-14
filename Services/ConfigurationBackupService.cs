namespace ThreadPilot.Services
{
    using System.Diagnostics;
    using System.IO;
    using System.Text.Json;
    using ThreadPilot.Models;

    public sealed class ConfigurationBackupService(
        IApplicationSettingsService settings,
        IProcessPowerPlanAssociationService associations,
        IPersistentProcessRuleStore rules)
    {
        internal static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        public async Task<ConfigurationBackup> CaptureAsync(ApplicationSettingsModel source) => new()
        {
            Settings = Clone(source),
            ProcessMonitorConfiguration = Clone(associations.Configuration),
            PersistentProcessRules = Clone((await rules.LoadAsync()).ToList()),
        };

        public static string Serialize(ConfigurationBackup backup) => JsonSerializer.Serialize(backup, JsonOptions);

        public static ConfigurationBackup? Parse(string json)
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("Configuration must be a JSON object.");
            }

            if (!document.RootElement.EnumerateObject().Any(property => property.Name.Equals("settings", StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            var backup = JsonSerializer.Deserialize<ConfigurationBackup>(json, JsonOptions)
                ?? throw new InvalidDataException("Empty configuration.");
            backup.ProcessMonitorConfiguration ??= backup.RulesConfiguration;
            Validate(backup);
            return backup;
        }

        public static void Validate(ConfigurationBackup backup)
        {
            if (backup.SchemaVersion is not ("2.0" or "3.0") || backup.Settings == null ||
                backup.ProcessMonitorConfiguration?.Associations == null ||
                (backup.SchemaVersion == "3.0" && backup.PersistentProcessRules == null))
            {
                throw new InvalidDataException("Unsupported or incomplete configuration bundle.");
            }

            if (!backup.Settings.Validate().IsValid || backup.ProcessMonitorConfiguration.Validate().Count != 0)
            {
                throw new InvalidDataException("Invalid settings or power plan associations.");
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rule in backup.PersistentProcessRules ?? [])
            {
                if (rule == null || string.IsNullOrWhiteSpace(rule.Id) || !ids.Add(rule.Id) ||
                    (string.IsNullOrWhiteSpace(rule.ProcessName) && string.IsNullOrWhiteSpace(rule.ExecutablePath)) ||
                    !Enum.IsDefined(rule.CpuAssignmentMode) ||
                    (rule.Priority.HasValue && (!Enum.IsDefined(rule.Priority.Value) || rule.Priority == ProcessPriorityClass.RealTime)) ||
                    (rule.MemoryPriority.HasValue && !Enum.IsDefined(rule.MemoryPriority.Value)) ||
                    (rule.IoPriority.HasValue && !Enum.IsDefined(rule.IoPriority.Value)) ||
                    (rule.EfficiencyMode.HasValue && !Enum.IsDefined(rule.EfficiencyMode.Value)) ||
                    (rule.ApplyPriorityOnStart && !rule.Priority.HasValue) ||
                    (rule.ApplyMemoryPriorityOnStart && !rule.MemoryPriority.HasValue) ||
                    (rule.ApplyIoPriorityOnStart && !rule.IoPriority.HasValue) ||
                    (rule.ApplyEfficiencyModeOnStart && !rule.EfficiencyMode.HasValue) ||
                    (rule.ApplyAffinityOnStart && rule.CpuSelection == null && rule.LegacyAffinityMask.GetValueOrDefault() == 0))
                {
                    throw new InvalidDataException("Invalid persistent process rule.");
                }
            }
        }

        public async Task ApplyAsync(ConfigurationBackup backup)
        {
            Validate(backup);
            var previous = await this.CaptureAsync(settings.Settings);
            try
            {
                if (backup.PersistentProcessRules != null)
                {
                    await rules.SaveAsync(backup.PersistentProcessRules);
                }

                if (!await associations.ReplaceConfigurationAsync(backup.ProcessMonitorConfiguration!))
                {
                    throw new IOException("Could not save power plan associations.");
                }

                await settings.UpdateSettingsAsync(backup.Settings!);
            }
            catch (Exception importError)
            {
                var errors = new List<Exception> { importError };
                try
                {
                    await rules.SaveAsync(previous.PersistentProcessRules!);
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }

                try
                {
                    if (!await associations.ReplaceConfigurationAsync(previous.ProcessMonitorConfiguration!))
                    {
                        throw new IOException("Could not restore power plan associations.");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }

                try
                {
                    await settings.UpdateSettingsAsync(previous.Settings!);
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }

                if (errors.Count > 1)
                {
                    throw new AggregateException("Import failed and restoration was incomplete.", errors);
                }

                throw;
            }
        }

        private static T Clone<T>(T source) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(source, JsonOptions), JsonOptions)!;
    }

    public sealed class ConfigurationBackup
    {
        public string SchemaVersion { get; set; } = "3.0";

        public DateTime ExportedAtUtc { get; set; } = DateTime.UtcNow;

        public ApplicationSettingsModel? Settings { get; set; }

        public ProcessMonitorConfiguration? ProcessMonitorConfiguration { get; set; }

        public ProcessMonitorConfiguration? RulesConfiguration { get; set; }

        public List<PersistentProcessRule>? PersistentProcessRules { get; set; }
    }
}
