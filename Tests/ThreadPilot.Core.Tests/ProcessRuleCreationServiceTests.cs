/*
 * ThreadPilot - persistent process rule creation tests.
 */
namespace ThreadPilot.Core.Tests
{
    using System.Diagnostics;
    using Microsoft.Extensions.Logging.Abstractions;
    using ThreadPilot.Models;
    using ThreadPilot.Services;

    public sealed class ProcessRuleCreationServiceTests
    {
        [Fact]
        public async Task SaveRuleAsync_UsesExecutablePathWhenAvailable()
        {
            var store = new CapturingRuleStore();
            var service = CreateService(store);
            var process = CreateProcess(path: @"C:\Games\Game.exe");

            var result = await service.SaveRuleAsync(
                process,
                new ProcessRuleCreationPayload { Priority = ProcessPriorityClass.AboveNormal });

            Assert.True(result.Success);
            var rule = Assert.Single(store.SavedRules);
            Assert.Equal(@"C:\Games\Game.exe", rule.ExecutablePath);
            Assert.Equal("Game.exe", rule.ProcessName);
            Assert.True(rule.IsEnabled);
            Assert.Equal("Game.exe rule", rule.Name);
            Assert.Equal("Created from Process tab action.", rule.Description);
            Assert.True(result.Created);
            Assert.False(result.Updated);
            Assert.Equal("Saved rule for Game.exe.", result.UserMessage);
        }

        [Fact]
        public async Task SaveRuleAsync_FallsBackToProcessNameWhenPathUnavailable()
        {
            var store = new CapturingRuleStore();
            var service = CreateService(store);

            await service.SaveRuleAsync(
                CreateProcess(path: string.Empty),
                new ProcessRuleCreationPayload { Priority = ProcessPriorityClass.Normal });

            var rule = Assert.Single(store.SavedRules);
            Assert.Null(rule.ExecutablePath);
            Assert.Equal("Game.exe", rule.ProcessName);
        }

        [Fact]
        public async Task SaveRuleAsync_UpdatesExistingPathMatchWithoutDuplicating()
        {
            var createdAt = DateTime.UtcNow.AddDays(-2);
            var existing = new PersistentProcessRule
            {
                Id = "existing-rule",
                Name = "Old",
                IsEnabled = true,
                ProcessName = "Game.exe",
                ExecutablePath = @"C:\Games\Game.exe",
                Priority = ProcessPriorityClass.Normal,
                ApplyPriorityOnStart = true,
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
            };
            var store = new CapturingRuleStore([existing]);
            var service = CreateService(store);

            var result = await service.SaveRuleAsync(
                CreateProcess(path: @"C:\Games\Game.exe"),
                new ProcessRuleCreationPayload { Priority = ProcessPriorityClass.High });

            var rule = Assert.Single(store.SavedRules);
            Assert.True(result.Updated);
            Assert.False(result.Created);
            Assert.Equal("Updated saved rule for Game.exe.", result.UserMessage);
            Assert.Equal("existing-rule", rule.Id);
            Assert.Equal(createdAt, rule.CreatedAt);
            Assert.Equal(ProcessPriorityClass.High, rule.Priority);
            Assert.True(rule.UpdatedAt > createdAt);
        }

        [Fact]
        public async Task SaveRuleAsync_UpdatesExistingPathlessNameMatchWhenNewPathIsAvailable()
        {
            var existing = new PersistentProcessRule
            {
                Id = "pathless-rule",
                Name = "Game.exe rule",
                IsEnabled = true,
                ProcessName = "Game.exe",
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow.AddDays(-1),
            };
            var store = new CapturingRuleStore([existing]);
            var service = CreateService(store);

            await service.SaveRuleAsync(
                CreateProcess(path: @"C:\Games\Game.exe"),
                new ProcessRuleCreationPayload { Priority = ProcessPriorityClass.AboveNormal });

            var rule = Assert.Single(store.SavedRules);
            Assert.Equal("pathless-rule", rule.Id);
            Assert.Equal(@"C:\Games\Game.exe", rule.ExecutablePath);
            Assert.Equal(ProcessPriorityClass.AboveNormal, rule.Priority);
        }

