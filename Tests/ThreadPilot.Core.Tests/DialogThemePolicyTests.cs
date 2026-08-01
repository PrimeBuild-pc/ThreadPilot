namespace ThreadPilot.Core.Tests
{
    using System.Xml.Linq;

    public sealed class DialogThemePolicyTests
    {
        [Fact]
        public void UnsavedSettingsDialogs_UseLocalizedMessageAndNeutralButtons()
        {
            var mainWindow = XDocument.Load(GetRepositoryFilePath("MainWindow.xaml"));
            var mainOverlay = FindNamedElement(mainWindow, "UnsavedSettingsOverlay");
            var message = FindNamedElement(mainWindow, "UnsavedSettingsDialogMessage");
            var settingsWindow = XDocument.Load(GetRepositoryFilePath("Views", "SettingsWindow.xaml"));
            var settingsOverlay = FindNamedElement(settingsWindow, "UnsavedSettingsOverlay");

            Assert.Equal(
                "{DynamicResource SettingsWindow_UnsavedDescription}",
                message.Attribute("Text")?.Value);
            AssertNeutralButtons(mainOverlay);
            AssertNeutralButtons(settingsOverlay);
        }

        [Fact]
        public void MainWindowMessageOverlays_DoNotUseBlueAccentButtons()
        {
            var document = XDocument.Load(GetRepositoryFilePath("MainWindow.xaml"));

            AssertNeutralButtons(FindNamedElement(document, "ElevationWarningOverlay"));
            AssertNeutralButtons(FindNamedElement(document, "PopupOverlay"));
        }

        [Theory]
        [InlineData("FluentDark.xaml", "#FF303030", "#FF4A4A4A")]
        [InlineData("FluentLight.xaml", "#FFF3F3F3", "#FFDADADA")]
        public void Theme_StateMessagesUseNeutralSurfaces(string themeFile, string background, string border)
        {
            var theme = File.ReadAllText(GetRepositoryFilePath("Themes", themeFile));

            Assert.Contains($"x:Key=\"InfoBackgroundBrush\" Color=\"{background}\"", theme, StringComparison.Ordinal);
            Assert.Contains($"x:Key=\"SuccessBackgroundBrush\" Color=\"{background}\"", theme, StringComparison.Ordinal);
            Assert.Contains($"x:Key=\"WarningBackgroundBrush\" Color=\"{background}\"", theme, StringComparison.Ordinal);
            Assert.Contains($"x:Key=\"ErrorBackgroundBrush\" Color=\"{background}\"", theme, StringComparison.Ordinal);
            Assert.Contains($"x:Key=\"InfoBorderBrush\" Color=\"{border}\"", theme, StringComparison.Ordinal);
            Assert.Contains($"x:Key=\"ErrorBorderBrush\" Color=\"{border}\"", theme, StringComparison.Ordinal);
        }

        private static void AssertNeutralButtons(XElement container)
        {
            var buttonBackgrounds = container
                .Descendants()
                .Where(element => element.Name.LocalName == "Button")
                .Select(element => element.Attribute("Background")?.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value));

            Assert.DoesNotContain(buttonBackgrounds, value => value!.Contains("Accent", StringComparison.Ordinal));
        }

        private static XElement FindNamedElement(XDocument document, string name)
        {
            XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
            return document.Descendants().Single(element => (string?)element.Attribute(x + "Name") == name);
        }

        private static string GetRepositoryFilePath(params string[] segments)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "ThreadPilot_1.sln")))
            {
                directory = directory.Parent;
            }

            var root = directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
            return Path.Combine(new[] { root }.Concat(segments).ToArray());
        }
    }
}
