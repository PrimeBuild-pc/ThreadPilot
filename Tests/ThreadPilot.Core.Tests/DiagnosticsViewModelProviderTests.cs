namespace ThreadPilot.Core.Tests
{
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using ThreadPilot.Models;
    using ThreadPilot.Services;
    using ThreadPilot.ViewModels;

    public sealed class DiagnosticsViewModelProviderTests
    {
        [Fact]
        public void Constructor_DoesNotCreatePerformanceViewModel()
        {
            var factoryCalls = 0;
            var provider = new DiagnosticsViewModelProvider(
                new Lazy<PerformanceViewModel>(() =>
                {
                    factoryCalls++;
                    throw new InvalidOperationException("PerformanceViewModel should be lazy.");
                }));

            Assert.False(provider.IsCreated);
            Assert.Equal(0, factoryCalls);
        }

        [Fact]
        public void GetOrCreate_CreatesPerformanceViewModelOnce()
        {
            var performanceViewModel = CreatePerformanceViewModel();
            var factoryCalls = 0;
            var provider = new DiagnosticsViewModelProvider(
                new Lazy<PerformanceViewModel>(() =>
                {
                    factoryCalls++;
                    return performanceViewModel;
                }));

            var first = provider.GetOrCreate();
            var second = provider.GetOrCreate();

            Assert.Same(performanceViewModel, first);
            Assert.Same(first, second);
            Assert.True(provider.IsCreated);
            Assert.Equal(1, factoryCalls);
        }

        private static PerformanceViewModel CreatePerformanceViewModel()
        {
            var performance = new Mock<IPerformanceMonitoringService>(MockBehavior.Strict);
            var process = new Mock<IProcessService>(MockBehavior.Strict);
            var associations = new Mock<IProcessPowerPlanAssociationService>(MockBehavior.Strict);
            var powerPlan = new Mock<IPowerPlanService>(MockBehavior.Strict);
            var processMonitorManager = new Mock<IProcessMonitorManagerService>(MockBehavior.Strict);
            var systemTweaks = new Mock<ISystemTweaksService>(MockBehavior.Strict);

            associations
                .Setup(x => x.GetAssociationsAsync())
                .ReturnsAsync(Array.Empty<ProcessPowerPlanAssociation>());

            return new PerformanceViewModel(
                performance.Object,
                process.Object,
                associations.Object,
                powerPlan.Object,
                processMonitorManager.Object,
                systemTweaks.Object,
                NullLogger<PerformanceViewModel>.Instance);
        }
    }
}