        [Fact]
        public async Task SaveRuleAsync_SavesCpuSelectionWhenProvided()
        {
            var store = new CapturingRuleStore();
            var service = CreateService(store);
            var selection = CreateCpuSelection();

            await service.SaveRuleAsync(
                CreateProcess(),
                new ProcessRuleCreationPayload { CpuSelection = selection });

            var rule = Assert.Single(store.SavedRules);
            Assert.Same(selection, rule.CpuSelection);
            Assert.Null(rule.LegacyAffinityMask);
            Assert.True(rule.ApplyAffinityOnStart);
        }

        [Fact]
        public async Task SaveCurrentSettingsAsRuleAsync_PrefersCpuSelectionWhenTopologyIsAvailable()
        {
            var store = new CapturingRuleStore();
            var topologyProvider = new FakeTopologyProvider(CpuTopologySnapshot.Create(
                [
                    new ProcessorRef(0, 0, 0),
                    new ProcessorRef(0, 1, 1),
                ]));
            var service = CreateService(store, topologyProvider);

            await service.SaveCurrentSettingsAsRuleAsync(
                CreateProcess(priority: ProcessPriorityClass.RealTime, affinity: 0),
                currentCoreSelection: [true, false],
                currentMemoryPriority: null,
                cpuAssignmentMode: CpuAssignmentMode.IdealProcessor);

            var rule = Assert.Single(store.SavedRules);
            Assert.NotNull(rule.CpuSelection);
            Assert.Null(rule.LegacyAffinityMask);
            Assert.True(rule.ApplyAffinityOnStart);
            Assert.Equal(CpuAssignmentMode.IdealProcessor, rule.CpuAssignmentMode);
            Assert.Equal(0, rule.CpuSelection.GlobalLogicalProcessorIndexes.Single());
        }

        [Fact]
        public async Task SaveCurrentSettingsAsRuleAsync_SavesLegacyMaskWhenSelectionIsSafelyRepresentable()
        {
            var store = new CapturingRuleStore();
            var service = CreateService(store, topologyProvider: null);

            var result = await service.SaveCurrentSettingsAsRuleAsync(
                CreateProcess(priority: ProcessPriorityClass.RealTime, affinity: 0x3),
                currentCoreSelection: [true, true, false],
                currentMemoryPriority: null);

            Assert.True(result.Success);
            var rule = Assert.Single(store.SavedRules);
            Assert.Equal(0x3, rule.LegacyAffinityMask);
            Assert.Null(rule.CpuSelection);
            Assert.True(rule.ApplyAffinityOnStart);
            Assert.Null(rule.Priority);
            Assert.False(rule.ApplyPriorityOnStart);
        }

        [Fact]
        public async Task SaveCurrentSettingsAsRuleAsync_BlocksUnsafeLegacyAffinity()
        {
            var store = new CapturingRuleStore();
            var service = CreateService(store, topologyProvider: null);
            var unsafeSelection = Enumerable.Repeat(true, 65).ToArray();

            var result = await service.SaveCurrentSettingsAsRuleAsync(
                CreateProcess(priority: ProcessPriorityClass.RealTime, affinity: 0),
                unsafeSelection,
                currentMemoryPriority: null);

            Assert.False(result.Success);
            Assert.Equal(
                "The current affinity selection cannot be saved safely on this CPU topology.",
                result.UserMessage);
            Assert.Empty(store.SavedRules);
        }

