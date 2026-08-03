namespace ThreadPilot.Core.Tests
{
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using ThreadPilot.Models;
    using ThreadPilot.Services;

    public sealed class ProcessMonitorServiceSettingsTests
    {
        [Fact]
        public async Task StartMonitoringAsync_WhenWmiDisabled_DoesNotReportWmiAsAvailable()
        {
            using var monitor = CreateMonitor(new ApplicationSettingsModel
            {
                EnableWmiMonitoring = false,
                EnableFallbackPolling = false,
            });

            await monitor.StartMonitoringAsync();

            Assert.False(monitor.IsWmiAvailable);
        }

        [Fact]
        public async Task StartMonitoringAsync_WhenFallbackDisabled_DoesNotActivateFallbackWhenWmiIsDisabled()
        {
            using var monitor = CreateMonitor(new ApplicationSettingsModel
            {
                EnableWmiMonitoring = false,
                EnableFallbackPolling = false,
            });

            await monitor.StartMonitoringAsync();

            Assert.False(monitor.IsFallbackPollingActive);
        }

        [Fact]
        public async Task StartMonitoringAsync_UsesFallbackPollingIntervalFromApplicationSettings()
        {
            using var monitor = CreateMonitor(new ApplicationSettingsModel
            {
                EnableWmiMonitoring = false,
                EnableFallbackPolling = true,
                FallbackPollingIntervalMs = 12345,
            });
            var messages = new List<string?>();
            monitor.MonitoringStatusChanged += (_, status) => messages.Add(status.StatusMessage);

            await monitor.StartMonitoringAsync();

            Assert.True(monitor.IsFallbackPollingActive);
            Assert.Contains("Fallback polling started (interval: 12345ms)", messages);
        }

        private static ProcessMonitorService CreateMonitor(ApplicationSettingsModel settings)
        {
            var processService = new Mock<IProcessService>(MockBehavior.Strict);
            processService.Setup(service => service.GetProcessesAsync())
                .ReturnsAsync(new System.Collections.ObjectModel.ObservableCollection<ProcessModel>());
            var settingsService = new Mock<IApplicationSettingsService>(MockBehavior.Strict);
            settingsService.SetupGet(service => service.Settings).Returns(settings);
            return new ProcessMonitorService(
                processService.Object,
                settingsService.Object,
                NullLogger<ProcessMonitorService>.Instance);
        }
    }
}
