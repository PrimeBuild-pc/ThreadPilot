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

        [Theory]
        [InlineData(false, true, true, false)]
        [InlineData(true, false, true, false)]
        [InlineData(true, true, false, false)]
        [InlineData(true, true, true, true)]
        public void ShouldLimitAffinity_RequiresHiddenLowImpactModeAndAffinitySetting(
            bool isHidden,
            bool enableSelfLowImpactMode,
            bool enableSelfAffinityLimit,
            bool expected)
        {
            Assert.Equal(
                expected,
                SelfResourcePolicy.ShouldLimitAffinity(isHidden, enableSelfLowImpactMode, enableSelfAffinityLimit));
        }
    }
}
