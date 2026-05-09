namespace ThreadPilot.Core.Tests
{
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using ThreadPilot.Models;
    using ThreadPilot.Services;

    public sealed class AffinityApplyServiceTests
    {
        [Fact]
        public async Task ApplyAsync_WhenVerifiedMaskMatches_ReturnsSuccess()
        {
            var process = new ProcessModel { ProcessId = 42, Name = "Game", ProcessorAffinity = 3 };
            var processService = CreateProcessService(processStillRunning: true);
            processService
                .Setup(service => service.SetProcessorAffinity(process, 1))
                .Returns(Task.CompletedTask);
            processService
                .Setup(service => service.RefreshProcessInfo(process))
                .Callback(() => process.ProcessorAffinity = 1)
                .Returns(Task.CompletedTask);

            var service = CreateService(processService);

            var result = await service.ApplyAsync(process, 1);

            Assert.True(result.Success);
            Assert.Equal(1, result.RequestedMask);
            Assert.Equal(1, result.VerifiedMask);
            Assert.Equal(AffinityApplyFailureReason.None, result.FailureReason);
        }

        [Fact]
        public async Task ApplyAsync_WhenProcessIsTerminated_ReturnsFailureWithoutApplying()
        {
            var process = new ProcessModel { ProcessId = 42, Name = "Game", ProcessorAffinity = 3 };
            var processService = CreateProcessService(processStillRunning: false);
            var service = CreateService(processService);

            var result = await service.ApplyAsync(process, 1);

            Assert.False(result.Success);
            Assert.Equal(AffinityApplyFailureReason.ProcessTerminated, result.FailureReason);
            processService.Verify(
                service => service.SetProcessorAffinity(It.IsAny<ProcessModel>(), It.IsAny<long>()),
                Times.Never);
        }

        [Fact]
        public async Task ApplyAsync_WhenAccessDenied_ReturnsAccessDeniedFailure()
        {
            var process = new ProcessModel { ProcessId = 42, Name = "Game", ProcessorAffinity = 3 };
            var processService = CreateProcessService(processStillRunning: true);
            processService
                .Setup(service => service.SetProcessorAffinity(process, 1))
                .ThrowsAsync(new InvalidOperationException("Access denied while setting processor affinity."));

            var service = CreateService(processService);

            var result = await service.ApplyAsync(process, 1);

            Assert.False(result.Success);
            Assert.Equal(AffinityApplyFailureReason.AccessDenied, result.FailureReason);
            Assert.Equal(3, result.VerifiedMask);
        }

        [Fact]
        public async Task ApplyAsync_WhenVerifiedMaskDiffers_ReturnsMismatchFailure()
        {
            var process = new ProcessModel { ProcessId = 42, Name = "Game", ProcessorAffinity = 3 };
            var processService = CreateProcessService(processStillRunning: true);
            processService
                .Setup(service => service.SetProcessorAffinity(process, 1))
                .Returns(Task.CompletedTask);
            processService
                .Setup(service => service.RefreshProcessInfo(process))
                .Callback(() => process.ProcessorAffinity = 2)
                .Returns(Task.CompletedTask);

            var service = CreateService(processService);

            var result = await service.ApplyAsync(process, 1);

            Assert.False(result.Success);
            Assert.Equal(AffinityApplyFailureReason.VerificationMismatch, result.FailureReason);
            Assert.Equal(1, result.RequestedMask);
            Assert.Equal(2, result.VerifiedMask);
        }

        [Fact]
        public async Task ApplyAsync_WhenMaskIsZero_ReturnsInvalidMaskFailure()
        {
            var process = new ProcessModel { ProcessId = 42, Name = "Game", ProcessorAffinity = 3 };
            var processService = CreateProcessService(processStillRunning: true);
            var service = CreateService(processService);

            var result = await service.ApplyAsync(process, 0);

            Assert.False(result.Success);
            Assert.Equal(AffinityApplyFailureReason.InvalidMask, result.FailureReason);
        }

        private static AffinityApplyService CreateService(Mock<IProcessService> processService)
        {
            var topologyService = new Mock<ICpuTopologyService>(MockBehavior.Loose);
            topologyService.Setup(service => service.IsAffinityMaskValid(It.IsAny<long>())).Returns(true);

            return new AffinityApplyService(
                processService.Object,
                topologyService.Object,
                NullLogger<AffinityApplyService>.Instance);
        }

        private static Mock<IProcessService> CreateProcessService(bool processStillRunning)
        {
            var processService = new Mock<IProcessService>(MockBehavior.Strict);
            processService
                .Setup(service => service.IsProcessStillRunning(It.IsAny<ProcessModel>()))
                .ReturnsAsync(processStillRunning);
            return processService;
        }
    }
}
