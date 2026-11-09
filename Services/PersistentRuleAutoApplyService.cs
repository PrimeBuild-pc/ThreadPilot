/*
 * ThreadPilot - persistent rule runtime auto-apply coordinator.
 */
namespace ThreadPilot.Services
{
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.IO;
    using Microsoft.Extensions.Logging;
    using ThreadPilot.Models;

    public interface IPersistentRuleAutoApplyService
    {
        Task<IReadOnlyList<PersistentRuleAutoApplyResult>> ApplyForDiscoveredProcessesAsync(
            IEnumerable<ProcessModel> processes,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<PersistentRuleAutoApplyResult>> ApplyForProcessStartAsync(
            ProcessModel process,
            CancellationToken cancellationToken = default);

        void MarkProcessExited(int processId);
    }

    public sealed record PersistentRuleAutoApplyResult
    {
        public bool Success { get; init; }

        public string RuleId { get; init; } = string.Empty;

        public int ProcessId { get; init; }

        public string ProcessName { get; init; } = string.Empty;

        public string? ErrorCode { get; init; }

        public string UserMessage { get; init; } = string.Empty;

        public string TechnicalMessage { get; init; } = string.Empty;

        public bool IsAccessDenied { get; init; }

        public bool IsAntiCheatLikely { get; init; }

        public bool IsProcessExited { get; init; }

        public System.Diagnostics.ProcessPriorityClass? RequestedPriority { get; init; }

        public System.Diagnostics.ProcessPriorityClass? ObservedPriority { get; init; }

        public bool PriorityVerified { get; init; }

        public bool PriorityVerificationUnavailable { get; init; }

        public string? PriorityVerificationPhase { get; init; }

        public static PersistentRuleAutoApplyResult FromApplyResult(PersistentRuleApplyResult result) =>
            new()
            {
                Success = result.Success,
                RuleId = result.RuleId,
                ProcessId = result.ProcessId,
                ProcessName = result.ProcessName,
                ErrorCode = result.ErrorCode,
                UserMessage = result.IsAntiCheatLikely
                    ? ProcessOperationUserMessages.PersistentRulesProtectedProcessWarning
                    : result.UserMessage,
                TechnicalMessage = result.TechnicalMessage,
                IsAccessDenied = result.IsAccessDenied,
                IsAntiCheatLikely = result.IsAntiCheatLikely,
                IsProcessExited = result.IsProcessExited,
                RequestedPriority = result.RequestedPriority,
                ObservedPriority = result.ObservedPriority,
                PriorityVerified = result.PriorityVerified,
                PriorityVerificationUnavailable = result.PriorityVerificationUnavailable,
                PriorityVerificationPhase = result.PriorityVerificationPhase,
            };
    }

    public sealed class PersistentRuleAutoApplyService : IPersistentRuleAutoApplyService
    {
        private static readonly TimeSpan DefaultCooldown = TimeSpan.FromSeconds(30);

        private readonly IPersistentProcessRuleStore ruleStore;
        private readonly IPersistentProcessRuleMatcher matcher;
        private readonly IPersistentRulesEngine rulesEngine;
        private readonly IApplicationSettingsService settingsService;
        private readonly ILogger<PersistentRuleAutoApplyService> logger;
        private readonly IActivityAuditService? activityAuditService;
        private readonly IPersistentRulePriorityVerificationService? priorityVerificationService;
        private readonly IProcessService? processService;
        private readonly Func<DateTimeOffset> nowProvider;
        private readonly TimeSpan cooldown;
        private readonly IEnhancedLoggingService? enhancedLogger;
        private readonly object attemptLock = new();
        private readonly Dictionary<int, CancellationTokenSource> activeApplications = new();
        private readonly ConcurrentDictionary<RuleAttemptKey, DateTimeOffset> recentAttempts = new();

        public PersistentRuleAutoApplyService(
            IPersistentProcessRuleStore ruleStore,
            IPersistentProcessRuleMatcher matcher,
            IPersistentRulesEngine rulesEngine,
            IApplicationSettingsService settingsService,
            ILogger<PersistentRuleAutoApplyService> logger,
            IActivityAuditService? activityAuditService = null,
            IPersistentRulePriorityVerificationService? priorityVerificationService = null,
            IProcessService? processService = null,
            IEnhancedLoggingService? enhancedLogger = null)
            : this(ruleStore, matcher, rulesEngine, settingsService, logger, () => DateTimeOffset.UtcNow, DefaultCooldown, activityAuditService, priorityVerificationService, processService, enhancedLogger)
        {
        }

