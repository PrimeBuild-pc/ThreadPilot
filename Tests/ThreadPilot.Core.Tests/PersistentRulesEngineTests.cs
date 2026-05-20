/*
 * ThreadPilot - persistent rules engine tests.
 */
namespace ThreadPilot.Core.Tests
{
    using System.Diagnostics;
    using Moq;
    using ThreadPilot.Models;
    using ThreadPilot.Services;

    public sealed class PersistentRulesEngineTests
    {
        [Fact]
        public async Task ApplyMatchingRulesAsync_WithCpuSelection_AppliesCpuSelection()
        {
            var selection = CreateCpuSelection();
            var rule = CreateRule(cpuSelection: selection, applyAffinity: true);
            var affinity = CreateAffinityService();
            var processService = CreateProcessService();
            var engine = CreateEngine([rule], affinity.Object, processService.Object, CreateMemoryPriorityService().Object);
            var process = CreateProcess();

            var results = await engine.ApplyMatchingRulesAsync(process);

            Assert.Single(results);
            Assert.True(results[0].Success);
            Assert.True(results[0].AffinityApplied);
            affinity.Verify(s => s.ApplyAsync(process, selection), Times.Once);
            affinity.Verify(s => s.ApplyAsync(It.IsAny<ProcessModel>(), It.IsAny<long>()), Times.Never);
        }

        [Fact]
        public async Task ApplyMatchingRulesAsync_WithLegacyAffinityMask_AppliesLegacyAffinity()
        {
            var rule = CreateRule(legacyAffinityMask: 3, applyAffinity: true);
            var affinity = CreateAffinityService();
            var processService = CreateProcessService();
            var engine = CreateEngine([rule], affinity.Object, processService.Object, CreateMemoryPriorityService().Object);
            var process = CreateProcess();

            var results = await engine.ApplyMatchingRulesAsync(process);

            Assert.Single(results);
            Assert.True(results[0].Success);
            Assert.True(results[0].AffinityApplied);
            affinity.Verify(s => s.ApplyAsync(process, 3), Times.Once);
            affinity.Verify(s => s.ApplyAsync(It.IsAny<ProcessModel>(), It.IsAny<CpuSelection>()), Times.Never);
        }

        [Fact]
        public async Task ApplyMatchingRulesAsync_WithPriority_AppliesPriority()
        {
            var rule = CreateRule(priority: ProcessPriorityClass.High, applyPriority: true);
            var affinity = CreateAffinityService();
            var processService = CreateProcessService();
            var memoryPriorityService = CreateMemoryPriorityService();
            var engine = CreateEngine([rule], affinity.Object, processService.Object, memoryPriorityService.Object);
            var process = CreateProcess();

            var results = await engine.ApplyMatchingRulesAsync(process);

            Assert.Single(results);
            Assert.True(results[0].Success);
            Assert.True(results[0].PriorityApplied);
            processService.Verify(s => s.SetProcessPriority(process, ProcessPriorityClass.High), Times.Once);
        }

        [Fact]
        public async Task ApplyMatchingRulesAsync_WithMemoryPriority_AppliesMemoryPriority()
        {
            var rule = CreateRule(memoryPriority: ProcessMemoryPriority.Low, applyMemoryPriority: true);
            var affinity = CreateAffinityService();
            var processService = CreateProcessService();
            var memoryPriorityService = CreateMemoryPriorityService();
            var engine = CreateEngine([rule], affinity.Object, processService.Object, memoryPriorityService.Object);
            var process = CreateProcess();

            var results = await engine.ApplyMatchingRulesAsync(process);

            Assert.Single(results);
            Assert.True(results[0].Success);
            Assert.True(results[0].MemoryPriorityApplied);
            memoryPriorityService.Verify(
                s => s.SetMemoryPriorityAsync(process, ProcessMemoryPriority.Low),
                Times.Once);
        }