        [Fact]
        public async Task SaveCurrentSettingsAsRuleAsync_BlocksRealtimePriority()
        {
            var store = new CapturingRuleStore();
            var service = CreateService(store);

            var result = await service.SaveCurrentSettingsAsRuleAsync(
                CreateProcess(priority: ProcessPriorityClass.RealTime, affinity: 0),
                currentCoreSelection: null,
                currentMemoryPriority: null);

            Assert.False(result.Success);
            Assert.Equal("There are no current settings to save as a rule.", result.UserMessage);
            Assert.Empty(store.SavedRules);
        }

        [Fact]
        public async Task SaveCurrentSettingsAsRuleAsync_WithNormalPriorityAndNoOtherPayload_ReturnsNoActionFailure()
        {
            var store = new CapturingRuleStore();
            var service = CreateService(store);

            var result = await service.SaveCurrentSettingsAsRuleAsync(
                CreateProcess(priority: ProcessPriorityClass.Normal, affinity: 0),
                currentCoreSelection: null,
                currentMemoryPriority: null);

            Assert.False(result.Success);
            Assert.Equal("NoActionableRulePayload", result.ErrorCode);
            Assert.Equal("There are no current settings to save as a rule.", result.UserMessage);
            Assert.Empty(store.SavedRules);
        }

        [Fact]
        public async Task SaveCurrentSettingsAsRuleAsync_WithNormalPriorityAndAffinity_SavesAffinityButDoesNotEnablePriority()
        {
            var store = new CapturingRuleStore();
            var service = CreateService(store);

            var result = await service.SaveCurrentSettingsAsRuleAsync(
                CreateProcess(priority: ProcessPriorityClass.Normal, affinity: 0x5),
                currentCoreSelection: null,
                currentMemoryPriority: null);

            Assert.True(result.Success);
            var rule = Assert.Single(store.SavedRules);
            Assert.Equal(0x5, rule.LegacyAffinityMask);
            Assert.True(rule.ApplyAffinityOnStart);
            Assert.Null(rule.Priority);
            Assert.False(rule.ApplyPriorityOnStart);
        }

        [Fact]
        public async Task SaveCurrentSettingsAsRuleAsync_WithAboveNormalPriority_SavesPriority()
        {
            var store = new CapturingRuleStore();
            var service = CreateService(store);

            var result = await service.SaveCurrentSettingsAsRuleAsync(
                CreateProcess(priority: ProcessPriorityClass.AboveNormal, affinity: 0),
                currentCoreSelection: null,
                currentMemoryPriority: null);

            Assert.True(result.Success);
            var rule = Assert.Single(store.SavedRules);
            Assert.Equal(ProcessPriorityClass.AboveNormal, rule.Priority);
            Assert.True(rule.ApplyPriorityOnStart);
        }

        [Fact]
        public async Task SaveCurrentSettingsAsRuleAsync_WithHighPriority_SavesPriority()
        {
            var store = new CapturingRuleStore();
            var service = CreateService(store);

            var result = await service.SaveCurrentSettingsAsRuleAsync(
                CreateProcess(priority: ProcessPriorityClass.High, affinity: 0),
                currentCoreSelection: null,
                currentMemoryPriority: null);

            Assert.True(result.Success);
            var rule = Assert.Single(store.SavedRules);
            Assert.Equal(ProcessPriorityClass.High, rule.Priority);
            Assert.True(rule.ApplyPriorityOnStart);
        }

        [Fact]
        public async Task SaveCurrentSettingsAsRuleAsync_WithRealtimePriority_OmitsPriorityWithoutSavingIt()
        {
            var store = new CapturingRuleStore();
            var service = CreateService(store);

            var result = await service.SaveCurrentSettingsAsRuleAsync(
                CreateProcess(priority: ProcessPriorityClass.RealTime, affinity: 0x3),
                currentCoreSelection: null,
                currentMemoryPriority: null);

            Assert.True(result.Success);
            var rule = Assert.Single(store.SavedRules);
            Assert.Equal(0x3, rule.LegacyAffinityMask);
            Assert.Null(rule.Priority);
            Assert.False(rule.ApplyPriorityOnStart);
        }

