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
        public void TweaksView_UsesLabelledPillToggle()
        {
            // The native ui:ToggleSwitch is 40x20 with no text, too quiet to read as a control on
            // this list. The pill states ON/OFF inside the track and is sized to be obvious.
            var document = XDocument.Load(GetRepositoryFilePath("Views", "SystemTweaksView.xaml"));
            var serialized = document.ToString(SaveOptions.DisableFormatting);

            Assert.Contains("PillToggleButtonStyle", serialized, StringComparison.Ordinal);
            Assert.Contains("AutomationProperties.Name=\"{Binding Name}\"", serialized, StringComparison.Ordinal);
            Assert.Contains("SystemTweaks_ToggleOn", serialized, StringComparison.Ordinal);
            Assert.Contains("SystemTweaks_ToggleOff", serialized, StringComparison.Ordinal);

            // The label and the thumb must be docked rather than stacked: a long localized label
            // (ru "ВЫКЛ") has to widen the pill, not slide underneath the thumb.
            var track = Assert.Single(
                document.Descendants(),
                element => element.Name.LocalName == "Border" &&
                    element.Attributes().Any(attribute =>
                        attribute.Name.LocalName == "Name" && attribute.Value == "Track"));
            Assert.Single(track.Descendants(), element => element.Name.LocalName == "DockPanel");

            var pill = Assert.Single(
                document.Descendants(),
                element => element.Name.LocalName == "Style" &&
                    element.Attributes().Any(attribute =>
                        attribute.Name.LocalName == "Key" && attribute.Value == "PillToggleButtonStyle"));
            var setters = pill
                .Elements()
                .Where(element => element.Name.LocalName == "Setter")
                .ToDictionary(
                    element => element.Attribute("Property")!.Value,
                    element => element.Attribute("Value")?.Value,
                    StringComparer.Ordinal);

            // MinWidth, never Width: a fixed track clips the wider locales.
            Assert.Equal("56", setters["MinWidth"]);
            Assert.Equal("28", setters["Height"]);
            Assert.False(setters.ContainsKey("Width"));
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
