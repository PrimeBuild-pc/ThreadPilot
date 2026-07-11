/*
 * ThreadPilot - persistent rule runtime auto-apply coordinator.
 */
namespace ThreadPilot.Services
{
    using System.Collections.Concurrent;
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
        private readonly Func<DateTimeOffset> nowProvider;
        private readonly TimeSpan cooldown;
        private readonly ConcurrentDictionary<RuleAttemptKey, DateTimeOffset> recentAttempts = new();

        public PersistentRuleAutoApplyService(
            IPersistentProcessRuleStore ruleStore,
            IPersistentProcessRuleMatcher matcher,
            IPersistentRulesEngine rulesEngine,
            IApplicationSettingsService settingsService,
            ILogger<PersistentRuleAutoApplyService> logger,
            IActivityAuditService? activityAuditService = null,
            IPersistentRulePriorityVerificationService? priorityVerificationService = null)
            : this(ruleStore, matcher, rulesEngine, settingsService, logger, () => DateTimeOffset.UtcNow, DefaultCooldown, activityAuditService, priorityVerificationService)
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
            IPersistentRulePriorityVerificationService? priorityVerificationService = null)
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
            var now = this.nowProvider();
            var candidates = rules
                .Where(rule => rule.IsEnabled && this.matcher.IsMatch(rule, process))
                .ToList();

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

                return results;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                this.logger.LogWarning(
                    ex,
                    "Persistent rule auto-apply failed for process {ProcessName} (PID: {ProcessId})",
                    process.Name,
                    process.ProcessId);

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

        private static bool IsProcessEligible(ProcessModel process) =>
            process.ProcessId > 0 && !string.IsNullOrWhiteSpace(process.Name);

        private readonly record struct RuleAttemptKey(int ProcessId, string RuleSignature);
    }
}