        [Fact]
        public async Task SaveCurrentSettingsAsRuleAsync_SavesMemoryPriorityWhenAvailable()
        {
            var store = new CapturingRuleStore();
            var service = CreateService(store);

            await service.SaveCurrentSettingsAsRuleAsync(
                CreateProcess(priority: ProcessPriorityClass.RealTime, affinity: 0),
                currentCoreSelection: null,
                currentMemoryPriority: ProcessMemoryPriority.BelowNormal);

            var rule = Assert.Single(store.SavedRules);
            Assert.Equal(ProcessMemoryPriority.BelowNormal, rule.MemoryPriority);
            Assert.True(rule.ApplyMemoryPriorityOnStart);
        }

        [Fact]
        public async Task SaveCurrentSettingsAsRuleAsync_ReturnsControlledFailureWhenNoActionablePayloadExists()
        {
            var store = new CapturingRuleStore();
            var service = CreateService(store);

            var result = await service.SaveCurrentSettingsAsRuleAsync(
                CreateProcess(priority: ProcessPriorityClass.RealTime, affinity: 0),
                currentCoreSelection: [],
                currentMemoryPriority: null);

            Assert.False(result.Success);
            Assert.Equal("There are no current settings to save as a rule.", result.UserMessage);
            Assert.Empty(store.SavedRules);
        }

        [Fact]
        public async Task CreateRule_SaveThenReload_PreservesAllPersistedFields()
        {
            var filePath = CreateTemporaryFilePath();
            var store = new PersistentProcessRuleJsonStore(() => filePath);
            var service = CreateService(store);
            var selection = CreateCpuSelection();

            try
            {
                var result = await service.SaveRuleAsync(
                    CreateProcess(name: "PersistenceProbe.exe", path: @"C:\Probe\PersistenceProbe.exe"),
                    new ProcessRuleCreationPayload
                    {
                        CpuSelection = selection,
                        Priority = ProcessPriorityClass.High,
                        MemoryPriority = ProcessMemoryPriority.BelowNormal,
                    });

                Assert.True(result.Success);
                var loaded = await new PersistentProcessRuleJsonStore(() => filePath).LoadAsync();
                var rule = Assert.Single(loaded);
                Assert.Equal(result.Rule!.Id, rule.Id);
                Assert.Equal("PersistenceProbe.exe rule", rule.Name);
                Assert.True(rule.IsEnabled);
                Assert.Equal("PersistenceProbe.exe", rule.ProcessName);
                Assert.Equal(@"C:\Probe\PersistenceProbe.exe", rule.ExecutablePath);
                Assert.NotNull(rule.CpuSelection);
                Assert.Equal(selection.GlobalLogicalProcessorIndexes, rule.CpuSelection.GlobalLogicalProcessorIndexes);
                Assert.Null(rule.LegacyAffinityMask);
                Assert.Equal(ProcessPriorityClass.High, rule.Priority);
                Assert.Equal(ProcessMemoryPriority.BelowNormal, rule.MemoryPriority);
                Assert.True(rule.ApplyAffinityOnStart);
                Assert.True(rule.ApplyPriorityOnStart);
                Assert.True(rule.ApplyMemoryPriorityOnStart);
                Assert.Equal(result.Rule.CreatedAt, rule.CreatedAt);
                Assert.Equal(result.Rule.UpdatedAt, rule.UpdatedAt);
                Assert.Equal("Created from Process tab action.", rule.Description);
            }
            finally
            {
                DeleteTemporaryDirectory(filePath);
            }
        }

