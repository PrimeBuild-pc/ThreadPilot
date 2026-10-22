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

            var service = CreateService(elevationService, elevatedTaskService);

            var result = await service.EnableAutostartAsync(startMinimized: true);

            Assert.True(result);
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

            var service = CreateService(elevationService, elevatedTaskService);

            var result = await service.DisableAutostartAsync();

            Assert.True(result);
            elevatedTaskService.Verify(x => x.RemoveAutostartTaskAsync(), Times.Once);
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
