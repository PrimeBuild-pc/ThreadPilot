/*
 * ThreadPilot - persistent process rule JSON store tests.
 */
namespace ThreadPilot.Core.Tests
{
    using System.Diagnostics;
    using ThreadPilot.Models;
    using ThreadPilot.Services;

    public sealed class PersistentProcessRuleJsonStoreTests
    {
        [Fact]
        public async Task LoadAsync_WithMissingFile_ReturnsEmptyList()
        {
            var filePath = CreateTemporaryFilePath();
            var store = new PersistentProcessRuleJsonStore(() => filePath);

            var rules = await store.LoadAsync();

            Assert.Empty(rules);
        }

        [Fact]
        public async Task SaveAndLoadAsync_RoundTripsCpuSelectionAndLegacyAffinityMask()
        {
            var filePath = CreateTemporaryFilePath();
            var store = new PersistentProcessRuleJsonStore(() => filePath);
            var rule = new PersistentProcessRule
            {
                Id = "rule-a",
                Name = "Game",
                IsEnabled = true,
                ProcessName = "game.exe",
                CpuSelection = new CpuSelection
                {
                    LogicalProcessors = [new ProcessorRef(0, 0, 0)],
                    GlobalLogicalProcessorIndexes = [0],
                },
                LegacyAffinityMask = 3,
                Priority = ProcessPriorityClass.AboveNormal,
                MemoryPriority = ProcessMemoryPriority.BelowNormal,
                ApplyAffinityOnStart = true,
                ApplyPriorityOnStart = true,
                ApplyMemoryPriorityOnStart = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Description = ProcessOperationUserMessages.PersistentRulesDescription,
            };

            try
            {
                await store.SaveAsync([rule]);

                var loaded = await store.LoadAsync();

                var loadedRule = Assert.Single(loaded);
                Assert.Equal("rule-a", loadedRule.Id);
                Assert.Equal(3, loadedRule.LegacyAffinityMask);
                Assert.Equal(ProcessPriorityClass.AboveNormal, loadedRule.Priority);
                Assert.Equal(ProcessMemoryPriority.BelowNormal, loadedRule.MemoryPriority);
                Assert.True(loadedRule.ApplyMemoryPriorityOnStart);
                Assert.NotNull(loadedRule.CpuSelection);
                Assert.Equal(0, loadedRule.CpuSelection.GlobalLogicalProcessorIndexes.Single());
            }
            finally
            {
                DeleteFile(filePath);
            }
        }

        [Fact]
        public async Task MultipleRules_SaveThenReload_DoesNotLoseEntries()
        {
            var filePath = CreateTemporaryFilePath();
            var store = new PersistentProcessRuleJsonStore(() => filePath);
            var rules = new[]
            {
                CreateRule("rule-a", "PersistenceProbe.exe", ProcessPriorityClass.High),
                CreateRule("rule-b", "AnotherProbe.exe", ProcessPriorityClass.AboveNormal),
            };

            try
            {
                await store.SaveAsync(rules);

                var loaded = await new PersistentProcessRuleJsonStore(() => filePath).LoadAsync();

                Assert.Equal(2, loaded.Count);
                Assert.Contains(loaded, rule => rule.Id == "rule-a" && rule.ProcessName == "PersistenceProbe.exe");
                Assert.Contains(loaded, rule => rule.Id == "rule-b" && rule.ProcessName == "AnotherProbe.exe");
            }
            finally
            {
                DeleteFile(filePath);
            }
        }

        [Fact]
        public async Task CpuPriority_SerializationRoundTrip()
        {
            var filePath = CreateTemporaryFilePath();
            var store = new PersistentProcessRuleJsonStore(() => filePath);
            var rule = CreateRule("rule-priority", "PersistenceProbe.exe", ProcessPriorityClass.High);

            try
            {
                await store.SaveAsync([rule]);

                var loaded = await new PersistentProcessRuleJsonStore(() => filePath).LoadAsync();

                var loadedRule = Assert.Single(loaded);
                Assert.Equal(ProcessPriorityClass.High, loadedRule.Priority);
                Assert.True(loadedRule.ApplyPriorityOnStart);
            }
            finally
            {
                DeleteFile(filePath);
            }
        }

        [Fact]
        public async Task SaveAsync_WithMissingDirectory_CreatesDirectory()
        {
            var filePath = CreateTemporaryFilePath();
            var store = new PersistentProcessRuleJsonStore(() => filePath);

            try
            {
                await store.SaveAsync([CreateRule("rule-a", "PersistenceProbe.exe", ProcessPriorityClass.High)]);

                Assert.True(File.Exists(filePath));
            }
            finally
            {
                DeleteFile(filePath);
            }
        }

        [Fact]
        public async Task LoadAsync_WithCorruptJson_ReturnsEmptyList()
        {
            var filePath = CreateTemporaryFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            await File.WriteAllTextAsync(filePath, "{ not json");
            var store = new PersistentProcessRuleJsonStore(() => filePath);

            try
            {
                var rules = await store.LoadAsync();

                Assert.Empty(rules);
            }
            finally
            {
                DeleteFile(filePath);
            }
        }

        private static PersistentProcessRule CreateRule(
            string id,
            string processName,
            ProcessPriorityClass priority) =>
            new()
            {
                Id = id,
                Name = $"{processName} rule",
                IsEnabled = true,
                ProcessName = processName,
                ExecutablePath = $@"C:\Probe\{processName}",
                Priority = priority,
                MemoryPriority = ProcessMemoryPriority.BelowNormal,
                ApplyPriorityOnStart = true,
                ApplyMemoryPriorityOnStart = true,
                CreatedAt = DateTime.UtcNow.AddMinutes(-1),
                UpdatedAt = DateTime.UtcNow,
                Description = "Persistence test rule",
            };

        private static string CreateTemporaryFilePath() =>
            Path.Combine(Path.GetTempPath(), $"threadpilot-rules-{Guid.NewGuid():N}", "rules.json");

        private static void DeleteFile(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (directory != null && Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
