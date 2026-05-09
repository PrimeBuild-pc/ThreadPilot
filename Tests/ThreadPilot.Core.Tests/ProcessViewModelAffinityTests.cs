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

        private static ProcessViewModel CreateViewModel(IProcessService processService, IGameModeService gameModeService)
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
                gameModeService);
        }
    }
}
