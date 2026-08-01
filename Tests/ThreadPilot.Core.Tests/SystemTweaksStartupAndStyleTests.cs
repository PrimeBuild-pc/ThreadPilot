namespace ThreadPilot.Core.Tests
{
    using System.Xml.Linq;

    public sealed class SystemTweaksStartupAndStyleTests
    {
        [Fact]
        public void Startup_LoadsPersistedTweakStateBeforePageNavigation()
        {
            var source = File.ReadAllText(GetRepositoryFilePath("MainWindow.Behaviors.partial.cs"));
            var start = source.IndexOf("private async Task LoadViewModelsAsync()", StringComparison.Ordinal);
            var end = source.IndexOf("private async Task InitializeServicesAsync()", start, StringComparison.Ordinal);
            var startupSection = source[start..end];

            Assert.Contains("await this.systemTweaksViewModel.LoadAsync();", startupSection, StringComparison.Ordinal);
            Assert.Contains("this.initializedSections.Add(\"Tweaks\");", startupSection, StringComparison.Ordinal);
        }

        [Fact]
        public void TweaksView_UsesNativeFluentToggleSwitch()
        {
            var document = XDocument.Load(GetRepositoryFilePath("Views", "SystemTweaksView.xaml"));
            var serialized = document.ToString(SaveOptions.DisableFormatting);

            Assert.Contains("ToggleSwitch", serialized, StringComparison.Ordinal);
            Assert.Contains("AutomationProperties.Name=\"{Binding Name}\"", serialized, StringComparison.Ordinal);
            Assert.DoesNotContain("PillToggleButtonStyle", serialized, StringComparison.Ordinal);
        }

        [Fact]
        public void TweaksService_UsesNativePowerApiAndNoDebugClockOverride()
        {
            var source = File.ReadAllText(GetRepositoryFilePath("Services", "SystemTweaksService.cs"));

            Assert.Contains("PowerReadACValueIndex", source, StringComparison.Ordinal);
            Assert.Contains("PowerWriteACValueIndex", source, StringComparison.Ordinal);
            Assert.DoesNotContain("useplatformclock", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("bcdedit", source, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void MainNavigation_UsesNeutralSelectionWithoutAccentBorder()
        {
            var document = XDocument.Load(GetRepositoryFilePath("MainWindow.xaml"));
            var activeTrigger = document
                .Descendants()
                .Single(element =>
                    element.Name.LocalName == "Trigger"
                    && element.Attribute("Property")?.Value == "IsActive");
            var setters = activeTrigger
                .Elements()
                .Where(element => element.Name.LocalName == "Setter")
                .ToDictionary(
                    element => element.Attribute("Property")!.Value,
                    element => element.Attribute("Value")!.Value,
                    StringComparer.Ordinal);

            Assert.Equal("{DynamicResource SoftSelectionBackgroundBrush}", setters["Background"]);
            Assert.Equal("0", setters["BorderThickness"]);
            Assert.DoesNotContain(setters.Values, value => value.Contains("Accent", StringComparison.Ordinal));
        }

        private static string GetRepositoryFilePath(params string[] pathParts)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "ThreadPilot.csproj")))
            {
                directory = directory.Parent;
            }

            if (directory == null)
            {
                throw new InvalidOperationException("Repository root was not found.");
            }

            return Path.Combine(new[] { directory.FullName }.Concat(pathParts).ToArray());
        }
    }
}
