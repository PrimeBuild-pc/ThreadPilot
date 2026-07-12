namespace ThreadPilot.Core.Tests
{
    using System.Collections.Concurrent;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using ThreadPilot.Models;
    using ThreadPilot.Services;

    public sealed class ProcessMonitorServiceTests
    {
        [Fact]
        public async Task GetRunningProcessesAsync_WhileMonitoring_ReusesMonitorSnapshot()
        {
            var processService = new Mock<IProcessService>(MockBehavior.Strict);
            var settingsService = new Mock<IApplicationSettingsService>(MockBehavior.Loose);
            settingsService.SetupGet(service => service.Settings).Returns(new ApplicationSettingsModel());
            using var monitor = new ProcessMonitorService(
                processService.Object,
                settingsService.Object,
                NullLogger<ProcessMonitorService>.Instance);
            var process = new ProcessModel { ProcessId = 42, Name = "cached" };
            var runningProcesses = (ConcurrentDictionary<int, ProcessModel>)typeof(ProcessMonitorService)
                .GetField("runningProcesses", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .GetValue(monitor)!;
            runningProcesses[process.ProcessId] = process;
            typeof(ProcessMonitorService)
                .GetField("isMonitoring", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .SetValue(monitor, true);

            var snapshot = await monitor.GetRunningProcessesAsync();

            Assert.Same(process, Assert.Single(snapshot));
            processService.Verify(service => service.GetProcessesAsync(), Times.Never);
        }

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
