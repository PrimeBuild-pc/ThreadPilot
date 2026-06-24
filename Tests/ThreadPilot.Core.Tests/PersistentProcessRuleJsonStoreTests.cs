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
