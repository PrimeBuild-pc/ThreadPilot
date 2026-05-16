namespace ThreadPilot.Core.Tests
{
    using ThreadPilot.Services;

    public sealed class SelfResourcePolicyTests
    {
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void TryCreateLowImpactAffinityMask_DisablesAffinityOnSmallSystems(int logicalProcessorCount)
        {
            Assert.False(SelfResourcePolicy.TryCreateLowImpactAffinityMask(logicalProcessorCount, out _));
        }

        [Theory]
        [InlineData(3, 0b100)]
        [InlineData(4, 0b1100)]
        [InlineData(8, 0b1100_0000)]
        public void TryCreateLowImpactAffinityMask_UsesLastLogicalProcessors(
            int logicalProcessorCount,
            long expectedMask)
        {
            Assert.True(SelfResourcePolicy.TryCreateLowImpactAffinityMask(logicalProcessorCount, out var mask));
            Assert.Equal(expectedMask, mask);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(64)]
        [InlineData(128)]
        public void TryCreateLowImpactAffinityMask_RejectsInvalidProcessorCounts(int logicalProcessorCount)
        {
            Assert.False(SelfResourcePolicy.TryCreateLowImpactAffinityMask(logicalProcessorCount, out _));
        }
    }
}
