namespace ThreadPilot.Core.Tests
{
    using System.Reflection;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using ThreadPilot.Services;

    public sealed class PerformanceMonitoringServiceTests
    {
        [Fact]
        public void BuildCpuCoreUsages_PreservesLogicalProcessorIdsAcrossMissingCounters()
        {
            var usages = PerformanceMonitoringService.BuildCpuCoreUsages(
                [(0, 10d), (3, 30d)],
                topology: null);

            Assert.Equal([0, 3], usages.Select(usage => usage.CoreId));
            Assert.Equal(["Core 0", "Core 3"], usages.Select(usage => usage.CoreName));
        }

        [Fact]
        public async Task StopStart_DoesNotClearAnInFlightTickGuard()
        {
            using var service = CreateService();
            var tickField = typeof(PerformanceMonitoringService).GetField(
                "isMonitoringTickInProgress",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            await service.StartMonitoringAsync();
            tickField.SetValue(service, 1);

            await service.StopMonitoringAsync();
            await service.StartMonitoringAsync();

            Assert.Equal(1, tickField.GetValue(service));
        }

        private static PerformanceMonitoringService CreateService() =>
            new(
                NullLogger<PerformanceMonitoringService>.Instance,
                new Mock<IProcessService>().Object,
                new Mock<ICpuTopologyService>().Object,
                new Mock<IApplicationSettingsService>().Object,
                new Mock<IEnhancedLoggingService>().Object);
    }
}
