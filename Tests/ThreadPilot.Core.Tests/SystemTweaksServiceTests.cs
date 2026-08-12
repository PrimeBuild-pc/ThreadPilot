namespace ThreadPilot.Core.Tests
{
    using ThreadPilot.Services;

    public sealed class SystemTweaksServiceTests
    {
        [Fact]
        public void GetHighSchedulingCategoryRegistryValue_WhenEnabled_ReturnsHigh()
        {
            var value = SystemTweaksService.GetHighSchedulingCategoryRegistryValue(enabled: true);

            Assert.Equal("High", value);
        }

        [Fact]
        public void GetHighSchedulingCategoryRegistryValue_WhenDisabled_KeepsDefaultRevertValue()
        {
            var value = SystemTweaksService.GetHighSchedulingCategoryRegistryValue(enabled: false);

            Assert.Equal("Medium", value);
        }

        [Fact]
        public void GetPointerPrecisionValues_WhenDisabled_ClearsAccelerationAndThresholds()
        {
            var values = SystemTweaksService.GetPointerPrecisionValues(enabled: false, [6, 10, 1]);

            Assert.Equal([0, 0, 0], values);
        }

        [Fact]
        public void GetPointerPrecisionValues_WhenEnabled_RestoresWindowsDefaultsAfterDisable()
        {
            var values = SystemTweaksService.GetPointerPrecisionValues(enabled: true, [0, 0, 0]);

            Assert.Equal([6, 10, 1], values);
        }

        [Fact]
        public void GetPointerPrecisionValues_WhenEnabled_PreservesExistingNonZeroValues()
        {
            var values = SystemTweaksService.GetPointerPrecisionValues(enabled: true, [4, 8, 2]);

            Assert.Equal([4, 8, 2], values);
        }
    }
}