        [Fact]
        public async Task UpdateRule_SaveThenReload_PreservesUpdatedValues()
        {
            var filePath = CreateTemporaryFilePath();
            var store = new PersistentProcessRuleJsonStore(() => filePath);
            var service = CreateService(store);
            var process = CreateProcess(name: "PersistenceProbe.exe", path: @"C:\Probe\PersistenceProbe.exe");

            try
            {
                var created = await service.SaveRuleAsync(
                    process,
                    new ProcessRuleCreationPayload { Priority = ProcessPriorityClass.AboveNormal });
                var updated = await service.SaveRuleAsync(
                    process,
                    new ProcessRuleCreationPayload
                    {
                        LegacyAffinityMask = 0x3,
                        Priority = ProcessPriorityClass.High,
                        MemoryPriority = ProcessMemoryPriority.Low,
                    });

                Assert.True(created.Success);
                Assert.True(updated.Success);
                Assert.True(updated.Updated);
                var loaded = await new PersistentProcessRuleJsonStore(() => filePath).LoadAsync();
                var rule = Assert.Single(loaded);
                Assert.Equal(created.Rule!.Id, rule.Id);
                Assert.Equal(created.Rule.CreatedAt, rule.CreatedAt);
                Assert.Equal(0x3, rule.LegacyAffinityMask);
                Assert.Equal(ProcessPriorityClass.High, rule.Priority);
                Assert.Equal(ProcessMemoryPriority.Low, rule.MemoryPriority);
                Assert.True(rule.ApplyAffinityOnStart);
                Assert.True(rule.ApplyPriorityOnStart);
                Assert.True(rule.ApplyMemoryPriorityOnStart);
            }
            finally
            {
                DeleteTemporaryDirectory(filePath);
            }
        }

        private static ProcessRuleCreationService CreateService(
            IPersistentProcessRuleStore store,
            ICpuTopologyProvider? topologyProvider = null) =>
            new(
                store,
                topologyProvider,
                new CpuSelectionMigrationService(),
                NullLogger<ProcessRuleCreationService>.Instance);

        private static CpuSelection CreateCpuSelection() =>
            new()
            {
                LogicalProcessors = [new ProcessorRef(0, 0, 0)],
                GlobalLogicalProcessorIndexes = [0],
            };

        [Fact]
        public async Task SaveRuleAsync_KeepsFieldsTheCallerDidNotCarry()
        {
            // The regression this covers: "Apply CPU assignment and save as rule" sent no memory or
            // I/O priority, and the rewrite dropped both from a rule that already had them.
            var existing = new PersistentProcessRule
            {
                Id = "existing-rule",
                ProcessName = "Game.exe",
                ExecutablePath = @"C:\Games\Game.exe",
                IsEnabled = true,
                MemoryPriority = ProcessMemoryPriority.Low,
                IoPriority = ProcessIoPriority.Low,
                Priority = ProcessPriorityClass.High,
                ApplyMemoryPriorityOnStart = true,
                ApplyIoPriorityOnStart = true,
                ApplyPriorityOnStart = true,
            };
            var store = new CapturingRuleStore([existing]);
            var service = CreateService(store);

            var result = await service.SaveRuleAsync(
                CreateProcess(path: @"C:\Games\Game.exe"),
                new ProcessRuleCreationPayload { CpuSelection = CreateCpuSelection() });

            Assert.True(result.Success);
            var rule = Assert.Single(store.SavedRules);
            Assert.Equal(ProcessMemoryPriority.Low, rule.MemoryPriority);
            Assert.Equal(ProcessIoPriority.Low, rule.IoPriority);
            Assert.Equal(ProcessPriorityClass.High, rule.Priority);
            Assert.True(rule.ApplyMemoryPriorityOnStart);
            Assert.True(rule.ApplyIoPriorityOnStart);
            Assert.True(rule.ApplyPriorityOnStart);
            Assert.True(rule.ApplyAffinityOnStart);
        }

