namespace ThreadPilot.Core.Tests
{
    using ThreadPilot.Services;

    public sealed class SystemTweaksServiceTests
    {
        [Fact]
        public void GetHighSchedulingCategoryRegistryValue_WhenEnabled_ReturnsWin32PrioritySeparation26()
        {
            var value = SystemTweaksService.GetHighSchedulingCategoryRegistryValue(enabled: true);

            Assert.Equal(26, value);
            Assert.Equal(0x1A, value);
        }

        [Fact]
        public void GetHighSchedulingCategoryRegistryValue_WhenDisabled_KeepsDefaultRevertValue()
        {
            var value = SystemTweaksService.GetHighSchedulingCategoryRegistryValue(enabled: false);

            Assert.Equal(2, value);
        }
    }
}
