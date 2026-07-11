namespace ThreadPilot.Core.Tests
{
    using System.Diagnostics;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using ThreadPilot.Models;
    using ThreadPilot.Services;
    using Xunit;

    public sealed class PersistentRulePriorityVerificationServiceTests
    {
        [Fact]
        public async Task DelayedVerification_PriorityRemainsHigh_RecordsVerifiedState()
        {
            var audit = new ActivityAuditService(NullLogger<ActivityAuditService>.Instance);
            var processService = CreateProcessService(ProcessPriorityClass.High, ProcessPriorityClass.High);
            var service = CreateService(processService.Object, audit);

            await service.VerifyDelayedAsync(CreateVerifiedResult(), CreateProcess(), "ProcessStarted");

            var entries = await audit.GetEntriesAsync();
            Assert.Equal(2, entries.Count);
            Assert.All(entries, entry => Assert.Contains("Verified saved priority", entry.Message));
            processService.Verify(x => x.RefreshProcessInfo(It.IsAny<ProcessModel>()), Times.Exactly(2));
        }

        [Fact]
        public async Task DelayedVerification_PriorityRevertsToNormal_RecordsReversion()
        {
            var audit = new ActivityAuditService(NullLogger<ActivityAuditService>.Instance);
            var processService = CreateProcessService(ProcessPriorityClass.Normal);
            var service = CreateService(processService.Object, audit);

            await service.VerifyDelayedAsync(CreateVerifiedResult(), CreateProcess(), "ProcessStarted");

            var entry = Assert.Single(await audit.GetEntriesAsync());
            Assert.Contains("reverted", entry.Message, StringComparison.OrdinalIgnoreCase);
            processService.Verify(x => x.RefreshProcessInfo(It.IsAny<ProcessModel>()), Times.Once);
        }

        [Fact]
        public async Task DelayedVerification_DoesNotLoopIndefinitely()
        {
            var processService = CreateProcessService(ProcessPriorityClass.High, ProcessPriorityClass.High, ProcessPriorityClass.High);
            var service = CreateService(processService.Object);

            await service.VerifyDelayedAsync(CreateVerifiedResult(), CreateProcess(), "ProcessStarted");

            processService.Verify(x => x.RefreshProcessInfo(It.IsAny<ProcessModel>()), Times.Exactly(2));
        }

        [Fact]
        public async Task PriorityRevertedAfterApply_SchedulesSingleRetry()
        {
            var audit = new ActivityAuditService(NullLogger<ActivityAuditService>.Instance);
            var processService = CreateRetryProcessService(ProcessPriorityClass.Normal, ProcessPriorityClass.Normal, ProcessPriorityClass.High, ProcessPriorityClass.High);
            var service = CreateRetryService(processService.Object, audit);

            await service.VerifyDelayedAsync(CreateVerifiedResult(), CreateProcess(), "ProcessStarted");
            await WaitForAuditAsync(audit, "PersistentRulePriorityRetryVerified");
            await service.VerifyDelayedAsync(CreateVerifiedResult(), CreateProcess(), "ProcessStarted");
            await Task.Delay(20);

            var entries = await audit.GetEntriesAsync();
            Assert.Single(entries, entry => entry.Details?.Contains("PersistentRulePriorityRetryScheduled") == true);
            processService.Verify(x => x.SetProcessPriority(It.IsAny<ProcessModel>(), ProcessPriorityClass.High, ProcessPriorityWriteSource.PersistentRuleRetry), Times.Once);
        }

        [Fact]
        public async Task PriorityRetry_WhenObservedPriorityMatchesRequested_RecordsVerifiedSuccess()
        {
            var audit = new ActivityAuditService(NullLogger<ActivityAuditService>.Instance);
            var processService = CreateRetryProcessService(ProcessPriorityClass.Normal, ProcessPriorityClass.Normal, ProcessPriorityClass.High, ProcessPriorityClass.High);
            var service = CreateRetryService(processService.Object, audit);

            await service.VerifyDelayedAsync(CreateVerifiedResult(), CreateProcess(), "ProcessStarted");
            var entries = await WaitForAuditAsync(audit, "PersistentRulePriorityRetryVerified");

            Assert.Contains(entries, entry => entry.Details?.Contains("PersistentRulePriorityRetryVerified") == true && entry.Details.Contains("Phase: retry-immediate"));
        }

        [Fact]
        public async Task PriorityRetry_WhenPriorityRevertsAgain_DoesNotRetryAgain()
        {
            var audit = new ActivityAuditService(NullLogger<ActivityAuditService>.Instance);
            var processService = CreateRetryProcessService(ProcessPriorityClass.Normal, ProcessPriorityClass.Normal, ProcessPriorityClass.High, ProcessPriorityClass.Normal);
            var service = CreateRetryService(processService.Object, audit);

            await service.VerifyDelayedAsync(CreateVerifiedResult(), CreateProcess(), "ProcessStarted");
            var entries = await WaitForAuditAsync(audit, "PersistentRulePriorityRetryReverted");

            Assert.Single(entries, entry => entry.Details?.Contains("PersistentRulePriorityRetryScheduled") == true);
            processService.Verify(x => x.SetProcessPriority(It.IsAny<ProcessModel>(), ProcessPriorityClass.High, ProcessPriorityWriteSource.PersistentRuleRetry), Times.Once);
        }

        [Fact]
        public async Task PriorityRetry_DoesNotRunForExitedProcess()
        {
            var audit = new ActivityAuditService(NullLogger<ActivityAuditService>.Instance);
            var processService = CreateRetryProcessService(ProcessPriorityClass.Normal);
            processService.Setup(x => x.IsProcessStillRunning(It.IsAny<ProcessModel>())).ReturnsAsync(false);
            var service = CreateRetryService(processService.Object, audit);

            await service.VerifyDelayedAsync(CreateVerifiedResult(), CreateProcess(), "ProcessStarted");
            var entries = await WaitForAuditAsync(audit, "PersistentRulePriorityRetrySkipped");

            Assert.Contains(entries, entry => entry.Details?.Contains("process-exited") == true);
            processService.Verify(x => x.SetProcessPriority(It.IsAny<ProcessModel>(), It.IsAny<ProcessPriorityClass>(), It.IsAny<ProcessPriorityWriteSource>()), Times.Never);
        }

        [Fact]
        public async Task PriorityRetry_DoesNotRunForAccessDeniedOrProtectedProcess()
        {
            var audit = new ActivityAuditService(NullLogger<ActivityAuditService>.Instance);
            var processService = CreateRetryProcessService(ProcessPriorityClass.Normal);
            var service = CreateRetryService(processService.Object, audit);
            var process = CreateProcess();
            process.Classification = ProcessClassification.ProtectedOrAccessDenied;

            await service.VerifyDelayedAsync(CreateVerifiedResult(), process, "ProcessStarted");
            var entries = await WaitForAuditAsync(audit, "PersistentRulePriorityRetrySkipped");

            Assert.Contains(entries, entry => entry.Details?.Contains("protected-or-access-denied") == true);
            processService.Verify(x => x.SetProcessPriority(It.IsAny<ProcessModel>(), It.IsAny<ProcessPriorityClass>(), It.IsAny<ProcessPriorityWriteSource>()), Times.Never);
        }

        [Fact]
        public async Task PriorityRetry_IsCancelledWhenProcessExits()
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var audit = new ActivityAuditService(NullLogger<ActivityAuditService>.Instance);
            var processService = CreateRetryProcessService(ProcessPriorityClass.Normal);
            var delays = 0;
            var service = CreateRetryService(processService.Object, audit, (_, token) => ++delays == 1 ? Task.CompletedTask : tcs.Task.WaitAsync(token));

            await service.VerifyDelayedAsync(CreateVerifiedResult(), CreateProcess(), "ProcessStarted");
            service.MarkProcessExited(42);
            tcs.SetResult();
            await Task.Delay(20);

            processService.Verify(x => x.SetProcessPriority(It.IsAny<ProcessModel>(), It.IsAny<ProcessPriorityClass>(), It.IsAny<ProcessPriorityWriteSource>()), Times.Never);
        }

        [Fact]
        public async Task PriorityRetry_IsScopedToPidAndRuleSignature()
        {
            var audit = new ActivityAuditService(NullLogger<ActivityAuditService>.Instance);
            var processService = CreateRetryProcessService(ProcessPriorityClass.Normal, ProcessPriorityClass.Normal, ProcessPriorityClass.High, ProcessPriorityClass.High, ProcessPriorityClass.Normal, ProcessPriorityClass.Normal, ProcessPriorityClass.High, ProcessPriorityClass.High);
            var service = CreateRetryService(processService.Object, audit);

            await service.VerifyDelayedAsync(CreateVerifiedResult() with { ProcessId = 42 }, CreateProcess(42), "ProcessStarted");
            await WaitForAuditAsync(audit, "PersistentRulePriorityRetryVerified");
            await service.VerifyDelayedAsync(CreateVerifiedResult() with { ProcessId = 43 }, CreateProcess(43), "ProcessStarted");
            await WaitForCountAsync(audit, entry => entry.Details?.Contains("PersistentRulePriorityRetryScheduled") == true, 2);

            processService.Verify(x => x.SetProcessPriority(It.IsAny<ProcessModel>(), ProcessPriorityClass.High, ProcessPriorityWriteSource.PersistentRuleRetry), Times.Exactly(2));
        }

        [Fact]
        public void PriorityRetry_DoesNotBlockProcessStartHandler()
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var processService = CreateRetryProcessService(ProcessPriorityClass.Normal);
            var delays = 0;
            var service = CreateRetryService(processService.Object, delayAsync: (_, token) => ++delays == 1 ? Task.CompletedTask : tcs.Task.WaitAsync(token));

            service.ScheduleDelayedVerification(CreateVerifiedResult(), CreateProcess(), "ProcessStarted");

            processService.Verify(x => x.SetProcessPriority(It.IsAny<ProcessModel>(), It.IsAny<ProcessPriorityClass>(), It.IsAny<ProcessPriorityWriteSource>()), Times.Never);
            service.MarkProcessExited(42);
            tcs.SetResult();
        }

        private static PersistentRulePriorityVerificationService CreateService(
            IProcessService processService,
            IActivityAuditService? audit = null) =>
            new(
                processService,
                NullLogger<PersistentRulePriorityVerificationService>.Instance,
                audit,
                [TimeSpan.Zero, TimeSpan.Zero],
                (_, _) => Task.CompletedTask);

        private static PersistentRulePriorityVerificationService CreateRetryService(
            IProcessService processService,
            IActivityAuditService? audit = null,
            Func<TimeSpan, CancellationToken, Task>? delayAsync = null,
            IReadOnlyList<PersistentProcessRule>? rules = null) =>
            new(
                processService,
                NullLogger<PersistentRulePriorityVerificationService>.Instance,
                audit,
                new FakePersistentProcessRuleStore(rules ?? [CreateRule()]),
                new PersistentProcessRuleMatcher(),
                [TimeSpan.Zero, TimeSpan.Zero],
                TimeSpan.Zero,
                delayAsync ?? ((_, _) => Task.CompletedTask));

        private static Mock<IProcessService> CreateProcessService(params ProcessPriorityClass[] observedPriorities)
        {
            var calls = 0;
            var mock = new Mock<IProcessService>(MockBehavior.Strict);
            mock
                .Setup(x => x.RefreshProcessInfo(It.IsAny<ProcessModel>()))
                .Returns<ProcessModel>(process =>
                {
                    process.Priority = observedPriorities[Math.Min(calls, observedPriorities.Length - 1)];
                    calls++;
                    return Task.CompletedTask;
                });
            return mock;
        }

        private static Mock<IProcessService> CreateRetryProcessService(params ProcessPriorityClass[] observedPriorities)
        {
            var mock = CreateProcessService(observedPriorities);
            mock.Setup(x => x.IsProcessStillRunning(It.IsAny<ProcessModel>())).ReturnsAsync(true);
            mock
                .Setup(x => x.SetProcessPriority(It.IsAny<ProcessModel>(), It.IsAny<ProcessPriorityClass>(), It.IsAny<ProcessPriorityWriteSource>()))
                .Returns<ProcessModel, ProcessPriorityClass, ProcessPriorityWriteSource>((process, priority, _) =>
                {
                    process.Priority = priority;
                    return Task.CompletedTask;
                });
            return mock;
        }

        private static async Task<IReadOnlyList<ActivityAuditEntry>> WaitForAuditAsync(ActivityAuditService audit, string action) =>
            await WaitForCountAsync(audit, entry => entry.Details?.Contains(action) == true, 1);

        private static async Task<IReadOnlyList<ActivityAuditEntry>> WaitForCountAsync(ActivityAuditService audit, Predicate<ActivityAuditEntry> predicate, int count)
        {
            for (var i = 0; i < 50; i++)
            {
                var entries = await audit.GetEntriesAsync();
                if (entries.Count(entry => predicate(entry)) >= count)
                {
                    return entries;
                }

                await Task.Delay(10);
            }

            return await audit.GetEntriesAsync();
        }

        private static PersistentRuleAutoApplyResult CreateVerifiedResult() =>
            new()
            {
                Success = true,
                RuleId = "rule-high",
                ProcessId = 42,
                ProcessName = "game.exe",
                RequestedPriority = ProcessPriorityClass.High,
                ObservedPriority = ProcessPriorityClass.High,
                PriorityVerified = true,
                PriorityVerificationPhase = "immediate",
            };

        private static PersistentProcessRule CreateRule(string id = "rule-high") =>
            new()
            {
                Id = id,
                Name = id,
                IsEnabled = true,
                ProcessName = "game.exe",
                Priority = ProcessPriorityClass.High,
                ApplyPriorityOnStart = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

        private static ProcessModel CreateProcess(int processId = 42) =>
            new()
            {
                ProcessId = processId,
                Name = "game.exe",
                ExecutablePath = @"C:\Games\game.exe",
                Priority = ProcessPriorityClass.High,
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