        [Fact]
        public async Task SaveRuleAsync_KeepsTheExistingAffinityWhenOnlyPrioritiesAreSaved()
        {
            var selection = CreateCpuSelection();
            var existing = new PersistentProcessRule
            {
                Id = "existing-rule",
                ProcessName = "Game.exe",
                ExecutablePath = @"C:\Games\Game.exe",
                IsEnabled = true,
                CpuSelection = selection,
                CpuAssignmentMode = CpuAssignmentMode.CpuSets,
                ApplyAffinityOnStart = true,
            };
            var store = new CapturingRuleStore([existing]);
            var service = CreateService(store);

            await service.SaveRuleAsync(
                CreateProcess(path: @"C:\Games\Game.exe"),
                new ProcessRuleCreationPayload { Priority = ProcessPriorityClass.High });

            var rule = Assert.Single(store.SavedRules);
            Assert.Same(selection, rule.CpuSelection);
            Assert.Equal(CpuAssignmentMode.CpuSets, rule.CpuAssignmentMode);
            Assert.True(rule.ApplyAffinityOnStart);
        }

        [Fact]
        public async Task SaveRuleAsync_StillReplacesAFieldTheCallerDoesCarry()
        {
            var existing = new PersistentProcessRule
            {
                Id = "existing-rule",
                ProcessName = "Game.exe",
                ExecutablePath = @"C:\Games\Game.exe",
                IsEnabled = true,
                MemoryPriority = ProcessMemoryPriority.Low,
                ApplyMemoryPriorityOnStart = true,
            };
            var store = new CapturingRuleStore([existing]);
            var service = CreateService(store);

            await service.SaveRuleAsync(
                CreateProcess(path: @"C:\Games\Game.exe"),
                new ProcessRuleCreationPayload { MemoryPriority = ProcessMemoryPriority.BelowNormal });

            var rule = Assert.Single(store.SavedRules);
            Assert.Equal(ProcessMemoryPriority.BelowNormal, rule.MemoryPriority);
        }

        [Fact]
        public async Task DeleteRuleAsync_RemovesTheRuleMatchingTheProcess()
        {
            var target = new PersistentProcessRule
            {
                Id = "target-rule",
                ProcessName = "Game.exe",
                ExecutablePath = @"C:\Games\Game.exe",
                IsEnabled = true,
                Priority = ProcessPriorityClass.High,
                ApplyPriorityOnStart = true,
            };
            var other = new PersistentProcessRule
            {
                Id = "other-rule",
                ProcessName = "Other.exe",
                ExecutablePath = @"C:\Games\Other.exe",
                IsEnabled = true,
            };
            var store = new CapturingRuleStore([target, other]);
            var service = CreateService(store);

            var result = await service.DeleteRuleAsync(CreateProcess(path: @"C:\Games\Game.exe"));

            Assert.True(result.Success);
            Assert.Equal("Deleted saved rule for Game.exe.", result.UserMessage);
            var remaining = Assert.Single(store.SavedRules);
            Assert.Equal("other-rule", remaining.Id);
        }

        [Fact]
        public async Task DeleteRuleAsync_ReportsAControlledFailureWhenNoRuleMatches()
        {
            var store = new CapturingRuleStore([]);
            var service = CreateService(store);

            var result = await service.DeleteRuleAsync(CreateProcess());

            Assert.False(result.Success);
            Assert.Equal("NoSavedRuleToDelete", result.ErrorCode);
            Assert.Equal(ProcessRuleCreationService.NoSavedRuleMessage, result.UserMessage);
            Assert.Empty(store.SavedRules);
        }

        [Fact]
        public async Task UpdateRuleAsync_ChangesTheEditableFieldsAndKeepsTheAffinity()
        {
            var selection = CreateCpuSelection();
            var existing = new PersistentProcessRule
            {
                Id = "rule-1",
                ProcessName = "cs2",
                IsEnabled = true,
                CpuSelection = selection,
                ApplyAffinityOnStart = true,
                CpuAssignmentMode = CpuAssignmentMode.AffinityMask,
            };
            var store = new CapturingRuleStore([existing]);
            var service = CreateService(store);

            var result = await service.UpdateRuleAsync(existing with
            {
                IsEnabled = false,
                MemoryPriority = ProcessMemoryPriority.Low,
                Priority = ProcessPriorityClass.High,
            });

            Assert.True(result.Success);
            var rule = Assert.Single(store.SavedRules);
            Assert.False(rule.IsEnabled);
            Assert.Equal(ProcessMemoryPriority.Low, rule.MemoryPriority);
            Assert.True(rule.ApplyMemoryPriorityOnStart);
            Assert.Equal(ProcessPriorityClass.High, rule.Priority);
            Assert.True(rule.ApplyPriorityOnStart);
            Assert.Same(selection, rule.CpuSelection);
            Assert.True(rule.ApplyAffinityOnStart);
        }

