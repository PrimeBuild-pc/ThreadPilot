namespace ThreadPilot.Core.Tests
{
    using System.Xml.Linq;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using ThreadPilot.Models;
    using ThreadPilot.Services;
    using ThreadPilot.ViewModels;

    public sealed class SystemTweaksGameBoostTests
    {
        [Fact]
        public async Task Load_ShowsSettingsAndDecision_ToggleSavesAtOnce()
        {
            var settings = new ApplicationSettingsModel { GameBoostMode = GameBoostMode.Isolation, GameBoostManualGames = ["Wow"] };
            ApplicationSettingsModel? saved = null;
            var settingsService = new Mock<IApplicationSettingsService>();
            settingsService.SetupGet(s => s.Settings).Returns(settings);
            settingsService.Setup(s => s.UpdateSettingsAsync(It.IsAny<ApplicationSettingsModel>()))
                .Callback((ApplicationSettingsModel model) => saved = model)
                .Returns(Task.CompletedTask);
            var session = new Mock<IGameSessionService>();
            session.Setup(s => s.GetDecisionAsync()).ReturnsAsync(GameBoostDecision.None);
            var viewModel = new SystemTweaksViewModel(
                new Mock<ISystemTweaksService>().Object,
                new Mock<INotificationService>().Object,
                new Mock<ILocalizationService>().Object,
                NullLogger<SystemTweaksViewModel>.Instance,
                settingsService: settingsService.Object,
                gameSessionService: session.Object);

            await viewModel.LoadGameBoostAsync();

            Assert.True(viewModel.IsGameBoostAvailable);
            Assert.True(viewModel.GameBoostIsolation);
            Assert.False(viewModel.GameBoostAutomatic);
            Assert.Equal("Wow", viewModel.GameBoostManualGamesText);
            Assert.Contains("Windows decides", viewModel.GameBoostDecisionText, StringComparison.Ordinal);
            Assert.Null(saved); // loading never saves

            viewModel.GameBoostEnabled = true;
            await Task.Delay(50);

            Assert.NotNull(saved);
            Assert.True(saved!.GameBoostEnabled);
            Assert.Equal(GameBoostMode.Isolation, saved.GameBoostMode);
            Assert.Equal(["Wow"], saved.GameBoostManualGames);
        }

        [Fact]
        public void TweaksView_HasGameBoostCard()
        {
            var serialized = XDocument.Load(RepositoryFile("Views", "SystemTweaksView.xaml")).ToString(SaveOptions.DisableFormatting);

            Assert.Contains("{Binding GameBoostEnabled, Mode=TwoWay}", serialized, StringComparison.Ordinal);
            Assert.Contains("{Binding GameBoostIsolation, Mode=TwoWay}", serialized, StringComparison.Ordinal);
            Assert.Contains("{Binding SaveGameBoostListsCommand}", serialized, StringComparison.Ordinal);
            Assert.Contains("{Binding GameBoostDecisionText}", serialized, StringComparison.Ordinal);
        }

        [Fact]
        public void EveryLocale_HasTheGameBoostKeys()
        {
            var english = GameBoostKeys("en-US");
            Assert.Equal(19, english.Count);
            foreach (var language in new[] { "it-IT", "de-DE", "es-ES", "fr-FR", "ru-RU", "zh-CN" })
            {
                Assert.Equal(english, GameBoostKeys(language));
            }
        }

        private static List<string> GameBoostKeys(string language)
        {
            XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
            return XDocument.Load(RepositoryFile("Locales", $"{language}.xaml"))
                .Descendants()
                .Select(element => (string?)element.Attribute(x + "Key"))
                .Where(key => key != null && key.StartsWith("GameBoost_", StringComparison.Ordinal))
                .Select(key => key!)
                .ToList();
        }

        private static string RepositoryFile(params string[] parts)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "ThreadPilot.csproj")))
            {
                directory = directory.Parent;
            }

            return Path.Combine([directory!.FullName, .. parts]);
        }
    }
}
