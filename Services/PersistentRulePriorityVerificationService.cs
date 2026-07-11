namespace ThreadPilot.Services
{
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using Microsoft.Extensions.Logging;
    using ThreadPilot.Models;

    public interface IPersistentRulePriorityVerificationService
    {
        void ScheduleDelayedVerification(PersistentRuleAutoApplyResult result, ProcessModel process, string source);

        void MarkProcessExited(int processId);
    }

    public sealed class PersistentRulePriorityVerificationService : IPersistentRulePriorityVerificationService
    {
        private static readonly TimeSpan[] DefaultDelays = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3)];
        private static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromSeconds(4); // ponytail: one conservative startup-settling retry; no polling.

        private readonly IProcessService processService;
        private readonly ILogger<PersistentRulePriorityVerificationService> logger;
        private readonly IActivityAuditService? activityAuditService;
        private readonly IPersistentProcessRuleStore? ruleStore;
        private readonly IPersistentProcessRuleMatcher? matcher;
        private readonly IReadOnlyList<TimeSpan> delays;
        private readonly TimeSpan retryDelay;
        private readonly Func<TimeSpan, CancellationToken, Task> delayAsync;
        private readonly ConcurrentDictionary<RetryKey, byte> retryUsed = new();
        private readonly ConcurrentDictionary<RetryKey, CancellationTokenSource> pendingRetries = new();

        public PersistentRulePriorityVerificationService(
            IProcessService processService,
            ILogger<PersistentRulePriorityVerificationService> logger,
            IActivityAuditService? activityAuditService = null,
            IPersistentProcessRuleStore? ruleStore = null,
            IPersistentProcessRuleMatcher? matcher = null)
            : this(processService, logger, activityAuditService, ruleStore, matcher, DefaultDelays, DefaultRetryDelay, Task.Delay)
        {
        }

        public PersistentRulePriorityVerificationService(
            IProcessService processService,
            ILogger<PersistentRulePriorityVerificationService> logger,
            IActivityAuditService? activityAuditService,
            IReadOnlyList<TimeSpan> delays,
            Func<TimeSpan, CancellationToken, Task> delayAsync)
            : this(processService, logger, activityAuditService, null, null, delays, DefaultRetryDelay, delayAsync)
        {
        }

        public PersistentRulePriorityVerificationService(
            IProcessService processService,
            ILogger<PersistentRulePriorityVerificationService> logger,
            IActivityAuditService? activityAuditService,
            IPersistentProcessRuleStore? ruleStore,
            IPersistentProcessRuleMatcher? matcher,
            IReadOnlyList<TimeSpan> delays,
            TimeSpan retryDelay,
            Func<TimeSpan, CancellationToken, Task> delayAsync)
        {
            this.processService = processService ?? throw new ArgumentNullException(nameof(processService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.activityAuditService = activityAuditService;
            this.ruleStore = ruleStore;
            this.matcher = matcher;
            this.delays = delays?.Take(2).ToArray() ?? DefaultDelays;
            this.retryDelay = retryDelay < TimeSpan.Zero ? DefaultRetryDelay : retryDelay;
            this.delayAsync = delayAsync ?? Task.Delay;
        }

        public void ScheduleDelayedVerification(PersistentRuleAutoApplyResult result, ProcessModel process, string source)
        {
            if (!ShouldVerify(result))
            {
                return;
            }

            TaskSafety.FireAndForget(
                this.VerifyDelayedAsync(result, process, source, CancellationToken.None),
                ex => this.logger.LogDebug(ex, "Persistent rule delayed priority verification failed"));
        }

        public void MarkProcessExited(int processId)
        {
            foreach (var key in this.pendingRetries.Keys.Where(key => key.ProcessId == processId))
            {
                if (this.pendingRetries.TryRemove(key, out var cts))
                {
                    cts.Cancel();
                    cts.Dispose();
                }
            }

            foreach (var key in this.retryUsed.Keys.Where(key => key.ProcessId == processId))
            {
                this.retryUsed.TryRemove(key, out _);
            }
        }

        public async Task VerifyDelayedAsync(
            PersistentRuleAutoApplyResult result,
            ProcessModel process,
            string source,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(process);

            if (!ShouldVerify(result))
            {
                return;
            }

            var requested = result.RequestedPriority!.Value;
            for (var i = 0; i < this.delays.Count; i++)
            {
                var phase = i == 0 ? "delayed" : $"delayed-{i + 1}";
                try
                {
                    await this.delayAsync(this.delays[i], cancellationToken).ConfigureAwait(false);
                    await this.processService.RefreshProcessInfo(process).ConfigureAwait(false);
                    var observed = process.Priority;
                    if (observed == requested)
                    {
                        await this.LogVerifiedAsync(result, process, source, phase, requested, observed, attempt: 0).ConfigureAwait(false);
                        continue;
                    }

                    await this.LogRevertedAsync(result, process, source, phase, requested, observed).ConfigureAwait(false);
                    await this.ScheduleRetryAsync(result, process, source, requested, observed).ConfigureAwait(false);
                    return;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    var isExited = AffinityApplyExceptionClassifier.IsProcessExited(ex);
                    this.logger.Log(
                        isExited ? LogLevel.Debug : LogLevel.Warning,
                        ex,
                        "Persistent rule delayed priority verification unavailable for rule {RuleId} on process {ProcessName} (PID: {ProcessId}). Source: {Source}; requested: {RequestedPriority}; phase: {Phase}; path: {ExecutablePath}",
                        result.RuleId,
                        result.ProcessName,
                        result.ProcessId,
                        source,
                        requested,
                        phase,
                        process.ExecutablePath);
                    await this.LogActivityAsync(
                        "PersistentRulePriorityVerificationUnavailable",
                        $"Could not verify saved priority for {result.ProcessName}: {ex.Message}",
                        result,
                        process,
                        source,
                        phase,
                        requested,
                        observed: null,
                        attempt: 0,
                        reason: ex.Message).ConfigureAwait(false);
                    return;
                }
            }
        }

        private async Task ScheduleRetryAsync(PersistentRuleAutoApplyResult result, ProcessModel process, string source, ProcessPriorityClass requested, ProcessPriorityClass observed)
        {
            if (this.ruleStore == null || this.matcher == null)
            {
                return;
            }

            var signature = await this.GetCurrentRuleSignature(result.RuleId).ConfigureAwait(false);
            if (signature == null)
            {
                TaskSafety.FireAndForget(
                    this.LogRetrySkippedAsync(result, process, source, "rule-not-found", requested, observed, "delayed"),
                    _ => { });
                return;
            }

            var key = new RetryKey(result.ProcessId, signature);
            if (!this.retryUsed.TryAdd(key, 0))
            {
                TaskSafety.FireAndForget(
                    this.LogRetrySkippedAsync(result, process, source, "retry-already-used", requested, observed, "delayed"),
                    _ => { });
                return;
            }

            var cts = new CancellationTokenSource();
            if (!this.pendingRetries.TryAdd(key, cts))
            {
                cts.Dispose();
                return;
            }

            TaskSafety.FireAndForget(
                this.LogActivityAsync(
                    "PersistentRulePriorityRetryScheduled",
                    $"Scheduled one priority retry for {result.ProcessName}: requested {requested}, observed {observed}.",
                    result,
                    process,
                    source,
                    "delayed",
                    requested,
                    observed,
                    attempt: 1,
                    reason: $"retry-delay={this.retryDelay.TotalSeconds:0.#}s"),
                _ => { });

            TaskSafety.FireAndForget(
                this.RunRetryAsync(key, result, process, source, requested, cts.Token),
                ex => this.logger.LogDebug(ex, "Persistent rule priority retry failed"));
        }

        private async Task RunRetryAsync(RetryKey key, PersistentRuleAutoApplyResult result, ProcessModel process, string source, ProcessPriorityClass requested, CancellationToken cancellationToken)
        {
            try
            {
                await this.delayAsync(this.retryDelay, cancellationToken).ConfigureAwait(false);
                var rule = await this.GetRetryRuleAsync(result.RuleId, key.RuleSignature, process).ConfigureAwait(false);
                if (rule == null)
                {
                    await this.LogRetrySkippedAsync(result, process, source, "rule-disabled-or-not-matching", requested, process.Priority, "retry-immediate").ConfigureAwait(false);
                    return;
                }

                if (process.Classification == ProcessClassification.ProtectedOrAccessDenied)
                {
                    await this.LogRetrySkippedAsync(result, process, source, "protected-or-access-denied", requested, process.Priority, "retry-immediate").ConfigureAwait(false);
                    return;
                }

                if (!await this.processService.IsProcessStillRunning(process).ConfigureAwait(false))
                {
                    await this.LogRetrySkippedAsync(result, process, source, "process-exited", requested, process.Priority, "retry-immediate").ConfigureAwait(false);
                    return;
                }

                await this.processService.RefreshProcessInfo(process).ConfigureAwait(false);
                if (process.Priority == requested)
                {
                    await this.LogRetryVerifiedAsync(result, process, source, "retry-immediate", requested, process.Priority).ConfigureAwait(false);
                    return;
                }

                await this.processService.SetProcessPriority(process, requested, ProcessPriorityWriteSource.PersistentRuleRetry).ConfigureAwait(false);
                await this.processService.RefreshProcessInfo(process).ConfigureAwait(false);
                if (process.Priority == requested)
                {
                    await this.LogRetryVerifiedAsync(result, process, source, "retry-immediate", requested, process.Priority).ConfigureAwait(false);
                }
                else
                {
                    await this.LogActivityAsync(
                        "PersistentRulePriorityRetryNotObserved",
                        $"Priority retry for {result.ProcessName} was not observed: requested {requested}, observed {process.Priority}.",
                        result,
                        process,
                        source,
                        "retry-immediate",
                        requested,
                        process.Priority,
                        attempt: 1).ConfigureAwait(false);
                    return;
                }

                var finalDelay = this.delays.Count > 0 ? this.delays[^1] : TimeSpan.Zero;
                await this.delayAsync(finalDelay, cancellationToken).ConfigureAwait(false);
                await this.processService.RefreshProcessInfo(process).ConfigureAwait(false);
                if (process.Priority == requested)
                {
                    await this.LogRetryVerifiedAsync(result, process, source, "retry-delayed", requested, process.Priority).ConfigureAwait(false);
                    return;
                }

                await this.LogActivityAsync(
                    "PersistentRulePriorityRetryReverted",
                    $"Priority retry for {result.ProcessName} reverted again: requested {requested}, observed {process.Priority}.",
                    result,
                    process,
                    source,
                    "retry-delayed",
                    requested,
                    process.Priority,
                    attempt: 1).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var reason = AffinityApplyExceptionClassifier.IsProcessExited(ex) ? "process-exited" :
                    AffinityApplyExceptionClassifier.IsAccessDenied(ex) || AffinityApplyExceptionClassifier.IsAntiCheatLikely(ex) ? "access-denied-or-protected" : ex.Message;
                var action = reason is "process-exited" or "access-denied-or-protected"
                    ? "PersistentRulePriorityRetrySkipped"
                    : "PersistentRulePriorityRetryFailed";
                await this.LogActivityAsync(action, $"Priority retry for {result.ProcessName} did not run: {reason}.", result, process, source, "retry-immediate", requested, null, 1, reason).ConfigureAwait(false);
            }
            finally
            {
                if (this.pendingRetries.TryRemove(key, out var cts))
                {
                    cts.Dispose();
                }
            }
        }

        private async Task<string?> GetCurrentRuleSignature(string ruleId)
        {
            var rule = (await this.ruleStore!.LoadAsync().ConfigureAwait(false)).FirstOrDefault(rule => rule.Id == ruleId);
            return rule == null ? null : PersistentRuleRuntimeKeys.GetRuleSignature(rule);
        }

        private async Task<PersistentProcessRule?> GetRetryRuleAsync(string ruleId, string ruleSignature, ProcessModel process)
        {
            var rule = (await this.ruleStore!.LoadAsync().ConfigureAwait(false)).FirstOrDefault(rule => rule.Id == ruleId);
            return rule != null &&
                rule.IsEnabled &&
                PersistentRuleRuntimeKeys.GetRuleSignature(rule) == ruleSignature &&
                this.matcher!.IsMatch(rule, process)
                    ? rule
                    : null;
        }

        private static bool ShouldVerify(PersistentRuleAutoApplyResult result) =>
            result.PriorityVerified && result.RequestedPriority.HasValue;

        private Task LogVerifiedAsync(PersistentRuleAutoApplyResult result, ProcessModel process, string source, string phase, ProcessPriorityClass requested, ProcessPriorityClass observed, int attempt)
        {
            this.logger.LogInformation(
                "Persistent rule priority verified for rule {RuleId} on process {ProcessName} (PID: {ProcessId}). Source: {Source}; requested: {RequestedPriority}; observed: {ObservedPriority}; phase: {Phase}; path: {ExecutablePath}; attempt: {Attempt}",
                result.RuleId, result.ProcessName, result.ProcessId, source, requested, observed, phase, process.ExecutablePath, attempt);
            return this.LogActivityAsync("PersistentRulePriorityVerified", $"Verified saved priority for {result.ProcessName}: {observed}.", result, process, source, phase, requested, observed, attempt);
        }

        private Task LogRevertedAsync(PersistentRuleAutoApplyResult result, ProcessModel process, string source, string phase, ProcessPriorityClass requested, ProcessPriorityClass observed)
        {
            this.logger.LogWarning(
                "Persistent rule priority reverted after apply for rule {RuleId} on process {ProcessName} (PID: {ProcessId}). Source: {Source}; requested: {RequestedPriority}; observed: {ObservedPriority}; phase: {Phase}; path: {ExecutablePath}",
                result.RuleId, result.ProcessName, result.ProcessId, source, requested, observed, phase, process.ExecutablePath);
            return this.LogActivityAsync("PersistentRulePriorityRevertedAfterApply", $"Saved priority for {result.ProcessName} was reverted: requested {requested}, observed {observed}.", result, process, source, phase, requested, observed, attempt: 0);
        }

        private Task LogRetryVerifiedAsync(PersistentRuleAutoApplyResult result, ProcessModel process, string source, string phase, ProcessPriorityClass requested, ProcessPriorityClass observed) =>
            this.LogActivityAsync("PersistentRulePriorityRetryVerified", $"Priority retry for {result.ProcessName} verified as {observed}.", result, process, source, phase, requested, observed, attempt: 1);

        private Task LogRetrySkippedAsync(PersistentRuleAutoApplyResult result, ProcessModel process, string source, string reason, ProcessPriorityClass requested, ProcessPriorityClass? observed, string phase) =>
            this.LogActivityAsync("PersistentRulePriorityRetrySkipped", $"Priority retry for {result.ProcessName} skipped: {reason}.", result, process, source, phase, requested, observed, attempt: 1, reason: reason);

        private async Task LogActivityAsync(
            string action,
            string message,
            PersistentRuleAutoApplyResult result,
            ProcessModel process,
            string source,
            string phase,
            ProcessPriorityClass requested,
            ProcessPriorityClass? observed,
            int attempt,
            string? reason = null)
        {
            if (this.activityAuditService == null)
            {
                return;
            }

            await this.activityAuditService.LogUserActionAsync(
                action,
                message,
                $"Action: {action}, Rule: {result.RuleId}, PID: {result.ProcessId}, Process: {result.ProcessName}, Source: {source}, Requested: {requested}, Observed: {observed?.ToString() ?? "unavailable"}, Attempt: {attempt}, Phase: {phase}, Reason: {reason ?? "none"}, Path: {process.ExecutablePath}").ConfigureAwait(false);
        }

        private readonly record struct RetryKey(int ProcessId, string RuleSignature);
    }
}
