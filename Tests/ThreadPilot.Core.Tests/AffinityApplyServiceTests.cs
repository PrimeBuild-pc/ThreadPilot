namespace ThreadPilot.Core.Tests
{
    using System.ComponentModel;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using ThreadPilot.Models;
    using ThreadPilot.Platforms.Windows;
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

        [Fact]
        public async Task ApplyAsync_WhenTopologyRejectsMask_ReturnsInvalidMaskFailure()
        {
            var process = new ProcessModel { ProcessId = 42, Name = "Game", ProcessorAffinity = 3 };
            var processService = CreateProcessService(processStillRunning: true);
            var topologyService = new Mock<ICpuTopologyService>(MockBehavior.Strict);
            topologyService.Setup(service => service.IsAffinityMaskValid(8)).Returns(false);
            var service = CreateService(processService, topologyService);

            var result = await service.ApplyAsync(process, 8);

            Assert.False(result.Success);
            Assert.Equal(AffinityApplyFailureReason.InvalidMask, result.FailureReason);
            processService.Verify(
                service => service.SetProcessorAffinity(It.IsAny<ProcessModel>(), It.IsAny<long>()),
                Times.Never);
        }

        [Fact]
        public async Task ApplyAsync_WhenProcessStateCheckIsAccessDenied_StillAttemptsApply()
        {
            var process = new ProcessModel { ProcessId = 42, Name = "Game", ProcessorAffinity = 3 };
            var processService = new Mock<IProcessService>(MockBehavior.Strict);
            processService
                .Setup(service => service.IsProcessStillRunning(process))
                .ThrowsAsync(new UnauthorizedAccessException("Access denied."));
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
            processService.Verify(service => service.SetProcessorAffinity(process, 1), Times.Once);
        }

        [Fact]
        public async Task ApplyAsync_WhenRefreshAfterApplyIsAccessDenied_ReportsVerificationMismatch()
        {
            var process = new ProcessModel { ProcessId = 42, Name = "Game", ProcessorAffinity = 3 };
            var processService = CreateProcessService(processStillRunning: true);
            processService
                .Setup(service => service.SetProcessorAffinity(process, 1))
                .Returns(Task.CompletedTask);
            processService
                .Setup(service => service.RefreshProcessInfo(process))
                .ThrowsAsync(new UnauthorizedAccessException("Access denied."));
            var service = CreateService(processService);

            var result = await service.ApplyAsync(process, 1);

            Assert.False(result.Success);
            Assert.Equal(AffinityApplyFailureReason.VerificationMismatch, result.FailureReason);
            Assert.Equal(3, result.VerifiedMask);
        }

        [Fact]
        public async Task ApplyAsync_WhenApplyThrowsUnexpectedError_ReturnsApplyFailed()
        {
            var process = new ProcessModel { ProcessId = 42, Name = "Game", ProcessorAffinity = 3 };
            var processService = CreateProcessService(processStillRunning: true);
            processService
                .Setup(service => service.SetProcessorAffinity(process, 1))
                .ThrowsAsync(new InvalidOperationException("Driver rejected request."));
            processService
                .Setup(service => service.RefreshProcessInfo(process))
                .Returns(Task.CompletedTask);
            var service = CreateService(processService);

            var result = await service.ApplyAsync(process, 1);

            Assert.False(result.Success);
            Assert.Equal(AffinityApplyFailureReason.ApplyFailed, result.FailureReason);
            Assert.Equal(3, result.VerifiedMask);
        }

        [Fact]
        public async Task CpuSelectionApply_WhenCpuSetsFailAndSelectionIsSingleGroupBelow64_UsesLegacyFallback()
        {
            var process = new ProcessModel { ProcessId = 42, Name = "Game" };
            var cpuSets = new FakeCpuSetHandler { ApplyCpuSelectionResult = false };
            var legacy = new RecordingLegacyAffinityApplier();
            var service = CreateCpuSelectionApplier(cpuSets, legacy);
            var selection = CreateSelection(
                new ProcessorRef(0, 0, 0),
                new ProcessorRef(0, 2, 2));

            var result = await service.ApplyAsync(process, selection);

            Assert.True(result.Success);
            Assert.Equal(AffinityApplyErrorCodes.None, result.ErrorCode);
            Assert.False(result.UsedCpuSets);
            Assert.True(result.UsedLegacyAffinity);
            Assert.Equal(0x05, legacy.LastMask);
            Assert.Equal(1, legacy.CallCount);
            Assert.Equal(1, cpuSets.ApplyCpuSelectionCalls);
        }

        [Fact]
        public async Task CpuSelectionApply_WhenCpuSetsFailAndSelectionHasMultipleGroups_BlocksLegacyFallback()
        {
            var process = new ProcessModel { ProcessId = 42, Name = "Game" };
            var cpuSets = new FakeCpuSetHandler { ApplyCpuSelectionResult = false };
            var legacy = new RecordingLegacyAffinityApplier();
            var service = CreateCpuSelectionApplier(cpuSets, legacy);
            var selection = CreateSelection(
                new ProcessorRef(0, 0, 0),
                new ProcessorRef(1, 0, 1));

            var result = await service.ApplyAsync(process, selection);

            Assert.False(result.Success);
            Assert.Equal(AffinityApplyErrorCodes.LegacyFallbackUnsafe, result.ErrorCode);
            Assert.True(result.IsLegacyFallbackBlocked);
            Assert.False(result.UsedLegacyAffinity);
            Assert.Equal(0, legacy.CallCount);
        }

        [Fact]
        public async Task CpuSelectionApply_WhenCpuSetsFailAndSelectionContainsCpu64_BlocksLegacyFallback()
        {
            var process = new ProcessModel { ProcessId = 42, Name = "Game" };
            var cpuSets = new FakeCpuSetHandler { ApplyCpuSelectionResult = false };
            var legacy = new RecordingLegacyAffinityApplier();
            var service = CreateCpuSelectionApplier(cpuSets, legacy);
            var selection = CreateSelection(new ProcessorRef(0, 64, 64));

            var result = await service.ApplyAsync(process, selection);

            Assert.False(result.Success);
            Assert.Equal(AffinityApplyErrorCodes.LegacyFallbackUnsafe, result.ErrorCode);
            Assert.True(result.IsLegacyFallbackBlocked);
            Assert.Equal(0, legacy.CallCount);
        }

        [Fact]
        public async Task CpuSelectionApply_WhenSelectionHasExplicitCpuSetIds_TriesCpuSets()
        {
            var process = new ProcessModel { ProcessId = 42, Name = "Game" };
            var cpuSets = new FakeCpuSetHandler { ApplyCpuSelectionResult = true };
            var legacy = new RecordingLegacyAffinityApplier();
            var service = CreateCpuSelectionApplier(cpuSets, legacy);
            var selection = new CpuSelection { CpuSetIds = [101, 103] };

            var result = await service.ApplyAsync(process, selection);

            Assert.True(result.Success);
            Assert.True(result.UsedCpuSets);
            Assert.False(result.UsedLegacyAffinity);
            Assert.Same(selection, cpuSets.LastSelection);
            Assert.Equal(0, legacy.CallCount);
        }

        [Fact]
        public async Task CpuSelectionApply_WhenCpuSetsSucceed_AuditsSuccessAndSkipsLegacyFallback()
        {
            var process = new ProcessModel { ProcessId = 42, Name = "Game" };
            var cpuSets = new FakeCpuSetHandler { ApplyCpuSelectionResult = true };
            var legacy = new RecordingLegacyAffinityApplier();
            var audit = new RecordingAffinityAudit();
            var service = CreateCpuSelectionApplier(cpuSets, legacy, audit);
            var selection = CreateSelection(new ProcessorRef(0, 0, 0));

            var result = await service.ApplyAsync(process, selection);

            Assert.True(result.Success);
            Assert.True(result.UsedCpuSets);
            Assert.False(result.UsedLegacyAffinity);
            Assert.Equal(0, legacy.CallCount);
            Assert.Equal([(process, true)], audit.Calls);
        }

        [Fact]
        public async Task CpuSelectionApply_WhenSelectionIsEmpty_ReturnsInvalidSelection()
        {
            var process = new ProcessModel { ProcessId = 42, Name = "Game" };
            var cpuSets = new FakeCpuSetHandler();
            var legacy = new RecordingLegacyAffinityApplier();
            var service = CreateCpuSelectionApplier(cpuSets, legacy);

            var result = await service.ApplyAsync(process, new CpuSelection());

            Assert.False(result.Success);
            Assert.Equal(AffinityApplyErrorCodes.InvalidSelection, result.ErrorCode);
            Assert.False(result.UsedCpuSets);
            Assert.False(result.UsedLegacyAffinity);
            Assert.Equal(0, cpuSets.ApplyCpuSelectionCalls);
            Assert.Equal(0, legacy.CallCount);
        }

        [Fact]
        public async Task CpuSelectionApply_WhenSelectionIsEmpty_AuditsFailure()
        {
            var process = new ProcessModel { ProcessId = 42, Name = "Game" };
            var cpuSets = new FakeCpuSetHandler();
            var legacy = new RecordingLegacyAffinityApplier();
            var audit = new RecordingAffinityAudit();
            var service = CreateCpuSelectionApplier(cpuSets, legacy, audit);

            var result = await service.ApplyAsync(process, new CpuSelection());

            Assert.False(result.Success);
            Assert.Equal(AffinityApplyErrorCodes.InvalidSelection, result.ErrorCode);
            Assert.Equal([(process, false)], audit.Calls);
        }

        [Fact]
        public async Task CpuSelectionApply_WhenLegacyFallbackIsUnsafe_AuditsFailure()
        {
            var process = new ProcessModel { ProcessId = 42, Name = "Game" };
            var cpuSets = new FakeCpuSetHandler { ApplyCpuSelectionResult = false };
            var legacy = new RecordingLegacyAffinityApplier();
            var audit = new RecordingAffinityAudit();
            var service = CreateCpuSelectionApplier(cpuSets, legacy, audit);
            var selection = CreateSelection(new ProcessorRef(1, 64, 64));

            var result = await service.ApplyAsync(process, selection);

            Assert.False(result.Success);
            Assert.True(result.IsLegacyFallbackBlocked);
            Assert.Equal(0, legacy.CallCount);
            Assert.Equal([(process, false)], audit.Calls);
        }

        [Fact]
        public async Task CpuSelectionApply_WhenLegacyFallbackSucceeds_DoesNotAuditTwice()
        {
            var process = new ProcessModel { ProcessId = 42, Name = "Game" };
            var cpuSets = new FakeCpuSetHandler { ApplyCpuSelectionResult = false };
            var legacy = new RecordingLegacyAffinityApplier();
            var audit = new RecordingAffinityAudit();
            var service = CreateCpuSelectionApplier(cpuSets, legacy, audit);
            var selection = CreateSelection(new ProcessorRef(0, 0, 0));

            var result = await service.ApplyAsync(process, selection);

            Assert.True(result.Success);
            Assert.True(result.UsedLegacyAffinity);
            Assert.Equal(1, legacy.CallCount);
            Assert.Empty(audit.Calls);
        }

        [Fact]
        public async Task CpuSelectionApply_WhenCpuSetsThrowAccessDenied_ReturnsAccessDenied()
        {
            var process = new ProcessModel { ProcessId = 42, Name = "Game" };
            var cpuSets = new FakeCpuSetHandler
            {
                ApplyCpuSelectionException = new Win32Exception(5, "Access is denied."),
            };
            var legacy = new RecordingLegacyAffinityApplier();
            var service = CreateCpuSelectionApplier(cpuSets, legacy);
            var selection = CreateSelection(new ProcessorRef(0, 0, 0));

            var result = await service.ApplyAsync(process, selection);

            Assert.False(result.Success);
            Assert.Equal(AffinityApplyErrorCodes.AccessDenied, result.ErrorCode);
            Assert.True(result.IsAccessDenied);
            Assert.Equal(0, legacy.CallCount);
        }

        [Fact]
        public async Task CpuSelectionApply_WhenFallbackThrowsAccessDenied_ReturnsAccessDenied()
        {
            var process = new ProcessModel { ProcessId = 42, Name = "Game" };
            var cpuSets = new FakeCpuSetHandler { ApplyCpuSelectionResult = false };
            var legacy = new RecordingLegacyAffinityApplier
            {
                ExceptionToThrow = new UnauthorizedAccessException("Access denied."),
            };
            var service = CreateCpuSelectionApplier(cpuSets, legacy);
            var selection = CreateSelection(new ProcessorRef(0, 0, 0));

            var result = await service.ApplyAsync(process, selection);

            Assert.False(result.Success);
            Assert.Equal(AffinityApplyErrorCodes.AccessDenied, result.ErrorCode);
            Assert.True(result.IsAccessDenied);
            Assert.Equal(1, legacy.CallCount);
        }

        [Fact]
        public async Task CpuSelectionApply_WhenFallbackThrowsProcessExited_ReturnsProcessExited()
        {
            var process = new ProcessModel { ProcessId = 42, Name = "Game" };
            var cpuSets = new FakeCpuSetHandler { ApplyCpuSelectionResult = false };
            var legacy = new RecordingLegacyAffinityApplier
            {
                ExceptionToThrow = new ArgumentException("Process has exited."),
            };
            var service = CreateCpuSelectionApplier(cpuSets, legacy);
            var selection = CreateSelection(new ProcessorRef(0, 0, 0));

            var result = await service.ApplyAsync(process, selection);

            Assert.False(result.Success);
            Assert.Equal(AffinityApplyErrorCodes.ProcessExited, result.ErrorCode);
            Assert.False(result.UsedLegacyAffinity);
        }

        [Fact]
        public async Task ApplyCpuSelectionAsync_WhenProcessIsNull_ReturnsProcessExitedWithoutDelegating()
        {
            var processService = new Mock<IProcessService>(MockBehavior.Strict);
            var service = CreateService(processService);

            var result = await service.ApplyAsync(null!, CreateSelection(new ProcessorRef(0, 0, 0)));

            Assert.False(result.Success);
            Assert.Equal(AffinityApplyErrorCodes.ProcessExited, result.ErrorCode);
            processService.Verify(
                service => service.SetProcessorAffinity(It.IsAny<ProcessModel>(), It.IsAny<CpuSelection>()),
                Times.Never);
        }

        [Fact]
        public async Task ApplyCpuSelectionAsync_WhenSelectionIsNull_ReturnsInvalidSelectionWithoutDelegating()
        {
            var process = new ProcessModel { ProcessId = 42, Name = "Game" };
            var processService = new Mock<IProcessService>(MockBehavior.Strict);
            var service = CreateService(processService);

            var result = await service.ApplyAsync(process, null!);

            Assert.False(result.Success);
            Assert.Equal(AffinityApplyErrorCodes.InvalidSelection, result.ErrorCode);
            processService.Verify(
                service => service.SetProcessorAffinity(It.IsAny<ProcessModel>(), It.IsAny<CpuSelection>()),
                Times.Never);
        }

        private static AffinityApplyService CreateService(Mock<IProcessService> processService)
        {
            var topologyService = new Mock<ICpuTopologyService>(MockBehavior.Loose);
            topologyService.Setup(service => service.IsAffinityMaskValid(It.IsAny<long>())).Returns(true);

            return CreateService(processService, topologyService);
        }

        private static AffinityApplyService CreateService(
            Mock<IProcessService> processService,
            Mock<ICpuTopologyService> topologyService)
        {
            return new AffinityApplyService(
                processService.Object,
                topologyService.Object,
                NullLogger<AffinityApplyService>.Instance);
        }

        private static CpuSelectionAffinityApplier CreateCpuSelectionApplier(
            FakeCpuSetHandler cpuSets,
            RecordingLegacyAffinityApplier legacy,
            RecordingAffinityAudit? audit = null) =>
            new(
                _ => cpuSets,
                legacy.ApplyAsync,
                NullLogger<CpuSelectionAffinityApplier>.Instance,
                null,
                audit is null ? null : audit.Record);

        private static Mock<IProcessService> CreateProcessService(bool processStillRunning)
        {
            var processService = new Mock<IProcessService>(MockBehavior.Strict);
            processService
                .Setup(service => service.IsProcessStillRunning(It.IsAny<ProcessModel>()))
                .ReturnsAsync(processStillRunning);
            return processService;
        }

        private static CpuSelection CreateSelection(params ProcessorRef[] processors) =>
            new()
            {
                LogicalProcessors = processors.ToList(),
                GlobalLogicalProcessorIndexes = processors.Select(processor => processor.GlobalIndex).ToList(),
            };

        private sealed class RecordingLegacyAffinityApplier
        {
            public int CallCount { get; private set; }

            public long? LastMask { get; private set; }

            public Exception? ExceptionToThrow { get; init; }

            public Task<long> ApplyAsync(ProcessModel process, long affinityMask)
            {
                this.CallCount++;
                this.LastMask = affinityMask;

                if (this.ExceptionToThrow != null)
                {
                    throw this.ExceptionToThrow;
                }

                process.ProcessorAffinity = affinityMask;
                return Task.FromResult(affinityMask);
            }
        }

        private sealed class RecordingAffinityAudit
        {
            public List<(ProcessModel Process, bool Success)> Calls { get; } = new();

            public void Record(ProcessModel process, bool success) =>
                this.Calls.Add((process, success));
        }

        private sealed class FakeCpuSetHandler : IProcessCpuSetHandler
        {
            public uint ProcessId => 42;

            public string ExecutableName => "Game";

            public bool IsValid { get; init; } = true;

            public bool ApplyCpuSelectionResult { get; init; }

            public Exception? ApplyCpuSelectionException { get; init; }

            public int ApplyCpuSelectionCalls { get; private set; }

            public CpuSelection? LastSelection { get; private set; }

            public bool ApplyCpuSetMask(long affinityMask, bool clearMask = false) => false;

            public bool ApplyCpuSelection(CpuSelection? selection, bool clearSelection = false)
            {
                this.ApplyCpuSelectionCalls++;
                this.LastSelection = selection;

                if (this.ApplyCpuSelectionException != null)
                {
                    throw this.ApplyCpuSelectionException;
                }

                return this.ApplyCpuSelectionResult;
            }

            public double GetAverageCpuUsage() => 0;

            public void Dispose()
            {
            }
        }
    }
}
