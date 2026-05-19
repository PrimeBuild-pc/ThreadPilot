namespace ThreadPilot.Core.Tests
{
    using ThreadPilot.Models;

    public sealed class CpuSelectionTests
    {
        [Fact]
        public void CpuSelection_WithGlobalIndex64_DoesNotAliasCpu0InLegacyMask()
        {
            var topology = CpuTopologySnapshot.Create(
            [
                new ProcessorRef(0, 0, 0),
                new ProcessorRef(1, 0, 64),
            ]);

            var selection = CpuSelection.FromProcessors(
                [new ProcessorRef(1, 0, 64)],
                topology);

            var legacyMask = CpuSelection.ToLegacyAffinityMaskOrNull(selection);

            Assert.Null(legacyMask);
            Assert.Contains(selection.LogicalProcessors, p => p.GlobalIndex == 64);
            Assert.DoesNotContain(selection.LogicalProcessors, p => p.GlobalIndex == 0);
        }

        [Fact]
        public void CoreMask_ToProcessorAffinity_WithCpu64Only_DocumentsLegacyAliasBug()
        {
            var mask = new CoreMask { Name = "CPU64 Only" };
            for (var i = 0; i < 65; i++)
            {
                mask.BoolMask.Add(i == 64);
            }

            var legacyAffinity = mask.ToProcessorAffinity();

            Assert.Equal(1, legacyAffinity);
            Assert.True((legacyAffinity & 1L) != 0);
        }

        [Fact]
        public void CpuTopologySnapshot_KeepsProcessorsWithSameLogicalIndexInDifferentGroupsDistinct()
        {
            var group0Cpu0 = new ProcessorRef(0, 0, 0);
            var group1Cpu0 = new ProcessorRef(1, 0, 64);
            var topology = CpuTopologySnapshot.Create(
                [group0Cpu0, group1Cpu0],
                new Dictionary<ProcessorRef, uint>
                {
                    [group0Cpu0] = 100,
                    [group1Cpu0] = 200,
                });

            Assert.True(topology.TryGetCpuSetId(group0Cpu0, out var group0CpuSetId));
            Assert.True(topology.TryGetCpuSetId(group1Cpu0, out var group1CpuSetId));
            Assert.Equal(100U, group0CpuSetId);
            Assert.Equal(200U, group1CpuSetId);
            Assert.Equal(2, topology.LogicalProcessors.Count);
        }

        [Fact]
        public void CpuTopologySnapshot_Create_ThrowsWhenGlobalIndexIsDuplicated()
        {
            var processors = new[]
            {
                new ProcessorRef(0, 0, 0),
                new ProcessorRef(0, 1, 0),
            };

            var exception = Assert.Throws<ArgumentException>(() => CpuTopologySnapshot.Create(processors));
            Assert.Contains("GlobalIndex", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void CpuTopologySnapshot_PerformanceEfficiencyClass_IsHighestNumericValue()
        {
            var eCore = new ProcessorRef(0, 8, 8);
            var pCore = new ProcessorRef(0, 0, 0);
            var topology = CpuTopologySnapshot.Create(
                [pCore, eCore],
                efficiencyClasses: new Dictionary<ProcessorRef, byte>
                {
                    [pCore] = 2,
                    [eCore] = 0,
                });

            Assert.Equal<byte?>(2, topology.GetPerformanceEfficiencyClass());
        }

        [Fact]
        public void FromLegacyAffinityMask_SelectsOnlyRepresentableProcessors()
        {
            var topology = CpuTopologySnapshot.Create(
            [
                new ProcessorRef(0, 0, 0),
                new ProcessorRef(0, 1, 1),
                new ProcessorRef(1, 0, 64),
            ]);

            var selection = CpuSelection.FromLegacyAffinityMask(0b11, topology);

            Assert.Equal([0, 1], selection.GlobalLogicalProcessorIndexes);
            Assert.DoesNotContain(selection.LogicalProcessors, p => p.GlobalIndex == 64);
        }

        [Fact]
        public void ToLegacyAffinityMaskOrNull_ReturnsMaskForSingleGroupBelow64()
        {
            var topology = CpuTopologySnapshot.Create(
            [
                new ProcessorRef(0, 0, 0),
                new ProcessorRef(0, 3, 3),
            ]);
            var selection = CpuSelection.FromProcessors(topology.LogicalProcessors, topology);

            var legacyMask = CpuSelection.ToLegacyAffinityMaskOrNull(selection);

            Assert.Equal(0b1001, legacyMask);
        }

        [Fact]
        public void FromProcessors_ThrowsWhenProcessorIsNotInTopology()
        {
            var topology = CpuTopologySnapshot.Create([new ProcessorRef(0, 0, 0)]);
            var missingProcessor = new ProcessorRef(0, 1, 1);

            var exception = Assert.Throws<ArgumentException>(() =>
                CpuSelection.FromProcessors([missingProcessor], topology));

            Assert.Contains("topology", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ToLegacyAffinityMaskOrNull_ReturnsNullForMultipleProcessorGroups()
        {
            var topology = CpuTopologySnapshot.Create(
            [
                new ProcessorRef(0, 0, 0),
                new ProcessorRef(1, 0, 64),
            ]);
            var selection = CpuSelection.FromProcessors(topology.LogicalProcessors, topology);

            var legacyMask = CpuSelection.ToLegacyAffinityMaskOrNull(selection);

            Assert.Null(legacyMask);
        }
    }
}