        public PersistentRuleAutoApplyService(
            IPersistentProcessRuleStore ruleStore,
            IPersistentProcessRuleMatcher matcher,
            IPersistentRulesEngine rulesEngine,
            IApplicationSettingsService settingsService,
            ILogger<PersistentRuleAutoApplyService> logger,
            Func<DateTimeOffset> nowProvider,
            TimeSpan cooldown,
            IActivityAuditService? activityAuditService = null,
            IPersistentRulePriorityVerificationService? priorityVerificationService = null,
            IProcessService? processService = null,
            IEnhancedLoggingService? enhancedLogger = null)
        {
            this.ruleStore = ruleStore ?? throw new ArgumentNullException(nameof(ruleStore));
            this.matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
            this.rulesEngine = rulesEngine ?? throw new ArgumentNullException(nameof(rulesEngine));
            this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.nowProvider = nowProvider ?? throw new ArgumentNullException(nameof(nowProvider));
            this.cooldown = cooldown <= TimeSpan.Zero ? DefaultCooldown : cooldown;
            this.activityAuditService = activityAuditService;
            this.priorityVerificationService = priorityVerificationService;
            this.processService = processService;
            this.enhancedLogger = enhancedLogger;
        }

        public async Task<IReadOnlyList<PersistentRuleAutoApplyResult>> ApplyForDiscoveredProcessesAsync(
            IEnumerable<ProcessModel> processes,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(processes);

            var snapshot = processes
                .Where(IsProcessEligible)
                .GroupBy(process => process.ProcessId)
                .Select(group => group.First())
                .ToList();
            this.ClearAttemptsForMissingProcesses(snapshot.Select(process => process.ProcessId).ToHashSet());

            if (!this.IsEnabled() || snapshot.Count == 0)
            {
                return Array.Empty<PersistentRuleAutoApplyResult>();
            }

            var rules = await this.ruleStore.LoadAsync().ConfigureAwait(false);
            if (rules.Count == 0)
            {
                return Array.Empty<PersistentRuleAutoApplyResult>();
            }

            var results = new List<PersistentRuleAutoApplyResult>();
            foreach (var process in snapshot)
            {
                cancellationToken.ThrowIfCancellationRequested();
                results.AddRange(await this.ApplyForProcessAsync(process, rules, "DiscoveredSnapshot", cancellationToken).ConfigureAwait(false));
            }

            return results;
        }

        public async Task<IReadOnlyList<PersistentRuleAutoApplyResult>> ApplyForProcessStartAsync(
            ProcessModel process,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(process);

            if (!this.IsEnabled() || !IsProcessEligible(process))
            {
                return Array.Empty<PersistentRuleAutoApplyResult>();
            }

            var rules = await this.ruleStore.LoadAsync().ConfigureAwait(false);
            return await this.ApplyForProcessAsync(process, rules, "ProcessStarted", cancellationToken).ConfigureAwait(false);
        }

        public void MarkProcessExited(int processId)
        {
            lock (this.attemptLock)
            {
                if (this.activeApplications.Remove(processId, out var application))
                {
                    application.Cancel();
                }
            }

            foreach (var key in this.recentAttempts.Keys.Where(key => key.ProcessId == processId))
            {
                this.recentAttempts.TryRemove(key, out _);
            }

            this.priorityVerificationService?.MarkProcessExited(processId);
        }

        private async Task<IReadOnlyList<PersistentRuleAutoApplyResult>> ApplyForProcessAsync(
            ProcessModel process,
            IReadOnlyList<PersistentProcessRule> rules,
            string source,
            CancellationToken cancellationToken)
        {
            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            lock (this.attemptLock)
            {
                if (!this.activeApplications.TryAdd(process.ProcessId, lifetime))
                {
                    return Array.Empty<PersistentRuleAutoApplyResult>();
                }
            }

            try
            {
                return await this.ApplyForProcessCoreAsync(process, rules, source, lifetime.Token).ConfigureAwait(false);
            }
            finally
            {
                lock (this.attemptLock)
                {
                    if (this.activeApplications.TryGetValue(process.ProcessId, out var current) && ReferenceEquals(current, lifetime))
                    {
                        this.activeApplications.Remove(process.ProcessId);
                    }
                }
            }
        }

