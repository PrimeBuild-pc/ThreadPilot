namespace ThreadPilot.Core.Tests
{
    using ThreadPilot.Services;

    public sealed class ProcessMonitorServiceTests
    {
        [Theory]
        [InlineData(0, 10)]
        [InlineData(1, 30)]
        [InlineData(2, 60)]
        [InlineData(3, 300)]
        [InlineData(10, 300)]
        public void GetWmiRetryDelay_BacksOffAfterFailures(int failureCount, int expectedSeconds)
        {
            Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), ProcessMonitorService.GetWmiRetryDelay(failureCount));
        }
    }
}
