namespace ThreadPilot.Core.Tests
{
    using Microsoft.Extensions.Logging.Abstractions;
    using ThreadPilot.Models;
    using ThreadPilot.Services;

    public sealed class ProcessAffinityApplyCoordinatorTests
    {
        [Fact]
        public async Task ApplyCoreMaskAsync_WithCpuSelection_UsesCpuSelectionPath()
        {
            var process = CreateProcess();
            var selection = new CpuSelection
            {
                LogicalProcessors = [new ProcessorRef(0, 0, 0), new ProcessorRef(0, 2, 2)],
                GlobalLogicalProcessorIndexes = [0, 2],
            };
            var mask = CreateMask([true, false, true]);
            mask.CpuSelection = selection;
            var affinity = new RecordingAffinityApplyService();
            var coordinator = CreateCoordinator(affinity);

            var result = await coordinator.ApplyCoreMaskAsync(process, mask);

            Assert.True(result.Success);
            Assert.Equal(1, affinity.CpuSelectionApplyCalls);
            Assert.Equal(0, affinity.LegacyApplyCalls);
            Assert.Same(selection, affinity.LastSelection);
        }

        [Fact]
        public async Task ApplyCoreMaskAsync_WithCpu64Selection_DoesNotUseLegacyMaskOrAliasCpu0()
        {
            var process = CreateProcess();
            var cpu64 = new ProcessorRef(1, 0, 64);
            var mask = CreateMask(Enumerable.Range(0, 65).Select(index => index == 64).ToList());
            mask.CpuSelection = new CpuSelection
            {
                LogicalProcessors = [cpu64],
                GlobalLogicalProcessorIndexes = [64],
            };
            var affinity = new RecordingAffinityApplyService();
            var coordinator = CreateCoordinator(affinity);

            var result = await coordinator.ApplyCoreMaskAsync(process, mask);

            Assert.True(result.Success);
            Assert.Equal(0, affinity.LegacyApplyCalls);
            var applied = Assert.Single(affinity.LastSelection!.LogicalProcessors);
            Assert.Equal(cpu64, applied);
            Assert.DoesNotContain(affinity.LastSelection.LogicalProcessors, processor => processor.GlobalIndex == 0);
        }

        [Fact]
        public async Task ApplyCoreMaskAsync_WithMultiGroupSelection_DoesNotUseLegacyMask()
        {
            var process = CreateProcess();
            var selection = new CpuSelection
            {
                LogicalProcessors = [new ProcessorRef(0, 0, 0), new ProcessorRef(1, 0, 64)],
                GlobalLogicalProcessorIndexes = [0, 64],
            };
            var mask = CreateMask(Enumerable.Repeat(true, 65).ToList());
            mask.CpuSelection = selection;
            var affinity = new RecordingAffinityApplyService();
            var coordinator = CreateCoordinator(affinity);

            var result = await coordinator.ApplyCoreMaskAsync(process, mask);

            Assert.True(result.Success);
            Assert.Equal(1, affinity.CpuSelectionApplyCalls);
            Assert.Equal(0, affinity.LegacyApplyCalls);
        }

        [Fact]
        public async Task ApplyCoreMaskAsync_WithoutCpuSelectionAndWithoutTopology_UsesLegacyForSingleGroupMask()
        {
            var process = CreateProcess();
            var affinity = new RecordingAffinityApplyService();
            var coordinator = CreateCoordinator(affinity, topologyProvider: null);

            var result = await coordinator.ApplyCoreMaskAsync(process, CreateMask([true, false, true]));

            Assert.True(result.Success);
            Assert.Equal(1, affinity.LegacyApplyCalls);
            Assert.Equal(0b101, affinity.LastLegacyMask);
            Assert.Equal(0, affinity.CpuSelectionApplyCalls);
        }

        [Fact]
        public async Task ApplyCoreMaskAsync_WithoutCpuSelection_MigratesToCpuSelectionWhenTopologyIsAvailable()
        {
            var process = CreateProcess();
            var topology = CpuTopologySnapshot.Create(
                [new ProcessorRef(0, 0, 0), new ProcessorRef(0, 1, 1), new ProcessorRef(0, 2, 2)]);
            var affinity = new RecordingAffinityApplyService();
            var coordinator = CreateCoordinator(affinity, new FakeCpuTopologyProvider(topology));

            var result = await coordinator.ApplyCoreMaskAsync(process, CreateMask([true, false, true]));

            Assert.True(result.Success);
            Assert.Equal(1, affinity.CpuSelectionApplyCalls);
            Assert.Equal(0, affinity.LegacyApplyCalls);
            Assert.Equal([0, 2], affinity.LastSelection!.GlobalLogicalProcessorIndexes);
        }

        [Fact]
        public async Task ApplyCoreMaskAsync_WithCpu64BoolMaskAndTopology_UsesCpuSelectionPath()
        {
            var process = CreateProcess();
            var processors = Enumerable.Range(0, 65)
                .Select(index => index < 64
                    ? new ProcessorRef(0, (byte)index, index)
                    : new ProcessorRef(1, 0, index))
                .ToList();
            var topology = CpuTopologySnapshot.Create(processors);
            var boolMask = Enumerable.Range(0, 65).Select(index => index == 64).ToList();
            var affinity = new RecordingAffinityApplyService();
            var coordinator = CreateCoordinator(affinity, new FakeCpuTopologyProvider(topology));

            var result = await coordinator.ApplyCoreMaskAsync(process, CreateMask(boolMask));

            Assert.True(result.Success);
            Assert.Equal(1, affinity.CpuSelectionApplyCalls);
            Assert.Equal(0, affinity.LegacyApplyCalls);
            var applied = Assert.Single(affinity.LastSelection!.LogicalProcessors);
            Assert.Equal(new ProcessorRef(1, 0, 64), applied);
            Assert.DoesNotContain(affinity.LastSelection.LogicalProcessors, processor => processor.GlobalIndex == 0);
        }