        [Fact]
        public async Task ApplyMatchingRulesAsync_WithRealtimePriority_ReturnsControlledFailure()
        {
            var rule = CreateRule(priority: ProcessPriorityClass.RealTime, applyPriority: true);
            var affinity = CreateAffinityService();
            var processService = CreateProcessService();
            processService
                .Setup(s => s.SetProcessPriority(It.IsAny<ProcessModel>(), ProcessPriorityClass.RealTime))
                .ThrowsAsync(new InvalidOperationException(ProcessOperationUserMessages.RealtimePriorityBlocked));
            var engine = CreateEngine([rule], affinity.Object, processService.Object, CreateMemoryPriorityService().Object);

            var results = await engine.ApplyMatchingRulesAsync(CreateProcess());

            var result = Assert.Single(results);
            Assert.False(result.Success);
            Assert.False(result.PriorityApplied);
            Assert.Equal(ProcessOperationUserMessages.RealtimePriorityBlocked, result.UserMessage);
        }

        [Fact]
        public async Task ApplyMatchingRulesAsync_WithAccessDeniedAffinity_ReturnsAccessDeniedResult()
        {
            var rule = CreateRule(legacyAffinityMask: 3, applyAffinity: true);
            var affinity = CreateAffinityService(AffinityApplyResult.Failed(
                AffinityApplyErrorCodes.AccessDenied,
                ProcessOperationUserMessages.AccessDenied,
                "Access is denied.",
                isAccessDenied: true));
            var engine = CreateEngine([rule], affinity.Object, CreateProcessService().Object, CreateMemoryPriorityService().Object);

            var results = await engine.ApplyMatchingRulesAsync(CreateProcess());

            var result = Assert.Single(results);
            Assert.False(result.Success);
            Assert.True(result.IsAccessDenied);
            Assert.Equal(ProcessOperationUserMessages.AccessDenied, result.UserMessage);
        }

