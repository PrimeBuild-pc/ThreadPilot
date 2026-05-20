namespace ThreadPilot.Core.Tests
{
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using ThreadPilot.Models;
    using ThreadPilot.Services;
    using ThreadPilot.ViewModels;

    public sealed class ProcessViewModelAffinityTests
    {
        [Fact]
        public async Task SelectingCoreMask_DoesNotApplyProcessorAffinity()
        {
            var processService = new Mock<IProcessService>(MockBehavior.Loose);
            var gameModeService = new Mock<IGameModeService>(MockBehavior.Loose);
            gameModeService
                .Setup(service => service.DisableGameModeForAffinityAsync())
                .ReturnsAsync(false);
            var viewModel = CreateViewModel(processService.Object, gameModeService.Object);

            viewModel.SelectedProcess = new ProcessModel
            {
                ProcessId = 1234,
                Name = "Game",
                ProcessorAffinity = 3,
            };

            viewModel.SelectedCoreMask = CoreMask.FromProcessorAffinity(1, 2, "First Core");

            await Task.Delay(100);

            processService.Verify(
                service => service.SetProcessorAffinity(It.IsAny<ProcessModel>(), It.IsAny<long>()),
                Times.Never);
        }

        [Fact]
        public async Task SelectingCoreMask_ReportsPendingAffinityWithoutChangingCurrentAffinity()
        {
            var processService = new Mock<IProcessService>(MockBehavior.Loose);
            var gameModeService = new Mock<IGameModeService>(MockBehavior.Loose);
            var viewModel = CreateViewModel(processService.Object, gameModeService.Object);
            viewModel.CpuTopology = CreateTwoCoreTopology();
            viewModel.CpuCores = new System.Collections.ObjectModel.ObservableCollection<CpuCoreModel>(
                viewModel.CpuTopology.LogicalCores);

            viewModel.SelectedProcess = new ProcessModel
            {
                ProcessId = 1234,
                Name = "Game",
                ProcessorAffinity = 3,
            };

            viewModel.SelectedCoreMask = CoreMask.FromProcessorAffinity(1, 2, "First Core");

            await Task.Delay(100);

            Assert.True(viewModel.HasPendingAffinityEdits);
            Assert.Equal("Current OS affinity: 0x3", viewModel.CurrentAffinityText);
            Assert.Equal("Pending core mask: 0x1", viewModel.PendingAffinityText);
            Assert.Equal("Core mask staged. Use Apply Affinity to change Windows affinity.", viewModel.AffinityEditStateText);
        }

        [Fact]
        public void ConstructorFallbackCoordinator_ReceivesTopologyProviderWhenProvided()
        {
            var processService = new Mock<IProcessService>(MockBehavior.Loose);
            var gameModeService = new Mock<IGameModeService>(MockBehavior.Loose);
            var topologyProvider = new Mock<ICpuTopologyProvider>(MockBehavior.Strict);

            var viewModel = CreateViewModel(
                processService.Object,
                gameModeService.Object,
                cpuTopologyProvider: topologyProvider.Object);

            var coordinator = typeof(ProcessViewModel)
                .GetField(
                    "processAffinityApplyCoordinator",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .GetValue(viewModel);
            var provider = typeof(ProcessAffinityApplyCoordinator)
                .GetField(
                    "cpuTopologyProvider",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .GetValue(coordinator);

            Assert.Same(topologyProvider.Object, provider);
        }

        private static ProcessViewModel CreateViewModel(IProcessService processService, IGameModeService gameModeService)
            => CreateViewModel(processService, gameModeService, cpuTopologyProvider: null);

        private static ProcessViewModel CreateViewModel(
            IProcessService processService,
            IGameModeService gameModeService,
            ICpuTopologyProvider? cpuTopologyProvider)
        {
            var virtualizedProcessService = new Mock<IVirtualizedProcessService>(MockBehavior.Loose);
            virtualizedProcessService.SetupProperty(
                service => service.Configuration,
                new VirtualizedProcessConfig());

            var cpuTopologyService = new Mock<ICpuTopologyService>(MockBehavior.Loose);
            var powerPlanService = new Mock<IPowerPlanService>(MockBehavior.Loose);
            var notificationService = new Mock<INotificationService>(MockBehavior.Loose);
            var systemTrayService = new Mock<ISystemTrayService>(MockBehavior.Loose);
            var coreMaskService = new Mock<ICoreMaskService>(MockBehavior.Loose);
            var associationService = new Mock<IProcessPowerPlanAssociationService>(MockBehavior.Loose);

            return new ProcessViewModel(
                NullLogger<ProcessViewModel>.Instance,
                processService,
                new ProcessFilterService(),
                virtualizedProcessService.Object,
                cpuTopologyService.Object,
                powerPlanService.Object,
                notificationService.Object,
                systemTrayService.Object,
                coreMaskService.Object,
                associationService.Object,
                gameModeService,
                cpuTopologyProvider: cpuTopologyProvider);
        }

        private static CpuTopologyModel CreateTwoCoreTopology()
        {
            return new CpuTopologyModel
            {
                LogicalCores =
                [
                    new CpuCoreModel { LogicalCoreId = 0, PhysicalCoreId = 0, Label = "CPU 0" },
                    new CpuCoreModel { LogicalCoreId = 1, PhysicalCoreId = 1, Label = "CPU 1" },
                ],
            };
        }
    }
}
