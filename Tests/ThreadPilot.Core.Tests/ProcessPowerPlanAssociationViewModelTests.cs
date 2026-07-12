namespace ThreadPilot.Core.Tests
{
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using ThreadPilot.Services;
    using ThreadPilot.ViewModels;

    public sealed class ProcessPowerPlanAssociationViewModelTests
    {
        [Fact]
        public void Constructor_IgnoresConfigurationReloadUntilViewIsInitialized()
        {
            var associations = new Mock<IProcessPowerPlanAssociationService>(MockBehavior.Loose);
            var powerPlans = new Mock<IPowerPlanService>(MockBehavior.Strict);
            var processes = new Mock<IProcessService>(MockBehavior.Strict);
            var monitor = new Mock<IProcessMonitorManagerService>(MockBehavior.Loose);
            var masks = new Mock<ICoreMaskService>(MockBehavior.Strict);
            _ = new ProcessPowerPlanAssociationViewModel(
                NullLogger<ProcessPowerPlanAssociationViewModel>.Instance,
                associations.Object,
                powerPlans.Object,
                processes.Object,
                monitor.Object,
                masks.Object);

            associations.Raise(
                service => service.ConfigurationChanged += null,
                new ConfigurationChangedEventArgs("Loaded"));

            powerPlans.Verify(service => service.GetPowerPlansAsync(), Times.Never);
            processes.Verify(service => service.GetProcessesAsync(), Times.Never);
            masks.Verify(service => service.InitializeAsync(), Times.Never);
        }
    }
}