        [Fact]
        public async Task ApplyMatchingRulesAsync_WithAntiCheatAffinity_ReturnsSafeProtectedResult()
        {
            var rule = CreateRule(legacyAffinityMask: 3, applyAffinity: true);
            var affinity = CreateAffinityService(AffinityApplyResult.Failed(
                AffinityApplyErrorCodes.AntiCheatOrProtectedProcessLikely,
                ProcessOperationUserMessages.AntiCheatProtectedLikely,
                "Protected process.",
                isAccessDenied: true,
                isAntiCheatLikely: true));
            var engine = CreateEngine([rule], affinity.Object, CreateProcessService().Object, CreateMemoryPriorityService().Object);

            var results = await engine.ApplyMatchingRulesAsync(CreateProcess());

            var result = Assert.Single(results);
            Assert.False(result.Success);
            Assert.True(result.IsAntiCheatLikely);
            Assert.DoesNotContain("bypass", result.UserMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ApplyMatchingRulesAsync_WithProcessExitedAffinity_ReturnsProcessExitedResult()
        {
            var rule = CreateRule(legacyAffinityMask: 3, applyAffinity: true);
            var affinity = CreateAffinityService(AffinityApplyResult.Failed(
                AffinityApplyErrorCodes.ProcessExited,
                ProcessOperationUserMessages.ProcessExited,
                "Process exited.",
                failureReason: AffinityApplyFailureReason.ProcessTerminated));
            var engine = CreateEngine([rule], affinity.Object, CreateProcessService().Object, CreateMemoryPriorityService().Object);

            var results = await engine.ApplyMatchingRulesAsync(CreateProcess());

            var result = Assert.Single(results);
            Assert.False(result.Success);
            Assert.True(result.IsProcessExited);
            Assert.Equal(ProcessOperationUserMessages.ProcessExited, result.UserMessage);
        }

        [Fact]
        public async Task ApplyMatchingRulesAsync_WithDisabledRule_DoesNotApply()
        {
            var rule = CreateRule(legacyAffinityMask: 3, applyAffinity: true) with { IsEnabled = false };
            var affinity = CreateAffinityService();
            var processService = CreateProcessService();
            var memoryPriorityService = CreateMemoryPriorityService();
            var engine = CreateEngine([rule], affinity.Object, processService.Object, memoryPriorityService.Object);

            var results = await engine.ApplyMatchingRulesAsync(CreateProcess());

            Assert.Empty(results);
            affinity.Verify(s => s.ApplyAsync(It.IsAny<ProcessModel>(), It.IsAny<long>()), Times.Never);
            processService.Verify(s => s.SetProcessPriority(It.IsAny<ProcessModel>(), It.IsAny<ProcessPriorityClass>()), Times.Never);
            memoryPriorityService.Verify(
                s => s.SetMemoryPriorityAsync(It.IsAny<ProcessModel>(), It.IsAny<ProcessMemoryPriority>()),
                Times.Never);
        }

        [Fact]
        public async Task ApplyMatchingRulesAsync_WithAffinityEnabledButNoAffinityPayload_ReturnsFailure()
        {
            var rule = CreateRule(applyAffinity: true);
            var affinity = CreateAffinityService();
            var processService = CreateProcessService();
            var engine = CreateEngine([rule], affinity.Object, processService.Object, CreateMemoryPriorityService().Object);

            var results = await engine.ApplyMatchingRulesAsync(CreateProcess());

            var result = Assert.Single(results);
            Assert.False(result.Success);
            Assert.False(result.AffinityApplied);
            Assert.False(result.PriorityApplied);
            Assert.False(result.MemoryPriorityApplied);
            Assert.Equal("PersistentRuleMissingAffinity", result.ErrorCode);
            Assert.Equal("This saved rule has no affinity selection to apply.", result.UserMessage);
            affinity.Verify(s => s.ApplyAsync(It.IsAny<ProcessModel>(), It.IsAny<long>()), Times.Never);
            affinity.Verify(s => s.ApplyAsync(It.IsAny<ProcessModel>(), It.IsAny<CpuSelection>()), Times.Never);
            processService.Verify(s => s.SetProcessPriority(It.IsAny<ProcessModel>(), It.IsAny<ProcessPriorityClass>()), Times.Never);
        }

        [Fact]
        public async Task ApplyMatchingRulesAsync_WithPriorityEnabledButNoPriorityPayload_ReturnsFailure()
        {
            var rule = CreateRule(applyPriority: true);
            var affinity = CreateAffinityService();
            var processService = CreateProcessService();
            var engine = CreateEngine([rule], affinity.Object, processService.Object, CreateMemoryPriorityService().Object);

            var results = await engine.ApplyMatchingRulesAsync(CreateProcess());

            var result = Assert.Single(results);
            Assert.False(result.Success);
            Assert.False(result.AffinityApplied);
            Assert.False(result.PriorityApplied);
            Assert.False(result.MemoryPriorityApplied);
            Assert.Equal("PersistentRuleMissingPriority", result.ErrorCode);
            Assert.Equal("This saved rule has no priority value to apply.", result.UserMessage);
            affinity.Verify(s => s.ApplyAsync(It.IsAny<ProcessModel>(), It.IsAny<long>()), Times.Never);
            affinity.Verify(s => s.ApplyAsync(It.IsAny<ProcessModel>(), It.IsAny<CpuSelection>()), Times.Never);
            processService.Verify(s => s.SetProcessPriority(It.IsAny<ProcessModel>(), It.IsAny<ProcessPriorityClass>()), Times.Never);
        }

        [Fact]
        public async Task ApplyMatchingRulesAsync_WithMemoryPriorityEnabledButNoMemoryPriorityPayload_ReturnsFailure()
        {
            var rule = CreateRule(applyMemoryPriority: true);
            var affinity = CreateAffinityService();
            var processService = CreateProcessService();
            var memoryPriorityService = CreateMemoryPriorityService();
            var engine = CreateEngine([rule], affinity.Object, processService.Object, memoryPriorityService.Object);

            var results = await engine.ApplyMatchingRulesAsync(CreateProcess());

            var result = Assert.Single(results);
            Assert.False(result.Success);
            Assert.False(result.AffinityApplied);
            Assert.False(result.PriorityApplied);
            Assert.False(result.MemoryPriorityApplied);
            Assert.Equal("PersistentRuleMissingMemoryPriority", result.ErrorCode);
            Assert.Equal("This saved rule has no memory priority value to apply.", result.UserMessage);
            memoryPriorityService.Verify(
                s => s.SetMemoryPriorityAsync(It.IsAny<ProcessModel>(), It.IsAny<ProcessMemoryPriority>()),
                Times.Never);
        }

        [Fact]
        public async Task ApplyMatchingRulesAsync_WithAffinityPriorityAndMemoryPriority_AppliesAllFlags()
        {
            var rule = CreateRule(
                legacyAffinityMask: 3,
                priority: ProcessPriorityClass.AboveNormal,
                memoryPriority: ProcessMemoryPriority.BelowNormal,
                applyAffinity: true,
                applyPriority: true,
                applyMemoryPriority: true);
            var affinity = CreateAffinityService();
            var processService = CreateProcessService();
            var memoryPriorityService = CreateMemoryPriorityService();
            var engine = CreateEngine([rule], affinity.Object, processService.Object, memoryPriorityService.Object);

            var result = Assert.Single(await engine.ApplyMatchingRulesAsync(CreateProcess()));

            Assert.True(result.Success);
            Assert.True(result.AffinityApplied);
            Assert.True(result.PriorityApplied);
            Assert.True(result.MemoryPriorityApplied);
        }

        [Fact]
        public async Task ApplyMatchingRulesAsync_WithMemoryPriorityAccessDenied_PropagatesAccessDeniedResult()
        {
            var rule = CreateRule(memoryPriority: ProcessMemoryPriority.Low, applyMemoryPriority: true);
            var memoryPriorityService = CreateMemoryPriorityService(ProcessOperationResult.Failed(
                AffinityApplyErrorCodes.AccessDenied,
                ProcessOperationUserMessages.AccessDenied,
                "Access is denied.",
                isAccessDenied: true));
            var engine = CreateEngine(
                [rule],
                CreateAffinityService().Object,
                CreateProcessService().Object,
                memoryPriorityService.Object);

            var result = Assert.Single(await engine.ApplyMatchingRulesAsync(CreateProcess()));

            Assert.False(result.Success);
            Assert.False(result.MemoryPriorityApplied);
            Assert.True(result.IsAccessDenied);
            Assert.Equal(AffinityApplyErrorCodes.AccessDenied, result.ErrorCode);
            Assert.Equal(ProcessOperationUserMessages.AccessDenied, result.UserMessage);
        }

        [Fact]
        public async Task ApplyMatchingRulesAsync_WithNoActions_ReturnsControlledFailure()
        {
            var rule = CreateRule();
            var affinity = CreateAffinityService();
            var processService = CreateProcessService();
            var engine = CreateEngine([rule], affinity.Object, processService.Object, CreateMemoryPriorityService().Object);

            var results = await engine.ApplyMatchingRulesAsync(CreateProcess());

            var result = Assert.Single(results);
            Assert.False(result.Success);
            Assert.False(result.AffinityApplied);
            Assert.False(result.PriorityApplied);
            Assert.False(result.MemoryPriorityApplied);
            Assert.Equal("PersistentRuleNoActions", result.ErrorCode);
            affinity.Verify(s => s.ApplyAsync(It.IsAny<ProcessModel>(), It.IsAny<long>()), Times.Never);
            affinity.Verify(s => s.ApplyAsync(It.IsAny<ProcessModel>(), It.IsAny<CpuSelection>()), Times.Never);
            processService.Verify(s => s.SetProcessPriority(It.IsAny<ProcessModel>(), It.IsAny<ProcessPriorityClass>()), Times.Never);
        }

        [Fact]
        public async Task ApplyMatchingRulesAsync_WithMultipleMatchingRules_ReturnsResultPerRuleWithoutRetry()
        {
            var rules = new[]
            {
                CreateRule(id: "rule-a", legacyAffinityMask: 1, applyAffinity: true),
                CreateRule(id: "rule-b", priority: ProcessPriorityClass.AboveNormal, applyPriority: true),
            };
            var affinity = CreateAffinityService();
            var processService = CreateProcessService();
            var engine = CreateEngine(rules, affinity.Object, processService.Object, CreateMemoryPriorityService().Object);

            var results = await engine.ApplyMatchingRulesAsync(CreateProcess());

            Assert.Equal(2, results.Count);
            Assert.Contains(results, result => result.RuleId == "rule-a");
            Assert.Contains(results, result => result.RuleId == "rule-b");
            affinity.Verify(s => s.ApplyAsync(It.IsAny<ProcessModel>(), It.IsAny<long>()), Times.Once);
            processService.Verify(s => s.SetProcessPriority(It.IsAny<ProcessModel>(), It.IsAny<ProcessPriorityClass>()), Times.Once);
        }

        private static PersistentRulesEngine CreateEngine(
            IReadOnlyList<PersistentProcessRule> rules,
            IAffinityApplyService affinityApplyService,
            IProcessService processService,
            IProcessMemoryPriorityService memoryPriorityService) =>
            new(
                new FakePersistentProcessRuleStore(rules),
                new PersistentProcessRuleMatcher(),
                affinityApplyService,
                processService,
                memoryPriorityService,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<PersistentRulesEngine>.Instance);

        private static Mock<IAffinityApplyService> CreateAffinityService(AffinityApplyResult? result = null)
        {
            var mock = new Mock<IAffinityApplyService>(MockBehavior.Strict);
            var resolved = result ?? AffinityApplyResult.Succeeded(1, 1);
            mock
                .Setup(s => s.ApplyAsync(It.IsAny<ProcessModel>(), It.IsAny<long>()))
                .ReturnsAsync(resolved);
            mock
                .Setup(s => s.ApplyAsync(It.IsAny<ProcessModel>(), It.IsAny<CpuSelection>()))
                .ReturnsAsync(resolved);
            return mock;
        }

        private static Mock<IProcessService> CreateProcessService()
        {
            var mock = new Mock<IProcessService>(MockBehavior.Strict);
            mock
                .Setup(s => s.SetProcessPriority(It.IsAny<ProcessModel>(), It.IsAny<ProcessPriorityClass>()))
                .Returns(Task.CompletedTask);
            return mock;
        }

        private static Mock<IProcessMemoryPriorityService> CreateMemoryPriorityService(ProcessOperationResult? result = null)
        {
            var mock = new Mock<IProcessMemoryPriorityService>(MockBehavior.Strict);
            mock
                .Setup(s => s.SetMemoryPriorityAsync(It.IsAny<ProcessModel>(), It.IsAny<ProcessMemoryPriority>()))
                .ReturnsAsync(result ?? ProcessOperationResult.Succeeded(
                    "Memory priority applied.",
                    "Memory priority applied in test."));
            return mock;
        }

        private static PersistentProcessRule CreateRule(
            string id = "rule",
            CpuSelection? cpuSelection = null,
            long? legacyAffinityMask = null,
            ProcessPriorityClass? priority = null,
            ProcessMemoryPriority? memoryPriority = null,
            bool applyAffinity = false,
            bool applyPriority = false,
            bool applyMemoryPriority = false) =>
            new()
            {
                Id = id,
                Name = id,
                IsEnabled = true,
                ProcessName = "game.exe",
                CpuSelection = cpuSelection,
                LegacyAffinityMask = legacyAffinityMask,
                Priority = priority,
                MemoryPriority = memoryPriority,
                ApplyAffinityOnStart = applyAffinity,
                ApplyPriorityOnStart = applyPriority,
                ApplyMemoryPriorityOnStart = applyMemoryPriority,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

        private static CpuSelection CreateCpuSelection() =>
            new()
            {
                LogicalProcessors = [new ProcessorRef(0, 0, 0)],
                GlobalLogicalProcessorIndexes = [0],
            };

        private static ProcessModel CreateProcess() =>
            new()
            {
                ProcessId = 42,
                Name = "game.exe",
                ExecutablePath = @"C:\Games\Game.exe",
                Priority = ProcessPriorityClass.Normal,
            };

        private sealed class FakePersistentProcessRuleStore(IReadOnlyList<PersistentProcessRule> rules)
            : IPersistentProcessRuleStore
        {
            public Task<IReadOnlyList<PersistentProcessRule>> LoadAsync() =>
                Task.FromResult(rules);

            public Task SaveAsync(IReadOnlyList<PersistentProcessRule> rules) =>
                Task.CompletedTask;
        }
    }
}
