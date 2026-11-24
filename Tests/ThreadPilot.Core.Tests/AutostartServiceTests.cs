/*
 * ThreadPilot - autostart service unit tests.
 */
namespace ThreadPilot.Core.Tests
{
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using ThreadPilot.Services;

    public sealed class AutostartServiceTests
    {
        [Fact]
        public void GetAutostartArguments_IncludesStartMinimized_WhenRequested()
        {
            var service = CreateService();

            var args = service.GetAutostartArguments(startMinimized: true);

            Assert.Contains("--start-minimized", args, StringComparison.Ordinal);
            Assert.Contains("--autostart", args, StringComparison.Ordinal);
        }

        [Fact]
        public void GetAutostartArguments_OmitsStartMinimized_WhenNotRequested()
        {
            var service = CreateService();

            var args = service.GetAutostartArguments(startMinimized: false);

            Assert.DoesNotContain("--start-minimized", args, StringComparison.Ordinal);
            Assert.Equal("--autostart", args);
        }

        [Fact]
        public async Task EnableAutostartAsync_WhenNotAdmin_RequestsElevation_AndReturnsFalse()
        {
            var elevationService = new Mock<IElevationService>(MockBehavior.Strict);
            elevationService.Setup(x => x.IsRunningAsAdministrator()).Returns(false);
            elevationService.Setup(x => x.RequestElevationIfNeeded()).ReturnsAsync(true);

            var elevatedTaskService = new Mock<IElevatedTaskService>(MockBehavior.Loose);
            elevatedTaskService.Setup(x => x.IsAutostartTaskRegisteredAsync()).ReturnsAsync(false);

            var service = CreateService(elevationService, elevatedTaskService);

            var result = await service.EnableAutostartAsync(startMinimized: true);

            Assert.False(result);
            elevationService.Verify(x => x.RequestElevationIfNeeded(), Times.Once);
            elevatedTaskService.Verify(
                x => x.EnsureAutostartTaskAsync(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task EnableAutostartAsync_WhenAdmin_EnsuresScheduledTask()
        {
            var elevationService = new Mock<IElevationService>(MockBehavior.Strict);
            elevationService.Setup(x => x.IsRunningAsAdministrator()).Returns(true);

            var elevatedTaskService = new Mock<IElevatedTaskService>(MockBehavior.Strict);
            elevatedTaskService.Setup(x => x.IsAutostartTaskRegisteredAsync()).ReturnsAsync(false);
            elevatedTaskService
                .Setup(x => x.EnsureAutostartTaskAsync(It.IsAny<string>(), It.Is<string>(args => args.Contains("--autostart", StringComparison.Ordinal))))
                .ReturnsAsync(true);
            elevatedTaskService.Setup(x => x.EnsureLaunchTaskAsync()).ReturnsAsync(true);

            var service = CreateService(elevationService, elevatedTaskService);

            var result = await service.EnableAutostartAsync(startMinimized: true);

            Assert.True(result);
            elevatedTaskService.Verify(x => x.EnsureLaunchTaskAsync(), Times.Once);
            elevatedTaskService.Verify(
                x => x.EnsureAutostartTaskAsync(
                    It.IsAny<string>(),
                    It.Is<string>(args =>
                        args.Contains("--autostart", StringComparison.Ordinal) &&
                        args.Contains("--start-minimized", StringComparison.Ordinal))),
                Times.Once);
        }

        [Fact]
        public async Task DisableAutostartAsync_WhenAdmin_RemovesScheduledTask()
        {
            var elevationService = new Mock<IElevationService>(MockBehavior.Strict);
            elevationService.Setup(x => x.IsRunningAsAdministrator()).Returns(true);

            var elevatedTaskService = new Mock<IElevatedTaskService>(MockBehavior.Strict);
            elevatedTaskService.Setup(x => x.IsAutostartTaskRegisteredAsync()).ReturnsAsync(false);
            elevatedTaskService.Setup(x => x.RemoveAutostartTaskAsync()).ReturnsAsync(true);
            elevatedTaskService.Setup(x => x.RemoveLaunchTaskAsync()).ReturnsAsync(true);

            var service = CreateService(elevationService, elevatedTaskService);

            var result = await service.DisableAutostartAsync();

            Assert.True(result);
            elevatedTaskService.Verify(x => x.RemoveAutostartTaskAsync(), Times.Once);
            elevatedTaskService.Verify(x => x.RemoveLaunchTaskAsync(), Times.Once);
        }

        [Fact]
        public async Task LegacyRunEntryAlone_DoesNotCountAsAutostart()
        {
            // Windows does not start an admin-only exe from HKCU\Run at logon, so a leftover entry from an
            // old version must not stop the scheduled task from being created or show autostart as on.
            var elevatedTask = new Mock<IElevatedTaskService>(MockBehavior.Loose);
            elevatedTask.Setup(x => x.IsAutostartTaskRegisteredAsync()).ReturnsAsync(false);
            var service = new LegacyRunEntryAutostartService(new Mock<IElevationService>().Object, elevatedTask.Object);

            Assert.False(await service.CheckAutostartStatusAsync());
            Assert.False(service.IsAutostartEnabled);
        }

        [Fact]
        public async Task EnsureAutostartAsync_WhenTaskPointsAtAnOlderPackageFolder_ReRegistersIt()
        {
            // A Store update moves the exe to a new versioned folder; the old task would fail at logon.
            var elevation = new Mock<IElevationService>();
            elevation.Setup(x => x.IsRunningAsAdministrator()).Returns(true);
            var elevatedTask = new Mock<IElevatedTaskService>();
            elevatedTask.Setup(x => x.GetAutostartTaskExecutablePathAsync())
                .ReturnsAsync(@"C:\Program Files\WindowsApps\LorenzoMassafra.ThreadPilot_1.7.10.0_x64__abc\App\ThreadPilot.exe");
            elevatedTask.Setup(x => x.EnsureAutostartTaskAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
            elevatedTask.Setup(x => x.EnsureLaunchTaskAsync()).ReturnsAsync(true);
            var service = new FixedPathAutostartService(elevation.Object, elevatedTask.Object, CurrentPackageExe);

            await service.EnsureAutostartAsync(startMinimized: true);

            elevatedTask.Verify(x => x.EnsureAutostartTaskAsync(CurrentPackageExe, It.Is<string>(a => a.Contains("--autostart", StringComparison.Ordinal))), Times.Once);
            elevatedTask.Verify(x => x.EnsureLaunchTaskAsync(), Times.Once);
        }

        [Fact]
        public async Task EnsureAutostartAsync_WhenTaskAlreadyPointsHere_OnlyRefreshesTheLaunchTask()
        {
            var elevatedTask = new Mock<IElevatedTaskService>();
            elevatedTask.Setup(x => x.GetAutostartTaskExecutablePathAsync()).ReturnsAsync(CurrentPackageExe.ToUpperInvariant());
            elevatedTask.Setup(x => x.EnsureLaunchTaskAsync()).ReturnsAsync(true);
            var service = new FixedPathAutostartService(new Mock<IElevationService>().Object, elevatedTask.Object, CurrentPackageExe);

            await service.EnsureAutostartAsync(startMinimized: true);

            elevatedTask.Verify(x => x.EnsureAutostartTaskAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            elevatedTask.Verify(x => x.EnsureLaunchTaskAsync(), Times.Once);
        }

        private const string CurrentPackageExe = @"C:\Program Files\WindowsApps\LorenzoMassafra.ThreadPilot_1.7.11.0_x64__abc\App\ThreadPilot.exe";

        private sealed class FixedPathAutostartService(IElevationService elevation, IElevatedTaskService elevatedTask, string exePath)
            : AutostartService(NullLogger<AutostartService>.Instance, elevation, elevatedTask)
        {
            internal override string? GetExecutablePath() => exePath;
        }

        private sealed class LegacyRunEntryAutostartService(IElevationService elevation, IElevatedTaskService elevatedTask)
            : AutostartService(NullLogger<AutostartService>.Instance, elevation, elevatedTask)
        {
            internal override string? TryReadLegacyRegistryAutostart() => @"""C:\Apps\ThreadPilot\ThreadPilot.exe"" --autostart";
        }

        private static AutostartService CreateService(
            Mock<IElevationService>? elevationService = null,
            Mock<IElevatedTaskService>? elevatedTaskService = null)
        {
            var elevation = elevationService ?? new Mock<IElevationService>(MockBehavior.Loose);
            var elevatedTask = elevatedTaskService ?? new Mock<IElevatedTaskService>(MockBehavior.Loose);
            elevatedTask.Setup(x => x.IsAutostartTaskRegisteredAsync()).ReturnsAsync(false);

            return new AutostartService(NullLogger<AutostartService>.Instance, elevation.Object, elevatedTask.Object);
        }
    }
}
