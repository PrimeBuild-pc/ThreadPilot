namespace ThreadPilot.Core.Tests
{
    public sealed class AppSmokeTestStartupTests
    {
        [Fact]
        public void OnStartup_HandlesSmokeTestBeforeElevationSingleInstanceAndWindowStartup()
        {
            var source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "App.xaml.cs"));

            var smokeTestBranchIndex = source.IndexOf("if (startupMode.IsSmokeTest)", StringComparison.Ordinal);
            var elevationIndex = source.IndexOf("GetRequiredService<IElevationService>", StringComparison.Ordinal);
            var mutexIndex = source.IndexOf("Global\\\\ThreadPilot_SingleInstance", StringComparison.Ordinal);
            var baseStartupIndex = source.IndexOf("base.OnStartup(e);", StringComparison.Ordinal);
            var mainWindowIndex = source.IndexOf("GetRequiredService<MainWindow>", StringComparison.Ordinal);

            Assert.NotEqual(-1, smokeTestBranchIndex);
            Assert.True(smokeTestBranchIndex < elevationIndex);
            Assert.True(smokeTestBranchIndex < mutexIndex);
            Assert.True(smokeTestBranchIndex < baseStartupIndex);
            Assert.True(smokeTestBranchIndex < mainWindowIndex);
        }

        [Fact]
        public void SmokeTestMode_ExitsTheProcessAfterShutdownToAvoidDispatcherOrTimerHangs()
        {
            var source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "App.xaml.cs"));

            var smokeTestBranch = ExtractSection(
                source,
                "if (startupMode.IsSmokeTest)",
                "            // Set up global exception handlers first");

            Assert.Contains("this.Shutdown(smokeTestResult);", smokeTestBranch, StringComparison.Ordinal);
            Assert.Contains("Environment.Exit(smokeTestResult);", smokeTestBranch, StringComparison.Ordinal);
        }

        [Fact]
        public void RunSmokeTest_DoesNotResolveUiViewModelsOrMainWindow()
        {
            var source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "App.xaml.cs"));
            var smokeTestMethod = ExtractSection(
                source,
                "private int RunSmokeTest",
                "protected override void OnExit");

            Assert.DoesNotContain("ProcessViewModel", smokeTestMethod, StringComparison.Ordinal);
            Assert.DoesNotContain("PowerPlanViewModel", smokeTestMethod, StringComparison.Ordinal);
            Assert.DoesNotContain("MainWindow", smokeTestMethod, StringComparison.Ordinal);
        }

        private static string ExtractSection(string source, string startMarker, string endMarker)
        {
            var startIndex = source.IndexOf(startMarker, StringComparison.Ordinal);
            Assert.NotEqual(-1, startIndex);

            var endIndex = source.IndexOf(endMarker, startIndex, StringComparison.Ordinal);
            Assert.NotEqual(-1, endIndex);

            return source[startIndex..endIndex];
        }

        private static string GetRepositoryRoot()
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

            return directory.FullName;
        }
    }
}
