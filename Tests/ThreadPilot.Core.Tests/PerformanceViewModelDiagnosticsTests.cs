namespace ThreadPilot.Core.Tests
{
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using ThreadPilot.Models;
    using ThreadPilot.Services;
    using ThreadPilot.ViewModels;

    public sealed class PerformanceViewModelDiagnosticsTests
    {
        [Fact]
        public async Task InitializeAsync_DoesNotStartLiveMonitoringOrScanProcesses()
        {
            var harness = new Harness();
            var viewModel = harness.CreateViewModel();

            await viewModel.InitializeAsync();

            harness.Performance.Verify(x => x.StartMonitoringAsync(), Times.Never);
            harness.Performance.Verify(x => x.GetSystemMetricsAsync(It.IsAny<bool>()), Times.Never);
            harness.Performance.Verify(x => x.GetTopCpuProcessesAsync(It.IsAny<int>()), Times.Never);
            harness.Performance.Verify(x => x.GetTopMemoryProcessesAsync(It.IsAny<int>()), Times.Never);
            harness.PowerPlan.Verify(x => x.GetActivePowerPlan(), Times.Never);
        }

        [Fact]
        public async Task ActivateDiagnosticsAsync_LoadsSnapshotWithoutStartingLiveMonitoring()
        {
            var harness = new Harness();
            var viewModel = harness.CreateViewModel();

            await viewModel.ActivateDiagnosticsAsync();

            harness.Performance.Verify(x => x.GetSystemMetricsAsync(false), Times.Once);
            harness.Performance.Verify(x => x.GetHistoricalDataAsync(TimeSpan.FromHours(1)), Times.Once);
            harness.Performance.Verify(x => x.GetTopCpuProcessesAsync(25), Times.Once);
            harness.Performance.Verify(x => x.GetTopMemoryProcessesAsync(25), Times.Once);
            harness.PowerPlan.Verify(x => x.GetActivePowerPlan(), Times.Once);
            harness.Performance.Verify(x => x.StartMonitoringAsync(), Times.Never);
            Assert.False(viewModel.IsMonitoring);
        }

        [Fact]
        public async Task SuspendBackgroundMonitoringAsync_StopsLiveMonitoringAndDoesNotAutoResume()
        {
            var harness = new Harness();
            var viewModel = harness.CreateViewModel();

            await viewModel.StartMonitoringCommand.ExecuteAsync(null);
            await viewModel.SuspendBackgroundMonitoringAsync();
            await viewModel.ResumeBackgroundMonitoringAsync();

            harness.Performance.Verify(x => x.StartMonitoringAsync(), Times.Once);
            harness.Performance.Verify(x => x.StopMonitoringAsync(), Times.Once);
            Assert.False(viewModel.IsMonitoring);
            Assert.Equal("Stopped", viewModel.MonitoringStateText);
        }

        [Fact]
        public void ShowAdvancedDiagnostics_DefaultsToHidden()
        {
            Assert.False(AppNavigationOptions.ShowAdvancedDiagnostics);
        }

        private sealed class Harness
        {
            public Mock<IPerformanceMonitoringService> Performance { get; } = new(MockBehavior.Strict);

            public Mock<IProcessService> Process { get; } = new(MockBehavior.Strict);

            public Mock<IProcessPowerPlanAssociationService> Associations { get; } = new(MockBehavior.Strict);

            public Mock<IPowerPlanService> PowerPlan { get; } = new(MockBehavior.Strict);

            public Mock<IProcessMonitorManagerService> ProcessMonitorManager { get; } = new(MockBehavior.Strict);

            public Mock<ISystemTweaksService> SystemTweaks { get; } = new(MockBehavior.Strict);

            public Harness()
            {
                this.Performance
                    .Setup(x => x.GetSystemMetricsAsync(It.IsAny<bool>()))
                    .ReturnsAsync(new SystemPerformanceMetrics());
                this.Performance
                    .Setup(x => x.GetHistoricalDataAsync(It.IsAny<TimeSpan>()))
                    .ReturnsAsync(new List<SystemPerformanceMetrics>());
                this.Performance
                    .Setup(x => x.GetTopCpuProcessesAsync(It.IsAny<int>()))
                    .ReturnsAsync(new List<ProcessPerformanceInfo>());
                this.Performance
                    .Setup(x => x.GetTopMemoryProcessesAsync(It.IsAny<int>()))
                    .ReturnsAsync(new List<ProcessPerformanceInfo>());
                this.Performance
                    .Setup(x => x.StartMonitoringAsync())
                    .Returns(Task.CompletedTask);
                this.Performance
                    .Setup(x => x.StopMonitoringAsync())
                    .Returns(Task.CompletedTask);

                this.Associations
                    .Setup(x => x.GetAssociationsAsync())
                    .ReturnsAsync(Array.Empty<ProcessPowerPlanAssociation>());

                this.PowerPlan
                    .Setup(x => x.GetActivePowerPlan())
                    .ReturnsAsync(new PowerPlanModel { Guid = "balanced", Name = "Balanced" });
            }

            public PerformanceViewModel CreateViewModel() =>
                new(
                    this.Performance.Object,
                    this.Process.Object,
                    this.Associations.Object,
                    this.PowerPlan.Object,
                    this.ProcessMonitorManager.Object,
                    this.SystemTweaks.Object,
                    NullLogger<PerformanceViewModel>.Instance);
        }
    }
}
