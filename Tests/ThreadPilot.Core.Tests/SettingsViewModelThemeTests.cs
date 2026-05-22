namespace ThreadPilot.Core.Tests
{
    using System.Collections.ObjectModel;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using ThreadPilot.Models;
    using ThreadPilot.Services;
    using ThreadPilot.Services.Abstractions;
    using ThreadPilot.ViewModels;

    public sealed class SettingsViewModelThemeTests
    {
        [Fact]
        public async Task ChangingTheme_AppliesThemeAndLogsVisibleActivityEntry()
        {
            var harness = new Harness();
            var viewModel = harness.CreateViewModel();

            viewModel.Settings.UseDarkTheme = true;

            harness.Theme.Verify(service => service.ApplyTheme(true), Times.Once);
            harness.Tray.Verify(service => service.ApplyTheme(true), Times.Once);
            harness.Logging.Verify(
                service => service.LogUserActionAsync(
                    "ThemeChanged",
                    "Theme changed to Dark",
                    null),
                Times.Once);
            var entry = Assert.Single(await harness.Audit.GetEntriesAsync());
            Assert.Equal("Settings", entry.Category);
            Assert.Equal(ActivityAuditSeverity.Success, entry.Severity);
            Assert.Equal("Theme changed to Dark", entry.Message);
            Assert.Equal("Theme changed to Dark.", viewModel.StatusMessage);
        }

        [Fact]
        public void ChangingTheme_ToSameValue_DoesNotApplyOrLogAgain()
        {
            var harness = new Harness(initialDarkTheme: true);
            var viewModel = harness.CreateViewModel();

            viewModel.Settings.UseDarkTheme = true;

            harness.Theme.Verify(service => service.ApplyTheme(It.IsAny<bool>()), Times.Never);
            harness.Tray.Verify(service => service.ApplyTheme(It.IsAny<bool>()), Times.Never);
            harness.Logging.Verify(
                service => service.LogUserActionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()),
                Times.Never);
        }

        private sealed class Harness
        {
            private readonly ApplicationSettingsModel settings;

            public Mock<IApplicationSettingsService> SettingsService { get; } = new(MockBehavior.Loose);

            public Mock<INotificationService> Notifications { get; } = new(MockBehavior.Loose);

            public Mock<IAutostartService> Autostart { get; } = new(MockBehavior.Loose);

            public Mock<IPowerPlanService> PowerPlans { get; } = new(MockBehavior.Loose);

            public Mock<IProcessPowerPlanAssociationService> Associations { get; } = new(MockBehavior.Loose);

            public Mock<IProcessMonitorManagerService> ProcessMonitorManager { get; } = new(MockBehavior.Loose);

            public Mock<IThemeService> Theme { get; } = new(MockBehavior.Loose);

            public Mock<ISystemTrayService> Tray { get; } = new(MockBehavior.Loose);

            public Mock<IEnhancedLoggingService> Logging { get; } = new(MockBehavior.Loose);

            public ActivityAuditService Audit { get; } = new(NullLogger<ActivityAuditService>.Instance);

            public Harness(bool initialDarkTheme = false)
            {
                this.settings = new ApplicationSettingsModel
                {
                    UseDarkTheme = initialDarkTheme,
                    HasUserThemePreference = initialDarkTheme,
                };
                this.SettingsService.SetupGet(service => service.Settings).Returns(this.settings);
                this.PowerPlans
                    .Setup(service => service.GetPowerPlansAsync())
                    .ReturnsAsync(new ObservableCollection<PowerPlanModel>());
                this.PowerPlans
                    .Setup(service => service.GetCustomPowerPlansAsync())
                    .ReturnsAsync(new ObservableCollection<PowerPlanModel>());
                this.PowerPlans
                    .Setup(service => service.GetActivePowerPlan())
                    .ReturnsAsync((PowerPlanModel?)null);
                this.Associations
                    .Setup(service => service.GetDefaultPowerPlanAsync())
                    .ReturnsAsync((string.Empty, string.Empty));
            }

            public SettingsViewModel CreateViewModel() =>
                new(
                    NullLogger<SettingsViewModel>.Instance,
                    this.SettingsService.Object,
                    this.Notifications.Object,
                    this.Autostart.Object,
                    this.PowerPlans.Object,
                    this.Associations.Object,
                    this.ProcessMonitorManager.Object,
                    this.Theme.Object,
                    this.Tray.Object,
                    new GitHubUpdateChecker(new Mock<IGitHubReleaseClient>().Object),
                    this.Logging.Object,
                    this.Audit);
        }
    }
}