        private async Task<IReadOnlyList<PersistentRuleAutoApplyResult>> ApplyForProcessCoreAsync(
            ProcessModel process,
            IReadOnlyList<PersistentProcessRule> rules,
            string source,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var now = this.nowProvider();
            var potentialRules = rules
                .Where(rule => rule.IsEnabled && IsPotentialNameMatch(rule, process.Name))
                .ToList();

            if (potentialRules.Count == 0)
            {
                return Array.Empty<PersistentRuleAutoApplyResult>();
            }

            if (this.processService != null &&
                string.IsNullOrWhiteSpace(process.ExecutablePath) &&
                potentialRules.Any(rule => !string.IsNullOrWhiteSpace(rule.ExecutablePath)))
            {
                // Capture creation time with query-only access so a reused PID cannot receive this retry.
                var identity = TryGetProcessStartTime(process.ProcessId);
                await this.TryRefreshPathAsync(process).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (identity != null && !IsOriginalProcessRunning(process.ProcessId, identity.Value))
                {
                    return Array.Empty<PersistentRuleAutoApplyResult>();
                }

                if (string.IsNullOrWhiteSpace(process.ExecutablePath) && identity != null)
                {
                    source += "/PathRetry";
                    await this.LogPathRetryAsync(process, "scheduled (up to 3 retries over 3 seconds)").ConfigureAwait(false);
                    try
                    {
                        foreach (var delay in new[] { 250, 750, 2000 })
                        {
                            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                            if (!this.IsEnabled() || !IsOriginalProcessRunning(process.ProcessId, identity.Value))
                            {
                                await this.LogPathRetryAsync(process, "stopped: process exited or automation disabled").ConfigureAwait(false);
                                return Array.Empty<PersistentRuleAutoApplyResult>();
                            }

                            rules = await this.ruleStore.LoadAsync().ConfigureAwait(false);
                            potentialRules = rules.Where(rule => rule.IsEnabled && IsPotentialNameMatch(rule, process.Name)).ToList();
                            if (!potentialRules.Any(rule => !string.IsNullOrWhiteSpace(rule.ExecutablePath)))
                            {
                                break;
                            }

                            await this.TryRefreshPathAsync(process).ConfigureAwait(false);
                            cancellationToken.ThrowIfCancellationRequested();
                            if (!IsOriginalProcessRunning(process.ProcessId, identity.Value))
                            {
                                await this.LogPathRetryAsync(process, "stopped: original process exited").ConfigureAwait(false);
                                return Array.Empty<PersistentRuleAutoApplyResult>();
                            }

                            if (!string.IsNullOrWhiteSpace(process.ExecutablePath))
                            {
                                break;
                            }
                        }

                        await this.LogPathRetryAsync(process, string.IsNullOrWhiteSpace(process.ExecutablePath)
                            ? "finished: path still unavailable or rule removed"
                            : "recovered: matching current rules against refreshed path").ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        await this.LogPathRetryAsync(process, "cancelled: monitoring stopped or process exited").ConfigureAwait(false);
                        throw;
                    }
                }
                else if (string.IsNullOrWhiteSpace(process.ExecutablePath))
                {
                    await this.LogPathRetryAsync(process, "skipped: original process identity unavailable").ConfigureAwait(false);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            var candidates = potentialRules
                .Where(rule => this.matcher.IsMatch(rule, process))
                .ToList();

            if (source.EndsWith("/PathRetry", StringComparison.Ordinal))
            {
                await this.LogPathRetryAsync(process, $"matched {candidates.Count} current rules").ConfigureAwait(false);
            }

            if (candidates.Count == 0)
            {
                return Array.Empty<PersistentRuleAutoApplyResult>();
            }

            var selectedRules = candidates
                .Where(rule => this.TryRecordAttempt(process.ProcessId, rule, now))
                .ToList();

            if (selectedRules.Count == 0)
            {
                this.logger.LogDebug(
                    "Persistent rule auto-apply suppressed by cooldown for process {ProcessName} (PID: {ProcessId})",
                    process.Name,
                    process.ProcessId);
                return Array.Empty<PersistentRuleAutoApplyResult>();
            }

            var selectedSignatures = selectedRules
                .Select(PersistentRuleRuntimeKeys.GetRuleSignature)
                .ToHashSet(StringComparer.Ordinal);

            try
            {
                // Runtime auto-apply only runs while ThreadPilot is open; it does not use registry,
                // IFEO, services, or protected-process bypass techniques.
                var applyResults = await this.rulesEngine
                    .ApplyMatchingRulesAsync(
                        process,
                        rule => selectedSignatures.Contains(PersistentRuleRuntimeKeys.GetRuleSignature(rule)),
                        cancellationToken)
                    .ConfigureAwait(false);

                var results = applyResults.Select(PersistentRuleAutoApplyResult.FromApplyResult).ToList();
                foreach (var result in results)
                {
                    await this.LogResultAsync(result, process, source).ConfigureAwait(false);
                    this.priorityVerificationService?.ScheduleDelayedVerification(result, process, source);
                }

                if (source.EndsWith("/PathRetry", StringComparison.Ordinal))
                {
                    await this.LogPathRetryAsync(process, $"completed: {results.Count(result => result.Success)} succeeded, {results.Count(result => !result.Success)} failed").ConfigureAwait(false);
                }

                return results;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                this.logger.LogWarning(
                    ex,
                    "Persistent rule auto-apply failed for process {ProcessName} (PID: {ProcessId})",
                    process.Name,
                    process.ProcessId);

                if (source.EndsWith("/PathRetry", StringComparison.Ordinal))
                {
                    await this.LogPathRetryAsync(process, $"apply failed: {ex.Message}").ConfigureAwait(false);
                }

                return selectedRules
                    .Select(rule => new PersistentRuleAutoApplyResult
                    {
                        Success = false,
                        RuleId = rule.Id,
                        ProcessId = process.ProcessId,
                        ProcessName = process.Name,
                        UserMessage = "ThreadPilot could not apply the saved rule.",
                        TechnicalMessage = ex.Message,
                    })
                    .ToList();
            }
        }

        private static DateTime? TryGetProcessStartTime(int processId)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                return process.StartTime;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsOriginalProcessRunning(int processId, DateTime originalStartTime)
        {
            try
            {
                using var current = Process.GetProcessById(processId);
                return !current.HasExited && current.StartTime == originalStartTime;
            }
            catch
            {
                return false;
            }
        }

        private async Task TryRefreshPathAsync(ProcessModel process)
        {
            try
            {
                await this.processService!.RefreshProcessInfo(process).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                this.logger.LogDebug(ex, "Could not enrich process {ProcessId} for path-based rule matching", process.ProcessId);
            }
        }

        private async Task LogPathRetryAsync(ProcessModel process, string outcome)
        {
            try
            {
                if (this.enhancedLogger != null)
                {
                    await this.enhancedLogger.LogProcessMonitoringEventAsync(
                        "PersistentRulePathRetry",
                        process.Name,
                        process.ProcessId,
                        $"Path-based saved rule metadata retry: {outcome}; path: {process.ExecutablePath}").ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Could not persist path retry diagnostic");
            }
        }

        private bool TryRecordAttempt(int processId, PersistentProcessRule rule, DateTimeOffset now)
        {
            var key = new RuleAttemptKey(processId, PersistentRuleRuntimeKeys.GetRuleSignature(rule));
            if (this.recentAttempts.TryGetValue(key, out var lastAttempt) &&
                now - lastAttempt < this.cooldown)
            {
                return false;
            }

            this.recentAttempts[key] = now;
            return true;
        }

        private void ClearAttemptsForMissingProcesses(HashSet<int> currentProcessIds)
        {
            lock (this.attemptLock)
            {
                foreach (var pair in this.activeApplications.Where(pair => !currentProcessIds.Contains(pair.Key)).ToList())
                {
                    pair.Value.Cancel();
                }
            }

            foreach (var key in this.recentAttempts.Keys.Where(key => !currentProcessIds.Contains(key.ProcessId)))
            {
                this.recentAttempts.TryRemove(key, out _);
            }
        }

        private async Task LogResultAsync(PersistentRuleAutoApplyResult result, ProcessModel process, string source)
        {
            if (result.Success)
            {
                this.logger.LogInformation(
                    "Applied saved persistent rule {RuleId} to process {ProcessName} (PID: {ProcessId}). Source: {Source}; path: {ExecutablePath}; requested priority: {RequestedPriority}; observed priority: {ObservedPriority}; priority verified: {PriorityVerified}; phase: {Phase}",
                    result.RuleId,
                    result.ProcessName,
                    result.ProcessId,
                    source,
                    process.ExecutablePath,
                    result.RequestedPriority,
                    result.ObservedPriority,
                    result.PriorityVerified,
                    result.PriorityVerificationPhase);
                await this.LogActivityResultAsync(result, process, source).ConfigureAwait(false);
                return;
            }

            var logLevel = result.IsAccessDenied || result.IsAntiCheatLikely || result.IsProcessExited
                ? LogLevel.Debug
                : LogLevel.Warning;
            this.logger.Log(
                logLevel,
                "Persistent rule {RuleId} was not fully applied to process {ProcessName} (PID: {ProcessId}): {Message}. Source: {Source}; path: {ExecutablePath}; requested priority: {RequestedPriority}; observed priority: {ObservedPriority}; phase: {Phase}",
                result.RuleId,
                result.ProcessName,
                result.ProcessId,
                result.UserMessage,
                source,
                process.ExecutablePath,
                result.RequestedPriority,
                result.ObservedPriority,
                result.PriorityVerificationPhase);
            await this.LogActivityResultAsync(result, process, source).ConfigureAwait(false);
        }

        private async Task LogActivityResultAsync(PersistentRuleAutoApplyResult result, ProcessModel process, string source)
        {
            if (this.activityAuditService == null)
            {
                return;
            }

            var action = ResolveActivityAction(result);
            var message = ResolveActivityMessage(result);

            try
            {
                await this.activityAuditService
                    .LogUserActionAsync(
                        action,
                        message,
                        $"Rule: {result.RuleId}, PID: {result.ProcessId}, Source: {source}, Path: {process.ExecutablePath}, RequestedPriority: {result.RequestedPriority?.ToString() ?? "none"}, ObservedPriority: {result.ObservedPriority?.ToString() ?? "unavailable"}, Phase: {result.PriorityVerificationPhase ?? "none"}")
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Failed to write persistent rule activity audit entry");
            }
        }

        private static string ResolveActivityAction(PersistentRuleAutoApplyResult result)
        {
            if (result.PriorityVerified)
            {
                return "PersistentRulePriorityVerified";
            }

            if (result.RequestedPriority.HasValue && result.ObservedPriority.HasValue)
            {
                return "PersistentRulePriorityNotObserved";
            }

            if (result.RequestedPriority.HasValue && result.PriorityVerificationUnavailable)
            {
                return "PersistentRulePriorityVerificationUnavailable";
            }

            if (result.RequestedPriority.HasValue && !result.Success)
            {
                return "PersistentRulePriorityApplyFailed";
            }

            return result.Success
                ? "PersistentRuleAutoApplied"
                : "PersistentRuleAutoApplyFailed";
        }

        private static string ResolveActivityMessage(PersistentRuleAutoApplyResult result)
        {
            if (result.PriorityVerified)
            {
                return $"Saved rule priority verified immediately for {result.ProcessName} as {result.ObservedPriority}.";
            }

            if (result.RequestedPriority.HasValue && result.ObservedPriority.HasValue)
            {
                return $"Saved rule priority requested for {result.ProcessName}, but observed {result.ObservedPriority} instead of {result.RequestedPriority}.";
            }

            if (result.RequestedPriority.HasValue && result.PriorityVerificationUnavailable)
            {
                return $"Saved rule priority requested for {result.ProcessName}, but verification was unavailable: {result.UserMessage}";
            }

            return result.Success
                ? $"Auto-applied saved rule for {result.ProcessName}."
                : $"Failed to auto-apply saved rule for {result.ProcessName}: {result.UserMessage}";
        }

        private bool IsEnabled() =>
            this.settingsService.Settings.ApplyPersistentRulesOnProcessStart;

        private static bool IsPotentialNameMatch(PersistentProcessRule rule, string processName)
        {
            var candidateName = !string.IsNullOrWhiteSpace(rule.ProcessName)
                ? rule.ProcessName
                : Path.GetFileNameWithoutExtension(rule.ExecutablePath);
            return !string.IsNullOrWhiteSpace(candidateName) &&
                string.Equals(
                    Path.GetFileNameWithoutExtension(candidateName.Trim()),
                    Path.GetFileNameWithoutExtension(processName.Trim()),
                    StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsProcessEligible(ProcessModel process) =>
            process.ProcessId > 0 && !string.IsNullOrWhiteSpace(process.Name);

        private readonly record struct RuleAttemptKey(int ProcessId, string RuleSignature);
    }
}
