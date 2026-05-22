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
        private readonly Func<DateTimeOffset> nowProvider;
        private readonly TimeSpan cooldown;
        private readonly ConcurrentDictionary<RuleAttemptKey, DateTimeOffset> recentAttempts = new();

        public PersistentRuleAutoApplyService(
            IPersistentProcessRuleStore ruleStore,
            IPersistentProcessRuleMatcher matcher,
            IPersistentRulesEngine rulesEngine,
            IApplicationSettingsService settingsService,
            ILogger<PersistentRuleAutoApplyService> logger)
            : this(ruleStore, matcher, rulesEngine, settingsService, logger, () => DateTimeOffset.UtcNow, DefaultCooldown)
        {
        }

        public PersistentRuleAutoApplyService(
            IPersistentProcessRuleStore ruleStore,
            IPersistentProcessRuleMatcher matcher,
            IPersistentRulesEngine rulesEngine,
            IApplicationSettingsService settingsService,
            ILogger<PersistentRuleAutoApplyService> logger,
            Func<DateTimeOffset> nowProvider,
            TimeSpan cooldown)
        {
            this.ruleStore = ruleStore ?? throw new ArgumentNullException(nameof(ruleStore));
            this.matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
            this.rulesEngine = rulesEngine ?? throw new ArgumentNullException(nameof(rulesEngine));
            this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.nowProvider = nowProvider ?? throw new ArgumentNullException(nameof(nowProvider));
            this.cooldown = cooldown <= TimeSpan.Zero ? DefaultCooldown : cooldown;
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
                results.AddRange(await this.ApplyForProcessAsync(process, rules, cancellationToken).ConfigureAwait(false));
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
            return await this.ApplyForProcessAsync(process, rules, cancellationToken).ConfigureAwait(false);
        }

        public void MarkProcessExited(int processId)
        {
            foreach (var key in this.recentAttempts.Keys.Where(key => key.ProcessId == processId))
            {
                this.recentAttempts.TryRemove(key, out _);
            }
        }

        private async Task<IReadOnlyList<PersistentRuleAutoApplyResult>> ApplyForProcessAsync(
            ProcessModel process,
            IReadOnlyList<PersistentProcessRule> rules,
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
                .Select(GetRuleSignature)
                .ToHashSet(StringComparer.Ordinal);

            try
            {
                // Runtime auto-apply only runs while ThreadPilot is open; it does not use registry,
                // IFEO, services, or protected-process bypass techniques.
                var applyResults = await this.rulesEngine
                    .ApplyMatchingRulesAsync(
                        process,
                        rule => selectedSignatures.Contains(GetRuleSignature(rule)),
                        cancellationToken)
                    .ConfigureAwait(false);

                var results = applyResults.Select(PersistentRuleAutoApplyResult.FromApplyResult).ToList();
                foreach (var result in results)
                {
                    this.LogResult(result);
                }

                return results;
            }
            catch (Exception ex)
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
            var key = new RuleAttemptKey(processId, GetRuleSignature(rule));
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

        private void LogResult(PersistentRuleAutoApplyResult result)
        {
            if (result.Success)
            {
                this.logger.LogInformation(
                    "Applied saved persistent rule {RuleId} to process {ProcessName} (PID: {ProcessId})",
                    result.RuleId,
                    result.ProcessName,
                    result.ProcessId);
                return;
            }

            var logLevel = result.IsAccessDenied || result.IsAntiCheatLikely || result.IsProcessExited
                ? LogLevel.Debug
                : LogLevel.Warning;
            this.logger.Log(
                logLevel,
                "Persistent rule {RuleId} was not applied to process {ProcessName} (PID: {ProcessId}): {Message}",
                result.RuleId,
                result.ProcessName,
                result.ProcessId,
                result.UserMessage);
        }

        private bool IsEnabled() =>
            this.settingsService.Settings.ApplyPersistentRulesOnProcessStart;

        private static bool IsProcessEligible(ProcessModel process) =>
            process.ProcessId > 0 && !string.IsNullOrWhiteSpace(process.Name);

        private static string GetRuleSignature(PersistentProcessRule rule) =>
            string.Join(
                "|",
                string.IsNullOrWhiteSpace(rule.Id) ? rule.Name : rule.Id,
                rule.UpdatedAt.ToUniversalTime().Ticks);

        private readonly record struct RuleAttemptKey(int ProcessId, string RuleSignature);
    }
}
