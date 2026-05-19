namespace ThreadPilot.Core.Tests
{
    using System.Threading;
    using ThreadPilot.Models;
    using ThreadPilot.Services;

    public sealed class CpuTopologyProviderTests
    {
        [Fact]
        public async Task FakeProvider_ReturnsSingleGroupEightLogicalProcessors()
        {
            var processors = CreateSequentialProcessors(8).ToList();
            var topology = CpuTopologySnapshot.Create(processors);
            var provider = new FakeCpuTopologyProvider(topology);

            var snapshot = await provider.GetTopologySnapshotAsync(CancellationToken.None);

            Assert.Equal(8, snapshot.LogicalProcessors.Count);
            Assert.All(snapshot.LogicalProcessors, processor => Assert.Equal<ushort>(0, processor.Group));
            Assert.Equal(1, snapshot.Signature.ProcessorGroupCount);
        }

        [Fact]
        public void Snapshot_MultiGroupCpuZeroEntriesRemainDistinct()
        {
            var group0Cpu0 = new ProcessorRef(0, 0, 0);
            var group1Cpu0 = new ProcessorRef(1, 0, 64);

            var topology = CpuTopologySnapshot.Create(
                [group0Cpu0, group1Cpu0],
                cpuSetIds: new Dictionary<ProcessorRef, uint>
                {
                    [group0Cpu0] = 100,
                    [group1Cpu0] = 200,
                });

            Assert.True(topology.TryGetCpuSetId(group0Cpu0, out var group0CpuSetId));
            Assert.True(topology.TryGetCpuSetId(group1Cpu0, out var group1CpuSetId));
            Assert.Equal(100U, group0CpuSetId);
            Assert.Equal(200U, group1CpuSetId);
            Assert.Equal(2, topology.Signature.ProcessorGroupCount);
        }

        [Fact]
        public void Snapshot_PerformanceEfficiencyClass_UsesHighestClass()
        {
            var pCore = new ProcessorRef(0, 0, 0);
            var eCore = new ProcessorRef(0, 1, 1);

            var topology = CpuTopologySnapshot.Create(
                [pCore, eCore],
                efficiencyClasses: new Dictionary<ProcessorRef, byte>
                {
                    [pCore] = 2,
                    [eCore] = 0,
                });

            Assert.Equal<byte?>(2, topology.GetPerformanceEfficiencyClass());
            Assert.True(topology.TryGetEfficiencyClass(pCore, out var pCoreClass));
            Assert.Equal(2, pCoreClass);
        }

        [Fact]
        public void Snapshot_WithoutEfficiencyClasses_IsValid()
        {
            var topology = CpuTopologySnapshot.Create(CreateSequentialProcessors(4));

            Assert.Null(topology.GetPerformanceEfficiencyClass());
            Assert.False(topology.TryGetEfficiencyClass(new ProcessorRef(0, 0, 0), out _));
            Assert.Equal(4, topology.Signature.LogicalProcessorCount);
        }

        [Fact]
        public void Snapshot_WithSmtOn_MapsSiblingGroupsByCore()
        {
            var cpu0 = new ProcessorRef(0, 0, 0);
            var cpu1 = new ProcessorRef(0, 1, 1);
            var cpu2 = new ProcessorRef(0, 2, 2);
            var cpu3 = new ProcessorRef(0, 3, 3);

            var topology = CpuTopologySnapshot.Create(
                [cpu0, cpu1, cpu2, cpu3],
                coreIndexes: new Dictionary<ProcessorRef, int>
                {
                    [cpu0] = 0,
                    [cpu1] = 0,
                    [cpu2] = 1,
                    [cpu3] = 1,
                },
                smtSiblingGlobalIndexes: new Dictionary<ProcessorRef, IReadOnlyList<int>>
                {
                    [cpu0] = [1],
                    [cpu1] = [0],
                    [cpu2] = [3],
                    [cpu3] = [2],
                },
                signature: new CpuTopologySignature
                {
                    LogicalProcessorCount = 4,
                    PhysicalCoreCount = 2,
                    ProcessorGroupCount = 1,
                    Source = "Test",
                });

            Assert.Equal(2, topology.Signature.PhysicalCoreCount);
            Assert.True(topology.TryGetCoreIndex(cpu0, out var cpu0CoreIndex));
            Assert.True(topology.TryGetCoreIndex(cpu1, out var cpu1CoreIndex));
            Assert.Equal(0, cpu0CoreIndex);
            Assert.Equal(cpu0CoreIndex, cpu1CoreIndex);
            Assert.Equal([1], topology.GetSmtSiblingGlobalIndexes(cpu0));
            Assert.Equal([0], topology.GetSmtSiblingGlobalIndexes(cpu1));
        }

        [Fact]
        public void Snapshot_WithSmtOff_HasOneLogicalProcessorPerCore()
        {
            var processors = CreateSequentialProcessors(8).ToList();
            var coreIndexes = processors.ToDictionary(processor => processor, processor => processor.GlobalIndex);

            var topology = CpuTopologySnapshot.Create(
                processors,
                coreIndexes: coreIndexes,
                signature: new CpuTopologySignature
                {
                    LogicalProcessorCount = 8,
                    PhysicalCoreCount = 8,
                    ProcessorGroupCount = 1,
                    Source = "Test",
                });

            Assert.Equal(8, topology.Signature.PhysicalCoreCount);
            Assert.All(processors, processor =>
            {
                Assert.True(topology.TryGetCoreIndex(processor, out var coreIndex));
                Assert.Equal(processor.GlobalIndex, coreIndex);
                Assert.Empty(topology.GetSmtSiblingGlobalIndexes(processor));
            });
        }

        [Fact]
        public void Snapshot_DualCcdCacheGroups_AreRepresentedByLastLevelCacheIndex()
        {
            var processors = CreateSequentialProcessors(12).ToList();
            var l3Indexes = processors.ToDictionary(
                processor => processor,
                processor => processor.GlobalIndex < 6 ? 0 : 1);

            var topology = CpuTopologySnapshot.Create(
                processors,
                lastLevelCacheIndexes: l3Indexes,
                signature: new CpuTopologySignature
                {
                    LogicalProcessorCount = 12,
                    PhysicalCoreCount = 12,
                    ProcessorGroupCount = 1,
                    LastLevelCacheGroupCount = 2,
                    Source = "Test",
                });

            Assert.Equal(2, topology.Signature.LastLevelCacheGroupCount);
            Assert.All(processors.Take(6), processor =>
            {
                Assert.True(topology.TryGetLastLevelCacheIndex(processor, out var cacheIndex));
                Assert.Equal(0, cacheIndex);
            });
            Assert.All(processors.Skip(6), processor =>
            {
                Assert.True(topology.TryGetLastLevelCacheIndex(processor, out var cacheIndex));
                Assert.Equal(1, cacheIndex);
            });
        }

        [Fact]
        public void Snapshot_WithMoreThan64LogicalProcessors_IsValid()
        {
            var processors = Enumerable.Range(0, 72)
                .Select(index => new ProcessorRef((ushort)(index / 64), (byte)(index % 64), index))
                .ToList();

            var topology = CpuTopologySnapshot.Create(processors);

            Assert.Equal(72, topology.LogicalProcessors.Count);
            Assert.Equal(2, topology.Signature.ProcessorGroupCount);
            Assert.Contains(topology.LogicalProcessors, processor => processor.GlobalIndex == 64 && processor.Group == 1);
        }

        private static IEnumerable<ProcessorRef> CreateSequentialProcessors(int count)
        {
            return Enumerable.Range(0, count)
                .Select(index => new ProcessorRef(0, (byte)index, index));
        }

        private sealed class FakeCpuTopologyProvider(CpuTopologySnapshot snapshot) : ICpuTopologyProvider
        {
            public Task<CpuTopologySnapshot> GetTopologySnapshotAsync(CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(snapshot);
            }
        }
    }
}
