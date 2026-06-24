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
            var filePath = this.filePathProvider();
            if (!File.Exists(filePath))
            {
                return [];
            }

            try
            {
                var json = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
                return JsonSerializer.Deserialize<List<PersistentProcessRule>>(json, JsonOptions) ?? [];
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                this.logger?.LogWarning(ex, "Could not load persistent process rules from {FilePath}", filePath);
                return [];
            }
        }

        public async Task SaveAsync(IReadOnlyList<PersistentProcessRule> rules)
        {
            ArgumentNullException.ThrowIfNull(rules);

            var filePath = this.filePathProvider();
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(rules, JsonOptions);
            await File.WriteAllTextAsync(filePath, json).ConfigureAwait(false);
        }
    }
}
