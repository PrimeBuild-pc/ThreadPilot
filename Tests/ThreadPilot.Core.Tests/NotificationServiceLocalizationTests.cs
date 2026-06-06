namespace ThreadPilot.Core.Tests
{
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using ThreadPilot.Models;
    using ThreadPilot.Services;

    public sealed class NotificationServiceLocalizationTests
    {
        [Fact]
        public async Task ShowPowerPlanChangeNotificationAsync_UsesLocalizedTitleAndFormat()
        {
            var harness = new Harness(new Dictionary<string, string>
            {
                ["Notification_PowerPlanChangedTitle"] = "Localized power title",
                ["Notification_PowerPlanChangedFormat"] = "Changed {0} -> {1}",
            });
            var service = harness.CreateService();

            await service.ShowPowerPlanChangeNotificationAsync("Balanced", "Performance");

            var notification = Assert.Single(service.NotificationHistory);
            Assert.Equal("Localized power title", notification.Title);
            Assert.Equal("Changed Balanced -> Performance", notification.Message);
            harness.Tray.Verify(
                tray => tray.ShowTrayNotification(
                    "Localized power title",
                    "Changed Balanced -> Performance",
                    NotificationType.PowerPlanChange,
                    It.IsAny<int>()),
                Times.Once);
        }

        [Fact]
        public async Task ShowPowerPlanChangeNotificationAsync_UsesLocalizedProcessFormat()
        {
            var harness = new Harness(new Dictionary<string, string>
            {
                ["Notification_PowerPlanChangedTitle"] = "Power",
                ["Notification_PowerPlanChangedProcessFormat"] = "{1}: {0}",
            });
            var service = harness.CreateService();

            await service.ShowPowerPlanChangeNotificationAsync("Balanced", "Performance", "game.exe");

            var notification = Assert.Single(service.NotificationHistory);
            Assert.Equal("Power", notification.Title);
            Assert.Equal("game.exe: Performance", notification.Message);
        }

        [Theory]
        [InlineData(true, "Enabled localized", NotificationType.Success)]
        [InlineData(false, "Disabled localized", NotificationType.Warning)]
        public async Task ShowProcessMonitoringNotificationAsync_UsesLocalizedTitle(bool isEnabled, string expectedTitle, NotificationType expectedType)
        {
            var harness = new Harness(new Dictionary<string, string>
            {
                ["Notification_ProcessMonitoringEnabled"] = "Enabled localized",
                ["Notification_ProcessMonitoringDisabled"] = "Disabled localized",
            });
            var service = harness.CreateService();

            await service.ShowProcessMonitoringNotificationAsync("Monitoring changed", isEnabled);

            var notification = Assert.Single(service.NotificationHistory);
            Assert.Equal(expectedTitle, notification.Title);
            Assert.Equal("Monitoring changed", notification.Message);
            Assert.Equal(expectedType, notification.Type);
        }

        [Fact]
        public async Task ShowCpuAffinityNotificationAsync_UsesLocalizedTitleAndFormat()
        {
            var harness = new Harness(new Dictionary<string, string>
            {
                ["Notification_CpuAffinityAppliedTitle"] = "Affinity localized",
                ["Notification_CpuAffinityAppliedFormat"] = "{0} uses {1}",
            });
            var service = harness.CreateService();

            await service.ShowCpuAffinityNotificationAsync("game.exe", "CPU 0, 1");

            var notification = Assert.Single(service.NotificationHistory);
            Assert.Equal("Affinity localized", notification.Title);
            Assert.Equal("game.exe uses CPU 0, 1", notification.Message);
        }

        [Fact]
        public async Task ShowNotificationAsync_LocalizesKnownAndDynamicGameBoostStrings()
        {
            var harness = new Harness(new Dictionary<string, string>
            {
                ["Notification_GameBoostActivatedTitle"] = "Boost title",
                ["Notification_GameBoostActivatedFormat"] = "Boosted {0}",
            });
            var service = harness.CreateService();

            await service.ShowNotificationAsync(
                "Game Boost Activated",
                "Game Boost mode activated for game.exe",
                NotificationType.Information);

            var notification = Assert.Single(service.NotificationHistory);
            Assert.Equal("Boost title", notification.Title);
            Assert.Equal("Boosted game.exe", notification.Message);
        }

        [Fact]
        public async Task ShowNotificationAsync_KeepsOriginalText_WhenLocalizationKeyIsMissing()
        {
            var harness = new Harness(new Dictionary<string, string>());
            var service = harness.CreateService();

            await service.ShowNotificationAsync(
                "Affinity blocked",
                "Unmapped notification message",
                NotificationType.Warning);

            var notification = Assert.Single(service.NotificationHistory);
            Assert.Equal("Affinity blocked", notification.Title);
            Assert.Equal("Unmapped notification message", notification.Message);
        }

        private sealed class Harness
        {
            private readonly IReadOnlyDictionary<string, string> localizedStrings;

            public Mock<IApplicationSettingsService> Settings { get; } = new(MockBehavior.Loose);

            public Mock<ISystemTrayService> Tray { get; } = new(MockBehavior.Loose);

            public Mock<ILocalizationService> Localization { get; } = new(MockBehavior.Loose);

            public Harness(IReadOnlyDictionary<string, string> localizedStrings)
            {
                this.localizedStrings = localizedStrings;
                this.Settings.SetupGet(service => service.Settings).Returns(new ApplicationSettingsModel
                {
                    EnableToastNotifications = false,
                });
                this.Localization
                    .Setup(service => service.GetString(It.IsAny<string>()))
                    .Returns<string>(this.GetLocalizedString);
            }

            public NotificationService CreateService()
            {
                return new NotificationService(
                    NullLogger<NotificationService>.Instance,
                    this.Settings.Object,
                    this.Tray.Object,
                    this.Localization.Object);
            }

            private string GetLocalizedString(string key)
            {
                return this.localizedStrings.TryGetValue(key, out var value) ? value : key;
            }
        }
    }
}
