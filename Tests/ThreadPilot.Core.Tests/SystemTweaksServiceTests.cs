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
    }
}
