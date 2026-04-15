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

        private static AutostartService CreateService()
        {
            var elevationService = new Mock<IElevationService>(MockBehavior.Loose);
            var elevatedTaskService = new Mock<IElevatedTaskService>(MockBehavior.Loose);
            return new AutostartService(NullLogger<AutostartService>.Instance, elevationService.Object, elevatedTaskService.Object);
        }
    }
}
