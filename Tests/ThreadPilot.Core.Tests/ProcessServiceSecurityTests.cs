/*
 * ThreadPilot - process service security guard tests.
 */
namespace ThreadPilot.Core.Tests
{
    using Moq;
    using ThreadPilot.Models;
    using ThreadPilot.Services;

    /// <summary>
    /// Unit tests for security guard behavior in <see cref="ProcessService"/>.
    /// </summary>
    public sealed class ProcessServiceSecurityTests
    {
        /// <summary>
        /// Ensures protected process priority updates are blocked before mutation.
        /// </summary>
        [Fact]
        public async Task SetProcessPriority_ThrowsUnauthorized_ForProtectedProcess()
        {
            var security = new Mock<ISecurityService>(MockBehavior.Strict);
            security
                .Setup(s => s.ValidateProcessOperation("lsass", "SetProcessPriority"))
                .Returns(false);
            security
                .Setup(s => s.AuditElevatedAction("SetProcessPriority", "lsass", false))
                .Returns(Task.CompletedTask);

            var service = new ProcessService(null, security.Object);
            var process = new ProcessModel { Name = "lsass", ProcessId = 500 };

            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
                await service.SetProcessPriority(process, System.Diagnostics.ProcessPriorityClass.Normal));

            security.VerifyAll();
        }

        /// <summary>
        /// Ensures protected process affinity updates are blocked before mutation.
        /// </summary>
        [Fact]
        public async Task SetProcessorAffinity_ThrowsUnauthorized_ForProtectedProcess()
        {
            var security = new Mock<ISecurityService>(MockBehavior.Strict);
            security
                .Setup(s => s.ValidateProcessOperation("System", "SetProcessAffinity"))
                .Returns(false);
            security
                .Setup(s => s.AuditElevatedAction("SetProcessAffinity", "System", false))
                .Returns(Task.CompletedTask);

            var service = new ProcessService(null, security.Object);
            var process = new ProcessModel { Name = "System", ProcessId = 4 };

            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
                await service.SetProcessorAffinity(process, 0x03));

            security.VerifyAll();
        }
    }
}
