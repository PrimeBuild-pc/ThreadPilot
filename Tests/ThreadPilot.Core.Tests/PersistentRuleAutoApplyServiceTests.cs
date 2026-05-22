/*
 * ThreadPilot - persistent rule auto-apply coordinator tests.
 */
namespace ThreadPilot.Core.Tests
{
    using System.Diagnostics;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using ThreadPilot.Models;
    using ThreadPilot.Services;

    public sealed class PersistentRuleAutoApplyServiceTests
    {
        [Fact]
        public async Task ApplyForProcessStartAsync_WhenMatchingEnabledRuleExists_CallsRulesEngine()
        {
            var process = CreateProcess();
            var rule = CreateRule();
            var engine = CreateEngine(rule, CreateSuccess(rule, process));
            var service = CreateService([rule], engine.Object);

            var results = await service.ApplyForProcessStartAsync(process);

            Assert.Single(results);
            engine.Verify(
                x => x.ApplyMatchingRulesAsync(
                    process,
                    It.IsAny<Predicate<PersistentProcessRule>?>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ApplyForProcessStartAsync_WhenRuleIsDisabled_DoesNotCallRulesEngine()
        {
            var process = CreateProcess();
            var rule = CreateRule() with { IsEnabled = false };
            var engine = CreateEngine(rule, CreateSuccess(rule, process));
            var service = CreateService([rule], engine.Object);

            var results = await service.ApplyForProcessStartAsync(process);

            Assert.Empty(results);
            engine.Verify(
                x => x.ApplyMatchingRulesAsync(
                    It.IsAny<ProcessModel>(),
                    It.IsAny<Predicate<PersistentProcessRule>?>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task ApplyForProcessStartAsync_WhenNoRuleMatches_DoesNotCallRulesEngine()
        {
            var process = CreateProcess("editor.exe");
            var rule = CreateRule(processName: "game.exe");
            var engine = CreateEngine(rule, CreateSuccess(rule, process));
            var service = CreateService([rule], engine.Object);

            var results = await service.ApplyForProcessStartAsync(process);

            Assert.Empty(results);
            engine.Verify(
                x => x.ApplyMatchingRulesAsync(
                    It.IsAny<ProcessModel>(),
                    It.IsAny<Predicate<PersistentProcessRule>?>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task ApplyForProcessStartAsync_DoesNotReapplySameRuleDuringCooldown()
        {
            var now = DateTimeOffset.UtcNow;
            var process = CreateProcess();
            var rule = CreateRule();
            var engine = CreateEngine(rule, CreateSuccess(rule, process));
            var service = CreateService([rule], engine.Object, nowProvider: () => now);

            await service.ApplyForProcessStartAsync(process);
            await service.ApplyForProcessStartAsync(process);

            engine.Verify(
                x => x.ApplyMatchingRulesAsync(
                    process,
                    It.IsAny<Predicate<PersistentProcessRule>?>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ApplyForProcessStartAsync_AfterCooldown_RetriesRule()
        {
            var now = DateTimeOffset.UtcNow;
            var process = CreateProcess();
            var rule = CreateRule();
            var engine = CreateEngine(rule, CreateSuccess(rule, process));
            var service = CreateService([rule], engine.Object, nowProvider: () => now);

            await service.ApplyForProcessStartAsync(process);
            now = now.AddSeconds(31);
            await service.ApplyForProcessStartAsync(process);

            engine.Verify(
                x => x.ApplyMatchingRulesAsync(
                    process,
                    It.IsAny<Predicate<PersistentProcessRule>?>(),
                    It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }

        [Fact]
        public async Task ApplyForProcessStartAsync_AfterProcessExit_DoesNotSuppressReusedPid()
        {
            var process = CreateProcess();
            var rule = CreateRule();
            var engine = CreateEngine(rule, CreateSuccess(rule, process));
            var service = CreateService([rule], engine.Object);

            await service.ApplyForProcessStartAsync(process);
            service.MarkProcessExited(process.ProcessId);
            await service.ApplyForProcessStartAsync(process);

            engine.Verify(
                x => x.ApplyMatchingRulesAsync(
                    process,
                    It.IsAny<Predicate<PersistentProcessRule>?>(),
                    It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }

        [Fact]
        public async Task ApplyForProcessStartAsync_WithAccessDeniedFailure_ReturnsFailureWithoutThrowing()
        {
            var process = CreateProcess();
            var rule = CreateRule();
            var failure = CreateFailure(rule, process, ProcessOperationUserMessages.AccessDenied, isAccessDenied: true);
            var engine = CreateEngine(rule, failure);
            var service = CreateService([rule], engine.Object);

            var result = Assert.Single(await service.ApplyForProcessStartAsync(process));

            Assert.False(result.Success);
            Assert.True(result.IsAccessDenied);
            Assert.Equal(ProcessOperationUserMessages.AccessDenied, result.UserMessage);
        }

        [Fact]
        public async Task ApplyForProcessStartAsync_WithProcessExitedFailure_ReturnsFailureWithoutThrowing()
        {
            var process = CreateProcess();
            var rule = CreateRule();
            var failure = CreateFailure(rule, process, ProcessOperationUserMessages.ProcessExited, isProcessExited: true);
            var engine = CreateEngine(rule, failure);
            var service = CreateService([rule], engine.Object);

            var result = Assert.Single(await service.ApplyForProcessStartAsync(process));

            Assert.False(result.Success);
            Assert.True(result.IsProcessExited);
            Assert.Equal(ProcessOperationUserMessages.ProcessExited, result.UserMessage);
        }

        [Fact]
        public async Task ApplyForProcessStartAsync_WithProtectedProcessFailure_ReturnsSafeFailureWithoutThrowing()
        {
            var process = CreateProcess();
            var rule = CreateRule();
            var failure = CreateFailure(
                rule,
                process,
                ProcessOperationUserMessages.AntiCheatProtectedLikely,
                isAccessDenied: true,
                isAntiCheatLikely: true);
            var engine = CreateEngine(rule, failure);
            var service = CreateService([rule], engine.Object);

            var result = Assert.Single(await service.ApplyForProcessStartAsync(process));

            Assert.False(result.Success);
            Assert.True(result.IsAntiCheatLikely);
            Assert.DoesNotContain("bypass", result.UserMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ApplyForDiscoveredProcessesAsync_FeatureFlagDisabled_DoesNotCallRulesEngine()
        {
            var process = CreateProcess();
            var rule = CreateRule();
            var engine = CreateEngine(rule, CreateSuccess(rule, process));
            var service = CreateService(
                [rule],
                engine.Object,
                settings: new ApplicationSettingsModel { ApplyPersistentRulesOnProcessStart = false });

            var results = await service.ApplyForDiscoveredProcessesAsync([process]);

            Assert.Empty(results);
            engine.Verify(
                x => x.ApplyMatchingRulesAsync(
                    It.IsAny<ProcessModel>(),
                    It.IsAny<Predicate<PersistentProcessRule>?>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task ApplyForDiscoveredProcessesAsync_ClearsCooldownForProcessesNoLongerPresent()
        {
            var process = CreateProcess();
            var rule = CreateRule();
            var engine = CreateEngine(rule, CreateSuccess(rule, process));
            var service = CreateService([rule], engine.Object);

            await service.ApplyForDiscoveredProcessesAsync([process]);
            await service.ApplyForDiscoveredProcessesAsync([]);
            await service.ApplyForDiscoveredProcessesAsync([process]);

            engine.Verify(
                x => x.ApplyMatchingRulesAsync(
                    process,
                    It.IsAny<Predicate<PersistentProcessRule>?>(),
                    It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }

        private static PersistentRuleAutoApplyService CreateService(
            IReadOnlyList<PersistentProcessRule> rules,
            IPersistentRulesEngine engine,
            ApplicationSettingsModel? settings = null,
            Func<DateTimeOffset>? nowProvider = null) =>
            new(
                new FakePersistentProcessRuleStore(rules),
                new PersistentProcessRuleMatcher(),
                engine,
                CreateSettingsService(settings ?? new ApplicationSettingsModel()),
                NullLogger<PersistentRuleAutoApplyService>.Instance,
                nowProvider ?? (() => DateTimeOffset.UtcNow),
                TimeSpan.FromSeconds(30));

        private static Mock<IPersistentRulesEngine> CreateEngine(
            PersistentProcessRule rule,
            PersistentRuleApplyResult result)
        {
            var engine = new Mock<IPersistentRulesEngine>(MockBehavior.Strict);
            engine
                .Setup(x => x.ApplyMatchingRulesAsync(
                    It.IsAny<ProcessModel>(),
                    It.IsAny<Predicate<PersistentProcessRule>?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((ProcessModel _, Predicate<PersistentProcessRule>? predicate, CancellationToken _) =>
                    predicate == null || predicate(rule)
                        ? new[] { result }
                        : Array.Empty<PersistentRuleApplyResult>());
            return engine;
        }

        private static IApplicationSettingsService CreateSettingsService(ApplicationSettingsModel settings)
        {
            var settingsService = new Mock<IApplicationSettingsService>(MockBehavior.Loose);
            settingsService.SetupGet(x => x.Settings).Returns(settings);
            return settingsService.Object;
        }

        private static ProcessModel CreateProcess(string name = "game.exe") =>
            new()
            {
                ProcessId = 42,
                Name = name,
                ExecutablePath = @"C:\Games\Game.exe",
                Priority = ProcessPriorityClass.Normal,
            };

        private static PersistentProcessRule CreateRule(string id = "rule", string processName = "game.exe") =>
            new()
            {
                Id = id,
                Name = id,
                IsEnabled = true,
                ProcessName = processName,
                LegacyAffinityMask = 3,
                ApplyAffinityOnStart = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

        private static PersistentRuleApplyResult CreateSuccess(PersistentProcessRule rule, ProcessModel process) =>
            new()
            {
                Success = true,
                RuleId = rule.Id,
                ProcessId = process.ProcessId,
                ProcessName = process.Name,
                AffinityApplied = true,
                UserMessage = "Persistent rule applied.",
                TechnicalMessage = "ok",
            };

        private static PersistentRuleApplyResult CreateFailure(
            PersistentProcessRule rule,
            ProcessModel process,
            string userMessage,
            bool isAccessDenied = false,
            bool isAntiCheatLikely = false,
            bool isProcessExited = false) =>
            new()
            {
                Success = false,
                RuleId = rule.Id,
                ProcessId = process.ProcessId,
                ProcessName = process.Name,
                UserMessage = userMessage,
                TechnicalMessage = userMessage,
                IsAccessDenied = isAccessDenied,
                IsAntiCheatLikely = isAntiCheatLikely,
                IsProcessExited = isProcessExited,
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
