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
                CpuAssignmentMode = CpuAssignmentMode.IdealProcessor,
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
                Assert.Equal(CpuAssignmentMode.IdealProcessor, loadedRule.CpuAssignmentMode);
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
        public async Task LoadAsync_OldRuleWithoutCpuAssignmentMode_DefaultsToAutomatic()
        {
            var filePath = CreateTemporaryFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            await File.WriteAllTextAsync(
                filePath,
                """
                [{"Id":"legacy","Name":"Legacy","IsEnabled":true,"ProcessName":"game.exe","LegacyAffinityMask":3,"ApplyAffinityOnStart":true}]
                """);

            try
            {
                var rule = Assert.Single(await new PersistentProcessRuleJsonStore(() => filePath).LoadAsync());
                Assert.Equal(CpuAssignmentMode.Automatic, rule.CpuAssignmentMode);
                Assert.Equal(3, rule.LegacyAffinityMask);
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
        public async Task LoadAsync_AfterFirstRead_ReturnsCachedSnapshot()
        {
            var filePath = CreateTemporaryFilePath();
            var store = new PersistentProcessRuleJsonStore(() => filePath);

            try
            {
                await store.SaveAsync([CreateRule("cached", "Cached.exe", ProcessPriorityClass.High)]);
                var first = await store.LoadAsync();
                await new PersistentProcessRuleJsonStore(() => filePath)
                    .SaveAsync([CreateRule("external", "External.exe", ProcessPriorityClass.Normal)]);

                var second = await store.LoadAsync();

                Assert.Same(first, second);
                Assert.Equal("cached", Assert.Single(second).Id);
            }
            finally
            {
                DeleteFile(filePath);
            }
        }

        [Fact]
        public async Task SaveAsync_AfterCachedLoad_UpdatesSnapshot()
        {
            var filePath = CreateTemporaryFilePath();
            var store = new PersistentProcessRuleJsonStore(() => filePath);

            try
            {
                await store.SaveAsync([CreateRule("first", "First.exe", ProcessPriorityClass.High)]);
                _ = await store.LoadAsync();
                await store.SaveAsync([CreateRule("second", "Second.exe", ProcessPriorityClass.AboveNormal)]);

                Assert.Equal("second", Assert.Single(await store.LoadAsync()).Id);
                Assert.Equal("second", Assert.Single(await new PersistentProcessRuleJsonStore(() => filePath).LoadAsync()).Id);
            }
            finally
            {
                DeleteFile(filePath);
            }
        }

        [Fact]
        public async Task ConcurrentSaves_LeaveOneCompleteSnapshot()
        {
            var filePath = CreateTemporaryFilePath();
            var store = new PersistentProcessRuleJsonStore(() => filePath);
            var expectedIds = Enumerable.Range(0, 8).Select(index => $"rule-{index}").ToHashSet();

            try
            {
                await Task.WhenAll(expectedIds.Select(id =>
                    store.SaveAsync([CreateRule(id, $"{id}.exe", ProcessPriorityClass.High)])));

                var cached = Assert.Single(await store.LoadAsync());
                var persisted = Assert.Single(await new PersistentProcessRuleJsonStore(() => filePath).LoadAsync());
                Assert.Contains(cached.Id, expectedIds);
                Assert.Equal(cached.Id, persisted.Id);
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

        [Fact]
        public async Task LoadAsync_AfterFailedRead_RetriesInsteadOfCachingAnEmptySet()
        {
            var filePath = CreateTemporaryFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            await File.WriteAllTextAsync(filePath, "{ not json");
            var store = new PersistentProcessRuleJsonStore(() => filePath);

            try
            {
                Assert.Empty(await store.LoadAsync());

                await new PersistentProcessRuleJsonStore(() => filePath)
                    .SaveAsync([CreateRule("recovered", "Recovered.exe", ProcessPriorityClass.High)]);

                var reloaded = await store.LoadAsync();

                Assert.Equal("recovered", Assert.Single(reloaded).Id);
            }
            finally
            {
                DeleteFile(filePath);
            }
        }

        [Fact]
        public async Task LoadAsync_WithUnreadableFile_PreservesACopyForRecovery()
        {
            var filePath = CreateTemporaryFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            const string OriginalContent = "{ not json but the user's only copy";
            await File.WriteAllTextAsync(filePath, OriginalContent);
            var store = new PersistentProcessRuleJsonStore(() => filePath);

            try
            {
                Assert.Empty(await store.LoadAsync());

                var backupPath = Assert.Single(Directory.GetFiles(Path.GetDirectoryName(filePath)!, "rules.json.unreadable*"));
                Assert.Equal(OriginalContent, await File.ReadAllTextAsync(backupPath));
            }
            finally
            {
                DeleteFile(filePath);
            }
        }

        [Fact]
        public async Task SaveAsync_WhenUnreadableFileCannotBePreserved_DoesNotOverwriteOriginal()
        {
            var filePath = CreateTemporaryFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            const string OriginalContent = "{ locked user rules";
            await File.WriteAllTextAsync(filePath, OriginalContent);
            var store = new PersistentProcessRuleJsonStore(
                () => filePath,
                copyFile: (_, _) => throw new IOException("Recovery destination unavailable"));

            try
            {
                Assert.Empty(await store.LoadAsync());

                await Assert.ThrowsAsync<IOException>(() =>
                    store.SaveAsync([CreateRule("replacement", "Replacement.exe", ProcessPriorityClass.High)]));
                Assert.Equal(OriginalContent, await File.ReadAllTextAsync(filePath));
            }
            finally
            {
                DeleteFile(filePath);
            }
        }

        [Fact]
        public async Task LoadAsync_WithExistingRecoveryFile_CreatesANewRecoveryCopy()
        {
            var filePath = CreateTemporaryFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            const string CurrentContent = "{ current user rules";
            await File.WriteAllTextAsync(filePath, CurrentContent);
            await File.WriteAllTextAsync(filePath + ".unreadable", "stale recovery");
            var store = new PersistentProcessRuleJsonStore(() => filePath);

            try
            {
                Assert.Empty(await store.LoadAsync());

                var recoveryFiles = Directory.GetFiles(Path.GetDirectoryName(filePath)!, "rules.json.unreadable*");
                Assert.Equal(2, recoveryFiles.Length);
                Assert.Contains(recoveryFiles, path => File.ReadAllText(path) == CurrentContent);
            }
            finally
            {
                DeleteFile(filePath);
            }
        }

        [Fact]
        public async Task SaveAsync_RetriesRecoveryAfterTransientCopyFailure()
        {
            var filePath = CreateTemporaryFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            const string OriginalContent = "{ temporarily locked user rules";
            await File.WriteAllTextAsync(filePath, OriginalContent);
            var copyAttempts = 0;
            var store = new PersistentProcessRuleJsonStore(
                () => filePath,
                copyFile: (source, destination) =>
                {
                    if (Interlocked.Increment(ref copyAttempts) == 1)
                    {
                        throw new IOException("Transient copy failure");
                    }

                    File.Copy(source, destination);
                });

            try
            {
                Assert.Empty(await store.LoadAsync());

                await store.SaveAsync([CreateRule("replacement", "Replacement.exe", ProcessPriorityClass.High)]);

                Assert.Equal(2, copyAttempts);
                Assert.Contains(
                    Directory.GetFiles(Path.GetDirectoryName(filePath)!, "rules.json.unreadable*"),
                    path => File.ReadAllText(path) == OriginalContent);
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