        [Fact]
        public async Task ApplyCoreMaskAsync_WhenTopologyUnavailableAndMaskIsUnsafe_BlocksLegacyFallback()
        {
            var process = CreateProcess();
            var boolMask = Enumerable.Range(0, 65).Select(index => index == 64).ToList();
            var affinity = new RecordingAffinityApplyService();
            var coordinator = CreateCoordinator(affinity, topologyProvider: null);

            var result = await coordinator.ApplyCoreMaskAsync(process, CreateMask(boolMask));

            Assert.False(result.Success);
            Assert.Equal(AffinityApplyErrorCodes.LegacyFallbackUnsafe, result.ErrorCode);
            Assert.Equal(ProcessOperationUserMessages.LegacyFallbackBlocked, result.UserMessage);
            Assert.Equal(0, affinity.LegacyApplyCalls);
            Assert.Equal(0, affinity.CpuSelectionApplyCalls);
        }

        [Fact]
        public async Task ApplyCoreMaskAsync_WhenCpuSelectionAccessDenied_ReturnsSafeAccessDeniedMessage()
        {
            var process = CreateProcess();
            var affinity = new RecordingAffinityApplyService
            {
                CpuSelectionResult = AffinityApplyResult.Failed(
                    AffinityApplyErrorCodes.AccessDenied,
                    ProcessOperationUserMessages.AccessDenied,
                    "Access is denied.",
                    isAccessDenied: true),
            };
            var mask = CreateMask([true]);
            mask.CpuSelection = new CpuSelection
            {
                LogicalProcessors = [new ProcessorRef(0, 0, 0)],
                GlobalLogicalProcessorIndexes = [0],
            };
            var coordinator = CreateCoordinator(affinity);

            var result = await coordinator.ApplyCoreMaskAsync(process, mask);

            Assert.False(result.Success);
            Assert.Equal(AffinityApplyErrorCodes.AccessDenied, result.ErrorCode);
            Assert.Equal(ProcessOperationUserMessages.AccessDenied, result.UserMessage);
            Assert.DoesNotContain("bypass", result.UserMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ApplyCoreMaskAsync_WhenCpuSelectionAntiCheatBlocked_ReturnsNoBypassMessage()
        {
            var process = CreateProcess();
            var affinity = new RecordingAffinityApplyService
            {
                CpuSelectionResult = AffinityApplyResult.Failed(
                    AffinityApplyErrorCodes.AntiCheatOrProtectedProcessLikely,
                    ProcessOperationUserMessages.AntiCheatProtectedLikely,
                    "Protected process.",
                    isAccessDenied: true,
                    isAntiCheatLikely: true),
            };
            var mask = CreateMask([true]);
            mask.CpuSelection = new CpuSelection
            {
                LogicalProcessors = [new ProcessorRef(0, 0, 0)],
                GlobalLogicalProcessorIndexes = [0],
            };
            var coordinator = CreateCoordinator(affinity);

            var result = await coordinator.ApplyCoreMaskAsync(process, mask);

            Assert.False(result.Success);
            Assert.Equal(AffinityApplyErrorCodes.AntiCheatOrProtectedProcessLikely, result.ErrorCode);
            Assert.Equal(ProcessOperationUserMessages.AntiCheatProtectedLikely, result.UserMessage);
            Assert.Contains("will not try to bypass", result.UserMessage, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("disable anti-cheat", result.UserMessage, StringComparison.OrdinalIgnoreCase);
        }

        private static ProcessAffinityApplyCoordinator CreateCoordinator(
            RecordingAffinityApplyService affinity,
            ICpuTopologyProvider? topologyProvider = null) =>
            new(
                affinity,
                topologyProvider,
                new CpuSelectionMigrationService(),
                NullLogger<ProcessAffinityApplyCoordinator>.Instance);

        private static ProcessModel CreateProcess() =>
            new()
            {
                ProcessId = 42,
                Name = "game.exe",
                ProcessorAffinity = 1,
            };

        private static CoreMask CreateMask(IReadOnlyList<bool> boolMask)
        {
            var mask = new CoreMask { Name = "Manual" };
            foreach (var bit in boolMask)
            {
                mask.BoolMask.Add(bit);
            }

            return mask;
        }

        private sealed class FakeCpuTopologyProvider(CpuTopologySnapshot snapshot) : ICpuTopologyProvider
        {
            public Task<CpuTopologySnapshot> GetTopologySnapshotAsync(CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(snapshot);
            }
        }

        private sealed class RecordingAffinityApplyService : IAffinityApplyService
        {
            public int LegacyApplyCalls { get; private set; }

            public int CpuSelectionApplyCalls { get; private set; }

            public long? LastLegacyMask { get; private set; }

            public CpuSelection? LastSelection { get; private set; }

            public AffinityApplyResult LegacyResult { get; init; } =
                AffinityApplyResult.SucceededWithLegacyFallback(1, 1);

            public AffinityApplyResult CpuSelectionResult { get; init; } =
                AffinityApplyResult.SucceededWithCpuSets("CPU Sets applied.");

            public Task<AffinityApplyResult> ApplyAsync(ProcessModel process, long requestedMask)
            {
                this.LegacyApplyCalls++;
                this.LastLegacyMask = requestedMask;
                return Task.FromResult(this.LegacyResult);
            }

            public Task<AffinityApplyResult> ApplyAsync(ProcessModel process, CpuSelection selection)
            {
                this.CpuSelectionApplyCalls++;
                this.LastSelection = selection;
                return Task.FromResult(this.CpuSelectionResult);
            }
        }
    }
}
