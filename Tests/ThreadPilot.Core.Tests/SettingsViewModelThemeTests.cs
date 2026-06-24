namespace ThreadPilot.Core.Tests
{
    using System.Collections.ObjectModel;
    using CommunityToolkit.Mvvm.Input;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using ThreadPilot.Models;
    using ThreadPilot.Services;
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

        [Fact]
        public async Task ChangingApplyPersistentRulesOnProcessStart_UpdatesSettingAndLogsVisibleActivityEntry()
        {
            var harness = new Harness();
            var viewModel = harness.CreateViewModel();

            viewModel.Settings.ApplyPersistentRulesOnProcessStart = false;

            Assert.False(viewModel.Settings.ApplyPersistentRulesOnProcessStart);
            harness.Logging.Verify(
                service => service.LogUserActionAsync(
                    "SettingsChanged",
                    "[Settings] Apply saved rules at process start disabled.",
                    null),
                Times.Once);
            var disabledEntry = Assert.Single(await harness.Audit.GetEntriesAsync());
            Assert.Equal("Settings", disabledEntry.Category);
            Assert.Equal("[Settings] Apply saved rules at process start disabled.", disabledEntry.Message);

            viewModel.Settings.ApplyPersistentRulesOnProcessStart = true;

            Assert.True(viewModel.Settings.ApplyPersistentRulesOnProcessStart);
            harness.Logging.Verify(
                service => service.LogUserActionAsync(
                    "SettingsChanged",
                    "[Settings] Apply saved rules at process start enabled.",
                    null),
                Times.Once);
            var entries = await harness.Audit.GetEntriesAsync();
            Assert.Contains(entries, entry => entry.Message == "[Settings] Apply saved rules at process start enabled.");
        }

        [Fact]
        public async Task ChangingLanguage_AppliesLanguageAndLogsVisibleActivityEntry()
        {
            var harness = new Harness();
            var viewModel = harness.CreateViewModel();

            viewModel.Settings.Language = "zh-CN";

            harness.Localization.Verify(service => service.ApplyLanguage("zh-CN"), Times.Once);
            harness.Logging.Verify(
                service => service.LogUserActionAsync(
                    "LanguageChanged",
                    "Language changed to Simplified Chinese",
                    null),
                Times.Once);
            var entry = Assert.Single(await harness.Audit.GetEntriesAsync());
            Assert.Equal("Language changed to Simplified Chinese", entry.Message);
            Assert.Equal("Language changed to Simplified Chinese.", viewModel.StatusMessage);
        }

        [Fact]
        public void ChangingLanguage_UsesLocalizedStatusMessage()
        {
            var harness = new Harness();
            harness.Localization
                .Setup(service => service.GetString(It.IsAny<string>()))
                .Returns<string>(key => key switch
                {
                    "Settings_LanguageSimplifiedChinese" => "ç®€ä½“ä¸­æ–‡",
                    "Settings_StatusLanguageChangedFormat" => "è¯­è¨€å·²åˆ‡æ¢ä¸º{0}ã€‚",
                    _ => key,
                });
            var viewModel = harness.CreateViewModel();

            viewModel.Settings.Language = "zh-CN";

            Assert.Equal("è¯­è¨€å·²åˆ‡æ¢ä¸ºç®€ä½“ä¸­æ–‡ã€‚", viewModel.StatusMessage);
        }

        [Fact]
        public async Task SaveSettingsCommand_PersistsSelectedLanguage()
        {
            var harness = new Harness();
            ApplicationSettingsModel? savedSettings = null;
            harness.SettingsService
                .Setup(service => service.UpdateSettingsAsync(It.IsAny<ApplicationSettingsModel>()))
                .Callback<ApplicationSettingsModel>(settings => savedSettings = (ApplicationSettingsModel)settings.Clone())
                .Returns(Task.CompletedTask);
            var viewModel = harness.CreateViewModel();
            viewModel.Settings.Language = "zh-CN";

            await ((IAsyncRelayCommand)viewModel.SaveSettingsCommand).ExecuteAsync(null);

            Assert.NotNull(savedSettings);
            Assert.Equal("zh-CN", savedSettings.Language);
            Assert.False(viewModel.HasUnsavedChanges);
        }

        [Fact]
        public void SettingsView_ExposesPersistentRuleAutoApplyToggle()
        {
            var settingsViewPath = Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "Views",
                "SettingsView.xaml");
            var serialized = File.ReadAllText(settingsViewPath);

            Assert.Contains("Text=\"{DynamicResource SettingsView_RulesAutomation}\" Style=\"{StaticResource SectionHeaderStyle}\"", serialized, StringComparison.Ordinal);
            Assert.Contains("Text=\"{DynamicResource SettingsView_ApplyOnStart}\"", serialized, StringComparison.Ordinal);
            Assert.Contains("TextWrapping=\"Wrap\"", serialized, StringComparison.Ordinal);
            Assert.Contains("IsChecked=\"{Binding Settings.ApplyPersistentRulesOnProcessStart}\"", serialized, StringComparison.Ordinal);
            Assert.Contains("Text=\"{DynamicResource SettingsView_ApplyOnStartDescription}\"", serialized, StringComparison.Ordinal);
        }

        [Fact]
        public void SettingsView_ExposesOptionalChineseLanguageSelection()
        {
            var settingsViewPath = Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "Views",
                "SettingsView.xaml");
            var serialized = File.ReadAllText(settingsViewPath);

            Assert.Contains("SelectedValue=\"{Binding Settings.Language}\"", serialized, StringComparison.Ordinal);
            Assert.Contains("Tag=\"en-US\"", serialized, StringComparison.Ordinal);
            Assert.Contains("Tag=\"zh-CN\"", serialized, StringComparison.Ordinal);
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

            public Mock<ILocalizationService> Localization { get; } = new(MockBehavior.Loose);

            public Mock<IUpdateService> Updates { get; } = new(MockBehavior.Loose);

            public Mock<IApplicationVersionProvider> VersionProvider { get; } = new(MockBehavior.Loose);

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
                this.Autostart
                    .Setup(service => service.CheckAutostartStatusAsync())
                    .ReturnsAsync(true);
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
                this.VersionProvider.SetupGet(service => service.DisplayVersion).Returns("v1.3.1");
                this.VersionProvider.SetupGet(service => service.CurrentVersion).Returns(new SemanticVersion(1, 3, 1));
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
                    this.Updates.Object,
                    this.VersionProvider.Object,
                    this.Localization.Object,
                    this.Logging.Object,
                    this.Audit);
        }
    }
}
