namespace ThreadPilot.Services
{
    using Microsoft.Extensions.Logging;

    public sealed class ActivityAuditService : IActivityAuditService
    {
        private const int MaxEntries = 1000;
        private readonly ILogger<ActivityAuditService> logger;
        private readonly object syncRoot = new();
        private readonly List<ActivityAuditEntry> entries = new();

        public event EventHandler<ActivityAuditEntry>? EntryAdded;

        public ActivityAuditService(ILogger<ActivityAuditService> logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task LogInfoAsync(string category, string message, string? details = null) =>
            this.AddEntryAsync(category, ActivityAuditSeverity.Info, message, details);

        public Task LogSuccessAsync(string category, string message, string? details = null) =>
            this.AddEntryAsync(category, ActivityAuditSeverity.Success, message, details);

        public Task LogWarningAsync(string category, string message, string? details = null) =>
            this.AddEntryAsync(category, ActivityAuditSeverity.Warning, message, details);

        public Task LogErrorAsync(string category, string message, string? details = null) =>
            this.AddEntryAsync(category, ActivityAuditSeverity.Error, message, details);

        public Task LogUserActionAsync(string action, string details, string? context = null)
        {
            var entry = ActivityAuditActionMapper.Map(action, details, context);
            return this.AddEntryAsync(entry.Category, entry.Severity, entry.Message, entry.Details);
        }

        public Task<IReadOnlyList<ActivityAuditEntry>> GetEntriesAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
            lock (this.syncRoot)
            {
                IEnumerable<ActivityAuditEntry> snapshot = this.entries;
                if (fromDate.HasValue)
                {
                    snapshot = snapshot.Where(entry => entry.Timestamp >= fromDate.Value);
                }

                if (toDate.HasValue)
                {
                    snapshot = snapshot.Where(entry => entry.Timestamp <= toDate.Value);
                }

                return Task.FromResult<IReadOnlyList<ActivityAuditEntry>>(
                    snapshot
                        .OrderByDescending(entry => entry.Timestamp)
                        .ToList());
            }
        }

        public Task ClearDisplayAsync()
        {
            lock (this.syncRoot)
            {
                this.entries.Clear();
            }

            return Task.CompletedTask;
        }

        private Task AddEntryAsync(string category, ActivityAuditSeverity severity, string message, string? details)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return Task.CompletedTask;
            }

            var entry = new ActivityAuditEntry
            {
                Timestamp = DateTime.Now,
                Category = string.IsNullOrWhiteSpace(category) ? ActivityAuditCategories.Diagnostics : category.Trim(),
                Severity = severity,
                Message = message.Trim(),
                Details = string.IsNullOrWhiteSpace(details) ? null : details.Trim(),
            };

            lock (this.syncRoot)
            {
                this.entries.Add(entry);
                if (this.entries.Count > MaxEntries)
                {
                    this.entries.RemoveRange(0, this.entries.Count - MaxEntries);
                }
            }

