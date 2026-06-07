namespace ThreadPilot.Core.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using ThreadPilot.Models;
    using ThreadPilot.Services;
    using ThreadPilot.Services.Abstractions;

    public sealed class UpdateServiceTests
    {
        [Fact]
        public void SemanticVersion_OrdersStableAbovePrerelease()
        {
            Assert.True(SemanticVersion.TryParse("v1.4.0-beta.1", out var prerelease));
            Assert.True(SemanticVersion.TryParse("1.4.0", out var stable));

            Assert.True(stable > prerelease);
        }

        [Fact]
        public async Task GitHubUpdateChecker_ExcludesPrereleasesByDefault()
        {
            var checker = new GitHubUpdateChecker(new FakeGitHubReleaseClient(
                """
                [
                  { "tag_name": "v1.5.0-beta.1", "prerelease": true, "draft": false, "html_url": "https://github.com/PrimeBuild-pc/ThreadPilot/releases/tag/v1.5.0-beta.1", "assets": [] },
                  { "tag_name": "v1.4.0", "prerelease": false, "draft": false, "html_url": "https://github.com/PrimeBuild-pc/ThreadPilot/releases/tag/v1.4.0", "assets": [] }
                ]
                """));

            var release = await checker.GetLatestReleaseInfoAsync("PrimeBuild-pc", "ThreadPilot");

            Assert.NotNull(release);
            Assert.Equal("1.4.0", release.Version.ToString());
        }

        [Fact]
        public async Task CheckForUpdatesAsync_StartupSkipsWhenLastCheckInsideInterval()
        {
            var harness = new Harness();
            harness.Settings.LastUpdateCheckUtc = harness.Clock.UtcNow.AddDays(-2);

            var result = await harness.Service.CheckForUpdatesAsync(new UpdateCheckRequest(UpdateCheckTrigger.Startup));

            Assert.Equal(UpdateCheckStatus.Skipped, result.Status);
            Assert.False(harness.ReleaseClient.RequestedReleases);
        }

        [Fact]
        public async Task CheckForUpdatesAsync_ManualFindsNewerStableRelease()
        {
            var harness = new Harness();

            var result = await harness.Service.CheckForUpdatesAsync(new UpdateCheckRequest(UpdateCheckTrigger.Manual));

            Assert.True(result.IsUpdateAvailable);
            Assert.Equal("1.4.0", result.Release?.Version.ToString());
            Assert.Equal(harness.Clock.UtcNow, harness.SavedSettings?.LastUpdateCheckUtc);
        }

        [Fact]
        public void UpdateAssetSelector_SelectsInstallerAndRejectsPortable()
        {
            var release = CreateRelease(
                new UpdateAsset("ThreadPilot_v1.4.0_Portable.zip", new Uri("https://github.com/PrimeBuild-pc/ThreadPilot/releases/download/v1.4.0/ThreadPilot_v1.4.0_Portable.zip"), 1),
                new UpdateAsset("ThreadPilot_v1.4.0_Setup.exe", new Uri("https://github.com/PrimeBuild-pc/ThreadPilot/releases/download/v1.4.0/ThreadPilot_v1.4.0_Setup.exe"), 1));

            var selected = UpdateAssetSelector.TrySelectInstaller(release, out var asset);

            Assert.True(selected);
            Assert.Equal("ThreadPilot_v1.4.0_Setup.exe", asset.Name);
        }

        [Fact]
        public async Task DownloadInstallerAsync_VerifiesChecksum()
        {
            using var tempRoot = new TempDirectory();
            var installerBytes = Encoding.UTF8.GetBytes("installer-content");
            var expectedHash = ComputeSha256(installerBytes);
            var client = new FakeUpdateDownloadClient(installerBytes, $"{expectedHash}  ThreadPilot_v1.4.0_Setup.exe");
            var service = CreateDownloadService(tempRoot.Path, client);

            var result = await service.DownloadInstallerAsync(CreateReleaseWithInstallerAndChecksum());

            Assert.True(result.ChecksumVerified);
            Assert.True(File.Exists(result.InstallerPath));
        }

        [Fact]
        public async Task DownloadInstallerAsync_RejectsInvalidChecksumAndCleansTemp()
        {
            using var tempRoot = new TempDirectory();
            var client = new FakeUpdateDownloadClient(Encoding.UTF8.GetBytes("installer-content"), $"{new string('0', 64)}  ThreadPilot_v1.4.0_Setup.exe");
            var service = CreateDownloadService(tempRoot.Path, client);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.DownloadInstallerAsync(CreateReleaseWithInstallerAndChecksum()));
            Assert.Empty(Directory.GetDirectories(tempRoot.Path));
        }

        [Fact]
        public void UpdateTempDirectoryProvider_DoesNotDeleteOutsideUpdateRoot()
        {
            using var tempRoot = new TempDirectory();
            using var outside = new TempDirectory();
            File.WriteAllText(Path.Combine(outside.Path, "settings.json"), "{}");
            var provider = new UpdateTempDirectoryProvider(tempRoot.Path);

            provider.Cleanup(outside.Path);

            Assert.True(File.Exists(Path.Combine(outside.Path, "settings.json")));
        }

        [Fact]
        public async Task StartupCheck_DoesNotDownloadOrInstallWithoutUserConsent()
        {
            var harness = new Harness();

            var result = await harness.Service.CheckForUpdatesAsync(new UpdateCheckRequest(UpdateCheckTrigger.Startup));

            Assert.True(result.IsUpdateAvailable);
            harness.Download.Verify(service => service.DownloadInstallerAsync(It.IsAny<UpdateReleaseInfo>(), It.IsAny<CancellationToken>()), Times.Never);
            harness.Installer.Verify(service => service.LaunchInstallerElevatedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task DownloadAndInstallAsync_StartsInstallerAndRequestsShutdown()
        {
            var harness = new Harness();
            harness.Download
                .Setup(service => service.DownloadInstallerAsync(It.IsAny<UpdateReleaseInfo>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UpdateDownloadResult(
                    Path.Combine(harness.TempDirectory, "ThreadPilot_v1.4.0_Setup.exe"),
                    harness.TempDirectory,
                    true,
                    UpdateSignatureStatus.Unknown,
                    "ok"));
            File.WriteAllText(Path.Combine(harness.TempDirectory, "ThreadPilot_v1.4.0_Setup.exe"), "installer");

            var result = await harness.Service.DownloadAndInstallAsync(CreateReleaseWithInstallerAndChecksum());

            Assert.Equal(UpdateInstallStatus.Started, result.Status);
            harness.Installer.Verify(service => service.LaunchInstallerElevatedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            harness.Shutdown.Verify(service => service.RequestShutdownForUpdate(), Times.Once);
        }

        private static UpdateDownloadService CreateDownloadService(string tempRoot, IUpdateDownloadClient client)
        {
            var signature = new Mock<IUpdateSignatureVerifier>();
            signature.Setup(verifier => verifier.Verify(It.IsAny<string>())).Returns(UpdateSignatureStatus.Unknown);
            return new UpdateDownloadService(
                client,
                new UpdateTempDirectoryProvider(tempRoot),
                signature.Object,
                NullLogger<UpdateDownloadService>.Instance);
        }

        private static UpdateReleaseInfo CreateReleaseWithInstallerAndChecksum()
        {
            return CreateRelease(
                new UpdateAsset("ThreadPilot_v1.4.0_Setup.exe", new Uri("https://github.com/PrimeBuild-pc/ThreadPilot/releases/download/v1.4.0/ThreadPilot_v1.4.0_Setup.exe"), 10),
                new UpdateAsset("SHA256SUMS.txt", new Uri("https://github.com/PrimeBuild-pc/ThreadPilot/releases/download/v1.4.0/SHA256SUMS.txt"), 10));
        }

        private static UpdateReleaseInfo CreateRelease(params UpdateAsset[] assets)
        {
            return new UpdateReleaseInfo(
                new SemanticVersion(1, 4, 0),
                "v1.4.0",
                new Uri("https://github.com/PrimeBuild-pc/ThreadPilot/releases/tag/v1.4.0"),
                false,
                assets);
        }

        private static string ComputeSha256(byte[] bytes)
        {
            var hash = System.Security.Cryptography.SHA256.HashData(bytes);
            return Convert.ToHexString(hash);
        }

        private sealed class Harness
        {
            public ApplicationSettingsModel Settings { get; } = new();

            public ApplicationSettingsModel? SavedSettings { get; private set; }

            public FakeClock Clock { get; } = new();

            public FakeGitHubReleaseClient ReleaseClient { get; } = new(
                """
                [
                  {
                    "tag_name": "v1.4.0",
                    "prerelease": false,
                    "draft": false,
                    "html_url": "https://github.com/PrimeBuild-pc/ThreadPilot/releases/tag/v1.4.0",
                    "assets": [
                      { "name": "ThreadPilot_v1.4.0_Setup.exe", "browser_download_url": "https://github.com/PrimeBuild-pc/ThreadPilot/releases/download/v1.4.0/ThreadPilot_v1.4.0_Setup.exe", "size": 100 },
                      { "name": "SHA256SUMS.txt", "browser_download_url": "https://github.com/PrimeBuild-pc/ThreadPilot/releases/download/v1.4.0/SHA256SUMS.txt", "size": 100 }
                    ]
                  }
                ]
                """);

            public Mock<IUpdateDownloadService> Download { get; } = new(MockBehavior.Strict);

            public Mock<IUpdateInstallerService> Installer { get; } = new(MockBehavior.Strict);

            public Mock<IApplicationShutdownService> Shutdown { get; } = new(MockBehavior.Strict);

            public string TempDirectory { get; }

            public UpdateService Service { get; }

            public Harness()
            {
                this.TempDirectory = Directory.CreateTempSubdirectory("ThreadPilotUpdateTest").FullName;
                var settingsService = new Mock<IApplicationSettingsService>();
                settingsService.SetupGet(service => service.Settings).Returns(() => (ApplicationSettingsModel)this.Settings.Clone());
                settingsService
                    .Setup(service => service.UpdateSettingsAsync(It.IsAny<ApplicationSettingsModel>()))
                    .Callback<ApplicationSettingsModel>(settings =>
                    {
                        this.SavedSettings = (ApplicationSettingsModel)settings.Clone();
                        this.Settings.CopyFrom(settings);
                    })
                    .Returns(Task.CompletedTask);

                var versionProvider = new Mock<IApplicationVersionProvider>();
                versionProvider.SetupGet(provider => provider.CurrentVersion).Returns(new SemanticVersion(1, 3, 1));
                versionProvider.SetupGet(provider => provider.DisplayVersion).Returns("v1.3.1");

                var tempProvider = new Mock<IUpdateTempDirectoryProvider>();
                tempProvider.Setup(provider => provider.Cleanup(It.IsAny<string>()));

                this.Shutdown.Setup(service => service.RequestShutdownForUpdate());
                this.Installer
                    .Setup(service => service.LaunchInstallerElevatedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

                this.Service = new UpdateService(
                    new GitHubUpdateChecker(this.ReleaseClient),
                    settingsService.Object,
                    versionProvider.Object,
                    this.Download.Object,
                    this.Installer.Object,
                    tempProvider.Object,
                    this.Shutdown.Object,
                    this.Clock,
                    NullLogger<UpdateService>.Instance);
            }
        }

        private sealed class FakeClock : IUpdateClock
        {
            public DateTimeOffset UtcNow { get; } = new(2026, 6, 7, 12, 0, 0, TimeSpan.Zero);
        }

        private sealed class FakeGitHubReleaseClient : IGitHubReleaseClient
        {
            private readonly string releasesJson;

            public bool RequestedReleases { get; private set; }

            public FakeGitHubReleaseClient(string releasesJson)
            {
                this.releasesJson = releasesJson;
            }

            public Task<string> GetLatestReleaseJsonAsync(string owner, string repo, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<string> GetReleasesJsonAsync(string owner, string repo, CancellationToken cancellationToken = default)
            {
                this.RequestedReleases = true;
                return Task.FromResult(this.releasesJson);
            }
        }

        private sealed class FakeUpdateDownloadClient : IUpdateDownloadClient
        {
            private readonly byte[] fileBytes;
            private readonly string? checksumsText;

            public FakeUpdateDownloadClient(byte[] fileBytes, string? checksumsText)
            {
                this.fileBytes = fileBytes;
                this.checksumsText = checksumsText;
            }

            public Task DownloadFileAsync(Uri uri, string destinationPath, CancellationToken cancellationToken = default)
            {
                File.WriteAllBytes(destinationPath, this.fileBytes);
                return Task.CompletedTask;
            }

            public Task<string?> TryDownloadStringAsync(Uri uri, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(this.checksumsText);
            }
        }

        private sealed class TempDirectory : IDisposable
        {
            public string Path { get; } = Directory.CreateTempSubdirectory("ThreadPilotUpdateTest").FullName;

            public void Dispose()
            {
                if (Directory.Exists(this.Path))
                {
                    Directory.Delete(this.Path, recursive: true);
                }
            }
        }
    }
}