        [Fact]
        public async Task UpdateRuleAsync_ClearsAFieldTheEditorSetBackToNone()
        {
            var existing = new PersistentProcessRule
            {
                Id = "rule-1",
                ProcessName = "cs2",
                IsEnabled = true,
                MemoryPriority = ProcessMemoryPriority.Low,
                ApplyMemoryPriorityOnStart = true,
            };
            var store = new CapturingRuleStore([existing]);
            var service = CreateService(store);

            await service.UpdateRuleAsync(existing with { MemoryPriority = null });

            var rule = Assert.Single(store.SavedRules);
            Assert.Null(rule.MemoryPriority);
            Assert.False(rule.ApplyMemoryPriorityOnStart);
        }

        [Fact]
        public async Task UpdateRuleAsync_RefusesRealtimeAndLeavesTheRuleAlone()
        {
            var existing = new PersistentProcessRule
            {
                Id = "rule-1",
                ProcessName = "cs2",
                IsEnabled = true,
                Priority = ProcessPriorityClass.High,
                ApplyPriorityOnStart = true,
            };
            var store = new CapturingRuleStore([existing]);
            var service = CreateService(store);

            var result = await service.UpdateRuleAsync(existing with { Priority = ProcessPriorityClass.RealTime });

            Assert.False(result.Success);
            Assert.Equal("RealtimePriorityBlocked", result.ErrorCode);
            Assert.Empty(store.SavedRules);
        }

        [Fact]
        public async Task UpdateRuleAsync_ReportsAControlledFailureForAnUnknownRule()
        {
            var store = new CapturingRuleStore([]);
            var service = CreateService(store);

            var result = await service.UpdateRuleAsync(new PersistentProcessRule { Id = "missing", ProcessName = "cs2" });

            Assert.False(result.Success);
            Assert.Equal("NoSavedRuleToUpdate", result.ErrorCode);
        }

        private static ProcessModel CreateProcess(
            string name = "Game.exe",
            string path = @"C:\Games\Game.exe",
            ProcessPriorityClass priority = ProcessPriorityClass.Normal,
            long affinity = 0xF) =>
            new()
            {
                ProcessId = 42,
                Name = name,
                ExecutablePath = path,
                Priority = priority,
                ProcessorAffinity = affinity,
            };

        private static string CreateTemporaryFilePath() =>
            Path.Combine(Path.GetTempPath(), $"threadpilot-rule-service-{Guid.NewGuid():N}", "rules.json");

        private static void DeleteTemporaryDirectory(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (directory != null && Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        private sealed class CapturingRuleStore(IReadOnlyList<PersistentProcessRule>? initialRules = null)
            : IPersistentProcessRuleStore
        {
            public IReadOnlyList<PersistentProcessRule> SavedRules { get; private set; } = [];

            public Task<IReadOnlyList<PersistentProcessRule>> LoadAsync() =>
                Task.FromResult(initialRules ?? this.SavedRules);

            public Task SaveAsync(IReadOnlyList<PersistentProcessRule> rules)
            {
                this.SavedRules = rules.ToList();
                return Task.CompletedTask;
            }
        }

        private sealed class FakeTopologyProvider(CpuTopologySnapshot topology) : ICpuTopologyProvider
        {
            public Task<CpuTopologySnapshot> GetTopologySnapshotAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult(topology);
        }
    }
}