            this.logger.Log(
                ToLogLevel(severity),
                "Activity audit: {Category} {Severity}: {Message}",
                entry.Category,
                entry.Severity,
                entry.Message);
            this.EntryAdded?.Invoke(this, entry);
            return Task.CompletedTask;
        }

        private static LogLevel ToLogLevel(ActivityAuditSeverity severity) =>
            severity switch
            {
                ActivityAuditSeverity.Error => LogLevel.Error,
                ActivityAuditSeverity.Warning => LogLevel.Warning,
                _ => LogLevel.Information,
            };
    }

    internal static class ActivityAuditCategories
    {
        public const string Process = "Process";
        public const string Affinity = "Affinity";
        public const string Priority = "Priority";
        public const string MemoryPriority = "Memory Priority";
        public const string Rules = "Rules";
        public const string PowerPlans = "Power Plans";
        public const string Settings = "Settings";
        public const string Tweaks = "Tweaks";
        public const string Optimization = "Optimization";
        public const string Diagnostics = "Diagnostics";
        public const string Safety = "Safety";
    }

    internal static class ActivityAuditActionMapper
    {
        public static ActivityAuditEntry Map(string action, string details, string? context)
        {
            var category = ResolveCategory(action);
            var severity = ResolveSeverity(action, details);
            return new ActivityAuditEntry
            {
                Category = category,
                Severity = severity,
                Message = string.IsNullOrWhiteSpace(details) ? action : details,
                Details = context,
            };
        }

        private static string ResolveCategory(string action)
        {
            if (action.StartsWith("ProcessAffinity", StringComparison.OrdinalIgnoreCase) ||
                action.StartsWith("CpuSets", StringComparison.OrdinalIgnoreCase))
            {
                return ActivityAuditCategories.Affinity;
            }

            if (action.StartsWith("ProcessPriority", StringComparison.OrdinalIgnoreCase))
            {
                return ActivityAuditCategories.Priority;
            }

            if (action.StartsWith("ProcessMemoryPriority", StringComparison.OrdinalIgnoreCase))
            {
                return ActivityAuditCategories.MemoryPriority;
            }

            if (action.StartsWith("PersistentRule", StringComparison.OrdinalIgnoreCase) ||
                action.Contains("Association", StringComparison.OrdinalIgnoreCase))
            {
                return ActivityAuditCategories.Rules;
            }

            if (action.StartsWith("PowerPlan", StringComparison.OrdinalIgnoreCase) ||
                action.StartsWith("PowerPlans", StringComparison.OrdinalIgnoreCase))
            {
                return ActivityAuditCategories.PowerPlans;
            }

            if (action.StartsWith("Theme", StringComparison.OrdinalIgnoreCase) ||
                action.StartsWith("Settings", StringComparison.OrdinalIgnoreCase) ||
                action.Contains("Configuration", StringComparison.OrdinalIgnoreCase))
            {
                return ActivityAuditCategories.Settings;
            }

            if (action.StartsWith("SystemTweak", StringComparison.OrdinalIgnoreCase) ||
                action.Contains("IdleServer", StringComparison.OrdinalIgnoreCase) ||
                action.Contains("RegistryPriority", StringComparison.OrdinalIgnoreCase))
            {
                return ActivityAuditCategories.Tweaks;
            }

            if (action.StartsWith("Optimization", StringComparison.OrdinalIgnoreCase))
            {
                return ActivityAuditCategories.Optimization;
            }

            if (action.Contains("Protected", StringComparison.OrdinalIgnoreCase) ||
                action.Contains("Elevation", StringComparison.OrdinalIgnoreCase))
            {
                return ActivityAuditCategories.Safety;
            }

            if (action.StartsWith("Process", StringComparison.OrdinalIgnoreCase))
            {
                return ActivityAuditCategories.Process;
            }

            return ActivityAuditCategories.Diagnostics;
        }

        private static ActivityAuditSeverity ResolveSeverity(string action, string details)
        {
            if (ContainsAny(action, "Blocked", "Denied") || ContainsAny(details, "blocked", "denied", "anti-cheat", "protected"))
            {
                return ActivityAuditSeverity.Warning;
            }

            if (ContainsAny(action, "Failed", "Failure", "Error") || ContainsAny(details, "failed", "error", "exited"))
            {
                return ActivityAuditSeverity.Error;
            }

            if (ContainsAny(
                action,
                "Applied",
                "Changed",
                "Saved",
                "Updated",
                "Deleted",
                "Imported",
                "Added",
                "Cleared",
                "Refreshed",
                "Started",
                "Stopped",
                "Exported",
                "Opened",
                "Copied"))
            {
                return ActivityAuditSeverity.Success;
            }

            return ActivityAuditSeverity.Info;
        }

        private static bool ContainsAny(string value, params string[] terms) =>
            terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}
