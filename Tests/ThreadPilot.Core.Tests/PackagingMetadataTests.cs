namespace ThreadPilot.Core.Tests
{
    using System.Text.RegularExpressions;

    public sealed partial class PackagingMetadataTests
    {
        private const string ReleaseVersion = "1.7.8-beta.1";
        private const string ReleaseAssemblyVersion = "1.7.8.0";

        [Fact]
        public void InnoInstallers_UseStableDisplayNameAndSeparateVersionMetadata()
        {
            var root = FindRepositoryRoot();
            var installerScripts = new[]
            {
                Path.Combine(root, "Installer", "setup.iss"),
                Path.Combine(root, "Installer", "Installer.iss"),
            };

            foreach (var scriptPath in installerScripts)
            {
                var script = File.ReadAllText(scriptPath);

                Assert.Contains("AppName={#MyAppName}", script, StringComparison.Ordinal);
                Assert.Contains("AppVersion={#MyAppVersion}", script, StringComparison.Ordinal);
                Assert.Contains("AppVerName={#MyAppName}", script, StringComparison.Ordinal);
                Assert.DoesNotContain("AppVerName={#MyAppName} {#MyAppVersion}", script, StringComparison.Ordinal);
                Assert.Matches(MyAppVersionRegex(), script);
            }
        }

        [Fact]
        public void PrimaryInstaller_RemovesThreadPilotOwnedDataOnlyDuringUninstall()
        {
            var root = FindRepositoryRoot();
            var script = File.ReadAllText(Path.Combine(root, "Installer", "setup.iss"));

            Assert.Contains("[UninstallDelete]", script, StringComparison.Ordinal);
            Assert.Contains("Name: \"{userappdata}\\ThreadPilot\"", script, StringComparison.Ordinal);
            Assert.Contains("ThreadPilot user data is preserved during install/update", script, StringComparison.Ordinal);
            Assert.Contains("ThreadPilot_Launch", script, StringComparison.Ordinal);
            Assert.Contains("ThreadPilot_Startup", script, StringComparison.Ordinal);
            Assert.DoesNotContain("[InstallDelete]", script, StringComparison.Ordinal);
            Assert.DoesNotContain("Name: \"{userappdata}\"", script, StringComparison.Ordinal);
        }

        [Fact]
        public void PortableUninstallers_RemoveCurrentAndLegacyStartupTasks()
        {
            var root = FindRepositoryRoot();
            var paths = new[]
            {
                Path.Combine(root, "build", "build-release.ps1"),
                Path.Combine(root, "build", "package-release-zips.ps1"),
                Path.Combine(root, ".github", "workflows", "release.yml"),
            };

            foreach (var path in paths)
            {
                var script = File.ReadAllText(path);
                Assert.Contains("ThreadPilot_Launch", script, StringComparison.Ordinal);
                Assert.Contains("ThreadPilot_Startup", script, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void PrimaryInstaller_CleansOnlyRecognizedLegacyBetaUninstallRegistryEntries()
        {
            var root = FindRepositoryRoot();
            var script = File.ReadAllText(Path.Combine(root, "Installer", "setup.iss"));

            Assert.Contains("ThreadPilot 0.1.0-beta", script, StringComparison.Ordinal);
            Assert.Contains("DeleteLegacyBetaUninstallEntry", script, StringComparison.Ordinal);
            Assert.Contains("DisplayName", script, StringComparison.Ordinal);
            Assert.Contains("InstallLocation", script, StringComparison.Ordinal);
            Assert.Contains("{autopf}\\ThreadPilot", script, StringComparison.Ordinal);
            Assert.DoesNotContain(@"DeleteKeyIncludingSubkeys(HKLM, 'Software\Microsoft\Windows\CurrentVersion\Uninstall')", script, StringComparison.Ordinal);
        }

        [Fact]
        public void VersionMetadata_IsBumpedToReleaseVersion()
        {
            var root = FindRepositoryRoot();

            AssertFileContains(Path.Combine(root, "ThreadPilot.csproj"), $"<Version>{ReleaseVersion}</Version>");
            AssertFileContains(Path.Combine(root, "ThreadPilot.csproj"), $"<AssemblyVersion>{ReleaseAssemblyVersion}</AssemblyVersion>");
            AssertFileContains(Path.Combine(root, "ThreadPilot.csproj"), $"<FileVersion>{ReleaseAssemblyVersion}</FileVersion>");
            AssertFileContains(Path.Combine(root, "ThreadPilot.csproj"), $"<InformationalVersion>{ReleaseVersion}</InformationalVersion>");
            AssertFileContains(Path.Combine(root, "app.manifest"), $"version=\"{ReleaseAssemblyVersion}\"");
            AssertFileContains(Path.Combine(root, "Installer", "ThreadPilot.wxs"), $"Version=\"{ReleaseAssemblyVersion}\"");
            AssertFileContains(Path.Combine(root, "chocolatey", "threadpilot.nuspec"), $"<version>{ReleaseVersion}</version>");
            AssertFileContains(Path.Combine(root, "chocolatey", "threadpilot.nuspec"), $"releases/tag/v{ReleaseVersion}");
            AssertFileContains(Path.Combine(root, "sonar-project.properties"), $"sonar.projectVersion={ReleaseVersion}");
            AssertFileContains(Path.Combine(root, "build", "build-release.ps1"), $"[string]$Version = \"{ReleaseVersion}\"");
            AssertFileContains(Path.Combine(root, "build", "build-installer.ps1"), $"[string]$Version = \"{ReleaseVersion}\"");
            AssertFileContains(Path.Combine(root, "build", "package-release-zips.ps1"), $"[string]$Version = \"{ReleaseVersion}\"");
            AssertFileContains(Path.Combine(root, "docs", "release", "RELEASE_NOTES.md"), $"v{ReleaseVersion}");
        }

        [Fact]
        public void ReleaseWorkflow_SeparatesStableAndPreviewChannels()
        {
            var root = FindRepositoryRoot();
            var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "release.yml"));
            Assert.Contains("prerelease: ${{ needs.build.outputs.prerelease == 'true' }}", workflow, StringComparison.Ordinal);
            Assert.Contains("make_latest: ${{ needs.build.outputs.prerelease == 'true' && 'false' || 'true' }}", workflow, StringComparison.Ordinal);
            Assert.Contains("if: needs.release.result == 'success' && needs.build.outputs.prerelease != 'true'", workflow, StringComparison.Ordinal);
            Assert.Contains("cp \"docs/releases/${{ needs.build.outputs.tag }}.md\" release-notes.md", workflow, StringComparison.Ordinal);
            AssertFileContains(Path.Combine(root, ".github", "workflows", "ci-devsecops.yml"), "runs-on: windows-2025");
            Assert.True(File.Exists(Path.Combine(root, "docs", "releases", $"v{ReleaseVersion}.md")));
        }

        private static void AssertFileContains(string path, string expected)
        {
            var content = File.ReadAllText(path);
            Assert.Contains(expected, content, StringComparison.Ordinal);
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "ThreadPilot.csproj")) &&
                    Directory.Exists(Path.Combine(directory.FullName, "Installer")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException("Repository root could not be located.");
        }

        [GeneratedRegex("#define MyAppVersion \"1\\.7\\.8-beta\\.1\"", RegexOptions.CultureInvariant)]
        private static partial Regex MyAppVersionRegex();
    }
}
