namespace ThreadPilot.Core.Tests
{
    using ThreadPilot.Models;
    using ThreadPilot.Platforms.Windows;

    public sealed class ProcessThreadCpuAssignmentHandlerTests
    {
        [Fact]
        public void BuildGroupAffinities_PreservesProcessorGroupsWithoutCpu64Aliasing()
        {
            var groups = ProcessThreadCpuAssignmentHandler.BuildGroupAffinities(
            [
                new ProcessorRef(0, 0, 0),
                new ProcessorRef(0, 63, 63),
                new ProcessorRef(1, 0, 64),
                new ProcessorRef(1, 2, 66),
            ]);

            Assert.Equal(2, groups.Count);
            Assert.Equal((ushort)0, groups[0].Group);
            Assert.Equal((1UL << 63) | 1UL, (ulong)groups[0].Mask);
            Assert.Equal((ushort)1, groups[1].Group);
            Assert.Equal((nuint)5, groups[1].Mask);
        }

        [Fact]
        public void BuildIdealProcessorSequence_IsSortedDistinctAndStableRoundRobin()
        {
            var sequence = ProcessThreadCpuAssignmentHandler.BuildIdealProcessorSequence(
            [
                new ProcessorRef(1, 2, 66),
                new ProcessorRef(0, 3, 3),
                new ProcessorRef(0, 1, 1),
                new ProcessorRef(0, 1, 1),
            ],
            threadCount: 7);

            Assert.Equal(
            [
                new ProcessorRef(0, 1, 1),
                new ProcessorRef(0, 3, 3),
                new ProcessorRef(1, 2, 66),
                new ProcessorRef(0, 1, 1),
                new ProcessorRef(0, 3, 3),
                new ProcessorRef(1, 2, 66),
                new ProcessorRef(0, 1, 1),
            ],
            sequence);
        }

        [Fact]
        public void Builders_ReturnEmptyForEmptySelections()
        {
            Assert.Empty(ProcessThreadCpuAssignmentHandler.BuildGroupAffinities([]));
            Assert.Empty(ProcessThreadCpuAssignmentHandler.BuildIdealProcessorSequence([], 4));
        }
    }
}
