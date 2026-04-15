/*
 * ThreadPilot - security service unit tests.
 */
namespace ThreadPilot.Core.Tests
{
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using ThreadPilot.Services;

    /// <summary>
    /// Unit tests for <see cref="SecurityService"/> validation behavior.
    /// </summary>
    public sealed class SecurityServiceTests
    {
        /// <summary>
        /// Ensures protected processes cannot be modified.
        /// </summary>
        [Theory]
        [InlineData("lsass")]
        [InlineData("lsass.exe")]
        [InlineData("csrss")]
        [InlineData("wininit.exe")]
        public void ValidateProcessOperation_ReturnsFalse_ForProtectedProcesses(string processName)
        {
            var service = CreateService();

            var allowed = service.ValidateProcessOperation(processName, "SetProcessPriority");

            Assert.False(allowed);
        }

        /// <summary>
        /// Ensures known-safe process operations remain allowed.
        /// </summary>
        [Fact]
        public void ValidateProcessOperation_ReturnsTrue_ForAllowedOperationOnRegularProcess()
        {
            var service = CreateService();

            var allowed = service.ValidateProcessOperation("notepad", "SetProcessAffinity");

            Assert.True(allowed);
        }

        /// <summary>
        /// Ensures invalid process operations are rejected.
        /// </summary>
        [Fact]
        public void ValidateProcessOperation_ReturnsFalse_ForInvalidOperation()
        {
            var service = CreateService();

            var allowed = service.ValidateProcessOperation("notepad", "TerminateProcess");

            Assert.False(allowed);
        }

        /// <summary>
        /// Ensures elevated operation validation tolerates log-control characters.
        /// </summary>
        [Fact]
        public void ValidateElevatedOperation_ReturnsTrue_ForKnownOperation_WithControlCharacters()
        {
            var service = CreateService();

            var allowed = service.ValidateElevatedOperation("SetProcessPriority\r\n");

            Assert.True(allowed);
        }

        private static SecurityService CreateService()
        {
            var enhancedLogger = new Mock<IEnhancedLoggingService>(MockBehavior.Loose);
            return new SecurityService(NullLogger<SecurityService>.Instance, enhancedLogger.Object);
        }
    }
}
