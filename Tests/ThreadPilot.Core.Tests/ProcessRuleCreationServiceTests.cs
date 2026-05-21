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
                currentMemoryPriority: null);

            var rule = Assert.Single(store.SavedRules);
            Assert.NotNull(rule.CpuSelection);
            Assert.Null(rule.LegacyAffinityMask);
            Assert.True(rule.ApplyAffinityOnStart);
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

        private static ProcessRuleCreationService CreateService(
            CapturingRuleStore store,
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
