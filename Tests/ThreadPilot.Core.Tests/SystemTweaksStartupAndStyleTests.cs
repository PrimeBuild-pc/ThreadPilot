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
        public void Shutdown_ResetsTrackedChangesBeforeTheMonitorForgetsThem()
        {
            // ProcessMonitorManagerService.StopAsync untracks every process it changed, so
            // "reset process changes on exit" found nothing to reset when it ran afterwards.
            var source = File.ReadAllText(GetRepositoryFilePath("MainWindow.Behaviors.partial.cs"));
            var start = source.IndexOf("private async Task PerformGracefulShutdownAsync(", StringComparison.Ordinal);
            var shutdown = source[start..source.IndexOf("// 3. Restore default power plan", start, StringComparison.Ordinal)];

            var reset = shutdown.IndexOf("await processService.ClearAllAppliedMasksAsync();", StringComparison.Ordinal);
            var stopMonitor = shutdown.IndexOf("await this.processMonitorManagerService.StopAsync();", StringComparison.Ordinal);
            Assert.True(reset >= 0 && stopMonitor >= 0);
            Assert.True(reset < stopMonitor);
        }

        [Fact]
        public void UpdateShutdown_GoesThroughTheGracefulShutdown()
        {
            // Application.Shutdown ignores the window's cancel, so the restore of Game Boost, process
            // changes and power plan in PerformGracefulShutdownAsync never ran before an update.
            var service = File.ReadAllText(GetRepositoryFilePath("Services", "WpfApplicationShutdownService.cs"));
            var window = File.ReadAllText(GetRepositoryFilePath("MainWindow.Behaviors.partial.cs"));

            Assert.Contains("ShutdownForUpdateAsync()", service, StringComparison.Ordinal);
            Assert.Contains("internal Task ShutdownForUpdateAsync() => this.PerformGracefulShutdownAsync(validateUnsavedChanges: false);", window, StringComparison.Ordinal);
        }

        [Fact]
        public void TweaksView_UsesWindows11ToggleWithStateLabel()
        {
            // Native Windows 11 switch (40x20 track, sliding thumb) with "On"/"Off" written beside it,
            // as in Windows Settings, so the state is readable without decoding colours.
            var document = XDocument.Load(GetRepositoryFilePath("Views", "SystemTweaksView.xaml"));
            var serialized = document.ToString(SaveOptions.DisableFormatting);

            Assert.Contains("Win11ToggleSwitchStyle", serialized, StringComparison.Ordinal);
            Assert.Contains("AutomationProperties.Name=\"{Binding Name}\"", serialized, StringComparison.Ordinal);
            Assert.Contains("SystemTweaks_ToggleOn", serialized, StringComparison.Ordinal);
            Assert.Contains("SystemTweaks_ToggleOff", serialized, StringComparison.Ordinal);

            var track = Assert.Single(
                document.Descendants(),
                element => element.Name.LocalName == "Border" &&
                    element.Attributes().Any(attribute =>
                        attribute.Name.LocalName == "Name" && attribute.Value == "Track"));
            Assert.Equal("40", track.Attribute("Width")?.Value);
            Assert.Equal("20", track.Attribute("Height")?.Value);

            // The label lives outside the track: a long translation ("Disattivato") widens the control
            // instead of sliding under the thumb.
            Assert.DoesNotContain(track.Descendants(), element => element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Name" && attribute.Value == "StateLabel"));
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
