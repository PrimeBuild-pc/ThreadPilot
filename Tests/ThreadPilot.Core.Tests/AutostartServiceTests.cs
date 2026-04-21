/*
 * ThreadPilot - autostart service unit tests.
 */
namespace ThreadPilot.Core.Tests
{
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using ThreadPilot.Services;

    /// <summary>
    /// Unit tests for non-registry behavior in <see cref="AutostartService"/>.
    /// </summary>
    public sealed class AutostartServiceTests
    {
        /// <summary>
        /// Ensures autostart arguments include both autostart and start-minimized flags when requested.
        /// </summary>
        [Fact]
        public void GetAutostartArguments_IncludesStartMinimized_WhenRequested()
        {
            var service = CreateService();

            var args = service.GetAutostartArguments(startMinimized: true);

            Assert.Contains("--start-minimized", args, StringComparison.Ordinal);
            Assert.Contains("--autostart", args, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures start-minimized is omitted when not requested.
        /// </summary>
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
