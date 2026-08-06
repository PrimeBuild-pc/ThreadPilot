/*
 * ThreadPilot - JSON-backed persistent process rule store.
 */
namespace ThreadPilot.Services
{
    using System.IO;
    using System.Text.Json;
    using Microsoft.Extensions.Logging;
    using ThreadPilot.Models;

    public sealed class PersistentProcessRuleJsonStore : IPersistentProcessRuleStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
        };

        private readonly Func<string> filePathProvider;
        private readonly ILogger<PersistentProcessRuleJsonStore>? logger;
        private readonly SemaphoreSlim cacheLock = new(1, 1);
        private volatile IReadOnlyList<PersistentProcessRule>? cachedRules;
        private bool loadFailed;

        public PersistentProcessRuleJsonStore(ILogger<PersistentProcessRuleJsonStore>? logger = null)
            : this(() => StoragePaths.PersistentRulesFilePath, logger)
        {
        }

        internal PersistentProcessRuleJsonStore(
            Func<string> filePathProvider,
            ILogger<PersistentProcessRuleJsonStore>? logger = null)
        {
            this.filePathProvider = filePathProvider ?? throw new ArgumentNullException(nameof(filePathProvider));
            this.logger = logger;
        }

        public async Task<IReadOnlyList<PersistentProcessRule>> LoadAsync()
        {
            if (this.cachedRules != null)
            {
                return this.cachedRules;
            }

            await this.cacheLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (this.cachedRules != null)
                {
                    return this.cachedRules;
                }

                var filePath = this.filePathProvider();
                this.logger?.LogDebug("Loading persistent process rules from {FilePath}", filePath);
                if (!File.Exists(filePath))
                {
                    this.logger?.LogDebug("Persistent process rules file does not exist at {FilePath}", filePath);
                    return this.cachedRules = [];
                }

                try
                {
                    var json = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
                    var rules = JsonSerializer.Deserialize<List<PersistentProcessRule>>(json, JsonOptions) ?? [];
                    this.logger?.LogDebug("Loaded {RuleCount} persistent process rules from {FilePath}", rules.Count, filePath);
                    this.loadFailed = false;
                    return this.cachedRules = rules.ToArray();
                }
                catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
                {
                    this.loadFailed = true;
                    this.TryPreserveUnreadableFile(filePath, ex);
                    this.logger?.LogWarning(ex, "Could not load persistent process rules from {FilePath}. The rule set will be re-read on the next access.", filePath);
                    return [];
                }
            }
            finally
            {
                this.cacheLock.Release();
            }
        }

        public async Task SaveAsync(IReadOnlyList<PersistentProcessRule> rules)
        {
            ArgumentNullException.ThrowIfNull(rules);

            await this.cacheLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var filePath = this.filePathProvider();
                this.logger?.LogDebug("Saving {RuleCount} persistent process rules to {FilePath}", rules.Count, filePath);
                if (this.loadFailed)
                {
                    this.logger?.LogWarning(
                        "Saving {RuleCount} persistent process rules to {FilePath} after a failed read. A copy of the previous file was preserved next to it.",
                        rules.Count,
                        filePath);
                }

                try
                {
                    var json = JsonSerializer.Serialize(rules, JsonOptions);
                    await AtomicFileWriter.WriteAllTextAsync(filePath, json).ConfigureAwait(false);
                    this.cachedRules = rules.ToArray();
                    this.logger?.LogDebug("Saved {RuleCount} persistent process rules to {FilePath}", rules.Count, filePath);
                }
                catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
                {
                    this.logger?.LogError(ex, "Could not save {RuleCount} persistent process rules to {FilePath}", rules.Count, filePath);
                    throw;
                }
            }
            finally
            {
                this.cacheLock.Release();
            }
        }

        private void TryPreserveUnreadableFile(string filePath, Exception readException)
        {
            var backupPath = filePath + ".unreadable";

            try
            {
                if (File.Exists(backupPath))
                {
                    return;
                }

                File.Copy(filePath, backupPath, overwrite: false);
                this.logger?.LogWarning(
                    readException,
                    "Preserved unreadable persistent process rules file as {BackupPath}",
                    backupPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                this.logger?.LogDebug(ex, "Could not preserve unreadable persistent process rules file {FilePath}", filePath);
            }
        }
    }
}
