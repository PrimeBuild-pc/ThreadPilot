namespace ThreadPilot.Core.Tests
{
    using System.Collections.ObjectModel;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using ThreadPilot.Models;
    using ThreadPilot.Services;
    using ThreadPilot.ViewModels;

    public sealed class PowerPlanViewModelTests
    {
        [Fact]
        public async Task DeletePowerPlanCommand_CallsServiceRefreshesAndLogs_WhenPlanIsNotActive()
        {
            var harness = new Harness();
            var deletePlan = new PowerPlanModel { Guid = Harness.DeleteGuid, Name = "Gaming" };
            var viewModel = harness.CreateViewModel();

            await viewModel.DeletePowerPlanCommand.ExecuteAsync(deletePlan);

            harness.PowerPlan.Verify(service => service.DeletePowerPlanAsync(Harness.DeleteGuid), Times.Once);
            harness.PowerPlan.Verify(service => service.GetPowerPlansAsync(), Times.Once);
            harness.Logging.Verify(
                logger => logger.LogUserActionAsync(
                    "PowerPlanDeleted",
                    "Deleted power plan Gaming",
                    $"Guid: {Harness.DeleteGuid}"),
                Times.Once);
            Assert.Equal("Power plan deleted: Gaming.", viewModel.StatusMessage);
            Assert.False(viewModel.HasError);
        }

        [Fact]
        public async Task DeletePowerPlanCommand_BlocksActivePlanBeforeCallingService()
        {
            var harness = new Harness();
            var activePlan = new PowerPlanModel { Guid = Harness.ActiveGuid, Name = "Balanced", IsActive = true };
            var viewModel = harness.CreateViewModel();

            await viewModel.DeletePowerPlanCommand.ExecuteAsync(activePlan);

            harness.PowerPlan.Verify(service => service.DeletePowerPlanAsync(It.IsAny<string>()), Times.Never);
            Assert.Equal("Switch to another power plan before deleting the active plan.", viewModel.StatusMessage);
            Assert.True(viewModel.HasError);
        }

        [Fact]
        public async Task DeletePowerPlanCommand_ShowsFailureAndDoesNotCrash_WhenServiceFails()
        {
            var harness = new Harness(deleteSucceeds: false);
            var deletePlan = new PowerPlanModel { Guid = Harness.DeleteGuid, Name = "Gaming" };
            var viewModel = harness.CreateViewModel();

            await viewModel.DeletePowerPlanCommand.ExecuteAsync(deletePlan);

            Assert.Equal("Could not delete power plan Gaming. Windows may not allow this plan to be removed.", viewModel.StatusMessage);
            Assert.True(viewModel.HasError);
        }

        [Fact]
        public async Task SetActivePlanCommand_ShowsSuccessStatusAndLogs()
        {
            var harness = new Harness();
            var viewModel = harness.CreateViewModel();
            viewModel.SelectedPowerPlan = new PowerPlanModel { Guid = Harness.DeleteGuid, Name = "Gaming" };

            await viewModel.SetActivePlanCommand.ExecuteAsync(null);

            Assert.Equal("Power plan applied: Gaming.", viewModel.StatusMessage);
            harness.Logging.Verify(
                logger => logger.LogUserActionAsync(
                    "PowerPlanApplied",
                    "Applied power plan Gaming",
                    $"Guid: {Harness.DeleteGuid}"),
                Times.Once);
        }

        [Fact]
        public async Task RefreshPowerPlansCommand_ShowsCompletionStatusAndLogs()
        {
            var harness = new Harness();
            var viewModel = harness.CreateViewModel();

            await viewModel.RefreshPowerPlansCommand.ExecuteAsync(null);

            Assert.Equal("Power plans refreshed.", viewModel.StatusMessage);
            harness.Logging.Verify(
                logger => logger.LogUserActionAsync(
                    "PowerPlansRefreshed",
                    "Refreshed power plan list",
                    null),
                Times.Once);
        }

        private sealed class Harness
        {
            public const string ActiveGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";
            public const string DeleteGuid = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";

            public Mock<IPowerPlanService> PowerPlan { get; } = new(MockBehavior.Strict);

            public Mock<IEnhancedLoggingService> Logging { get; } = new(MockBehavior.Loose);

            public Harness(bool deleteSucceeds = true)
            {
                var active = new PowerPlanModel { Guid = ActiveGuid, Name = "Balanced", IsActive = true };
                var delete = new PowerPlanModel { Guid = DeleteGuid, Name = "Gaming" };

                this.PowerPlan
                    .Setup(service => service.GetPowerPlansAsync())
                    .ReturnsAsync(new ObservableCollection<PowerPlanModel> { active, delete });
                this.PowerPlan
                    .Setup(service => service.GetCustomPowerPlansAsync())
                    .ReturnsAsync(new ObservableCollection<PowerPlanModel>());
                this.PowerPlan
                    .Setup(service => service.GetActivePowerPlan())
                    .ReturnsAsync(active);
                this.PowerPlan
                    .Setup(service => service.SetActivePowerPlan(It.IsAny<PowerPlanModel>()))
                    .ReturnsAsync(true);
                this.PowerPlan
                    .Setup(service => service.DeletePowerPlanAsync(DeleteGuid))
                    .ReturnsAsync(deleteSucceeds);
            }

            public PowerPlanViewModel CreateViewModel() =>
                new(
                    NullLogger<PowerPlanViewModel>.Instance,
                    this.PowerPlan.Object,
                    this.Logging.Object);
        }
    }
}
