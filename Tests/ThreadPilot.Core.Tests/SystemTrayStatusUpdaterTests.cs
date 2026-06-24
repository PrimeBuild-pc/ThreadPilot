namespace ThreadPilot.Core.Tests
{
    using System.Collections.ObjectModel;
    using Moq;
    using ThreadPilot.Models;
    using ThreadPilot.Services;

    public sealed class SystemTrayStatusUpdaterTests
    {
        [Fact]
        public async Task UpdateContextMenuAsync_DiagnosticsHidden_DoesNotResolvePerformanceService()
        {
            var harness = new Harness();
            var updater = harness.CreateUpdater(performanceFactory: () => throw new InvalidOperationException("Performance service should not be resolved."));

            await updater.UpdateContextMenuAsync(harness.Tray.Object);

            harness.Tray.Verify(x => x.UpdatePowerPlans(It.IsAny<IEnumerable<PowerPlanModel>>(), It.IsAny<PowerPlanModel?>()), Times.Once);
            harness.Tray.Verify(x => x.UpdateProfiles(It.IsAny<IEnumerable<string>>()), Times.Once);
            harness.Tray.Verify(x => x.UpdateSystemStatus("Balanced"), Times.Once);
            harness.Tray.Verify(x => x.UpdateSystemStatus(It.IsAny<string>(), It.IsAny<double>(), It.IsAny<double>()), Times.Never);
        }

        [Fact]
        public async Task UpdateStatusAsync_DiagnosticsHidden_DoesNotRequestLightweightMetrics()
        {
            var harness = new Harness();
            var updater = harness.CreateUpdater(performanceFactory: () => throw new InvalidOperationException("Performance service should not be resolved."));

            var updated = await updater.UpdateStatusAsync(harness.Tray.Object, action =>
            {
                action();
                return Task.CompletedTask;
            });

            Assert.True(updated);
            Assert.False(updater.ShouldRunPerformanceStatusUpdates);
            harness.Tray.Verify(x => x.UpdateSystemStatus("Balanced"), Times.Once);
            harness.Tray.Verify(x => x.UpdateSystemStatus(It.IsAny<string>(), It.IsAny<double>(), It.IsAny<double>()), Times.Never);
        }

        private sealed class Harness
        {
            public Mock<ISystemTrayService> Tray { get; } = new(MockBehavior.Strict);

            public Mock<IPowerPlanService> PowerPlan { get; } = new(MockBehavior.Strict);

            public Harness()
            {
                var activePlan = new PowerPlanModel { Guid = "balanced", Name = "Balanced" };
                this.PowerPlan
                    .Setup(x => x.GetPowerPlansAsync())
                    .ReturnsAsync(new ObservableCollection<PowerPlanModel> { activePlan });
                this.PowerPlan
                    .Setup(x => x.GetActivePowerPlan())
                    .ReturnsAsync(activePlan);

                this.Tray
                    .Setup(x => x.UpdatePowerPlans(It.IsAny<IEnumerable<PowerPlanModel>>(), It.IsAny<PowerPlanModel?>()));
                this.Tray
                    .Setup(x => x.UpdateProfiles(It.IsAny<IEnumerable<string>>()));
                this.Tray
                    .Setup(x => x.UpdateSystemStatus(It.IsAny<string>()));
            }

            public SystemTrayStatusUpdater CreateUpdater(Func<IPerformanceMonitoringService> performanceFactory) =>
                new(
                    this.PowerPlan.Object,
                    new Lazy<IPerformanceMonitoringService>(performanceFactory));
        }
    }
}
