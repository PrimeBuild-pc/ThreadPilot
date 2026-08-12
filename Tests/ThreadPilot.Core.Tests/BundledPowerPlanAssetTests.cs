namespace ThreadPilot.Core.Tests
{
    public sealed class BundledPowerPlanAssetTests
    {
        [Fact]
        public void RepositoryAndReleasePipeline_DoNotBundlePowerPlanFiles()
        {
            var root = FindRepositoryRoot();
            var assetsDirectory = Path.Combine(root, "assets", "Powerplans");
            var project = File.ReadAllText(Path.Combine(root, "ThreadPilot.csproj"));
            var installer = File.ReadAllText(Path.Combine(root, "Installer", "setup.iss"));
            var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "release.yml"));

            Assert.Empty(Directory.Exists(assetsDirectory)
                ? Directory.GetFiles(assetsDirectory, "*.pow", SearchOption.AllDirectories)
                : []);
            Assert.DoesNotContain(@"assets\Powerplans", project, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(@"\Powerplans\*", installer, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("assets/Powerplans", workflow, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void CommunityAndSupportLinks_AreTheRequestedDestinations()
        {
            Assert.Equal("https://discord.gg/EmPfb57Kfr", ThreadPilot.ViewModels.PowerPlanViewModel.MorePlansUrl);
            Assert.Equal("https://discord.gg/VYwkCbr4vH", ThreadPilot.ViewModels.SettingsViewModel.DiscordUrl);
            Assert.Equal("https://github.com/PrimeBuild-pc/ThreadPilot/issues", ThreadPilot.ViewModels.SettingsViewModel.IssuesUrl);
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "ThreadPilot.csproj")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
        }
    }
}
