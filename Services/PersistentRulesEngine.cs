/*
 * ThreadPilot - persistent rules engine foundation.
 */
namespace ThreadPilot.Services
{
    using System.Diagnostics;
    using Microsoft.Extensions.Logging;
    using ThreadPilot.Models;

    public interface IPersistentRulesEngine
    {
        Task<IReadOnlyList<PersistentRuleApplyResult>> ApplyMatchingRulesAsync(
            ProcessModel process,
            CancellationToken cancellationToken = default);
    }

    public sealed class PersistentRulesEngine : IPersistentRulesEngine
    {
        private const string MissingAffinityErrorCode = "PersistentRuleMissingAffinity";
        private const string MissingPriorityErrorCode = "PersistentRuleMissingPriority";
        private const string NoActionsErrorCode = "PersistentRuleNoActions";
        private const string PriorityApplyFailedErrorCode = "PriorityApplyFailed";
        private const string RealtimePriorityBlockedErrorCode = "RealtimePriorityBlocked";

        private readonly IPersistentProcessRuleStore ruleStore;
        private readonly IPersistentProcessRuleMatcher matcher;
        private readonly IAffinityApplyService affinityApplyService;
        private readonly IProcessService processService;
        private readonly ILogger<PersistentRulesEngine> logger;

        public PersistentRulesEngine(
            IPersistentProcessRuleStore ruleStore,
            IPersistentProcessRuleMatcher matcher,
            IAffinityApplyService affinityApplyService,
            IProcessService processService,
            ILogger<PersistentRulesEngine> logger)
        {
            this.ruleStore = ruleStore ?? throw new ArgumentNullException(nameof(ruleStore));
            this.matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
            this.affinityApplyService = affinityApplyService ?? throw new ArgumentNullException(nameof(affinityApplyService));
            this.processService = processService ?? throw new ArgumentNullException(nameof(processService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IReadOnlyList<PersistentRuleApplyResult>> ApplyMatchingRulesAsync(
            ProcessModel process,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(process);

            var rules = await this.ruleStore.LoadAsync().ConfigureAwait(false);
            var results = new List<PersistentRuleApplyResult>();

            foreach (var rule in rules.Where(rule => this.matcher.IsMatch(rule, process)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                results.Add(await this.ApplyRuleAsync(rule, process, cancellationToken).ConfigureAwait(false));
            }

            return results;
        }

        private async Task<PersistentRuleApplyResult> ApplyRuleAsync(
            PersistentProcessRule rule,
            ProcessModel process,
            CancellationToken cancellationToken)
        {
            var result = CreateSuccessResult(rule, process);
            var success = true;

            if (!rule.ApplyAffinityOnStart && !rule.ApplyPriorityOnStart)
            {
                return MarkRuleConfigurationFailure(
                    result,
                    rule,
                    NoActionsErrorCode,
                    "This saved rule has no actions to apply.");
            }

            if (rule.ApplyAffinityOnStart)
            {
                if (rule.CpuSelection == null && !rule.LegacyAffinityMask.HasValue)
                {
                    success = false;
                    result = MarkRuleConfigurationFailure(
                        result,
                        rule,
                        MissingAffinityErrorCode,
                        "This saved rule has no affinity selection to apply.");
                }
                else
                {
                    var affinityResult = await this.ApplyAffinityAsync(rule, process).ConfigureAwait(false);
                    if (affinityResult.Success)
                    {
                        result = result with { AffinityApplied = true };
                    }
                    else
                    {
                        success = false;
                        result = MergeAffinityFailure(result, affinityResult);
                    }
                }
            }

            if (rule.ApplyPriorityOnStart && !result.IsProcessExited)
            {
                if (!rule.Priority.HasValue)
                {
                    success = false;
                    result = MarkRuleConfigurationFailure(
                        result,
                        rule,
                        MissingPriorityErrorCode,
                        "This saved rule has no priority value to apply.");
                }
                else
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        await this.processService.SetProcessPriority(process, rule.Priority.Value).ConfigureAwait(false);
                        result = result with { PriorityApplied = true };
                    }
                    catch (Exception ex)
                    {
                        success = false;
                        result = this.MergePriorityFailure(result, ex);
                    }
                }
            }

            return result with
            {
                Success = success,
                UserMessage = success ? "Persistent rule applied." : result.UserMessage,
                TechnicalMessage = success ? $"Persistent rule '{rule.Name}' applied to process {process.Name}." : result.TechnicalMessage,
            };
        }

        private Task<AffinityApplyResult> ApplyAffinityAsync(PersistentProcessRule rule, ProcessModel process)
        {
            if (rule.CpuSelection != null)
            {
                return this.affinityApplyService.ApplyAsync(process, rule.CpuSelection);
            }

            if (rule.LegacyAffinityMask.HasValue)
            {
                return this.affinityApplyService.ApplyAsync(process, rule.LegacyAffinityMask.Value);
            }

            return Task.FromResult(AffinityApplyResult.Succeeded(0, process.ProcessorAffinity));
        }

        private static PersistentRuleApplyResult MarkRuleConfigurationFailure(
            PersistentRuleApplyResult result,
            PersistentProcessRule rule,
            string errorCode,
            string userMessage) =>
            result with
            {
                Success = false,
                ErrorCode = errorCode,
                UserMessage = userMessage,
                TechnicalMessage = $"Persistent rule '{rule.Name}' ({rule.Id}) is incomplete: {userMessage}",
            };

        private PersistentRuleApplyResult MergePriorityFailure(PersistentRuleApplyResult result, Exception ex)
        {
            this.logger.LogWarning(
                ex,
                "Persistent rule priority apply failed for rule {RuleId} on process {ProcessName} (PID: {ProcessId})",
                result.RuleId,
                result.ProcessName,
                result.ProcessId);

            var isProcessExited = AffinityApplyExceptionClassifier.IsProcessExited(ex);
            var isAccessDenied = AffinityApplyExceptionClassifier.IsAccessDenied(ex);
            var isAntiCheatLikely = AffinityApplyExceptionClassifier.IsAntiCheatLikely(ex);
            var isRealtimeBlocked = string.Equals(
                ex.Message,
                ProcessOperationUserMessages.RealtimePriorityBlocked,
                StringComparison.Ordinal);

            return result with
            {
                ErrorCode = isRealtimeBlocked
                    ? RealtimePriorityBlockedErrorCode
                    : isProcessExited
                        ? AffinityApplyErrorCodes.ProcessExited
                        : isAntiCheatLikely
                            ? AffinityApplyErrorCodes.AntiCheatOrProtectedProcessLikely
                            : isAccessDenied
                                ? AffinityApplyErrorCodes.AccessDenied
                                : PriorityApplyFailedErrorCode,
                UserMessage = isRealtimeBlocked
                    ? ProcessOperationUserMessages.RealtimePriorityBlocked
                    : isProcessExited
                        ? ProcessOperationUserMessages.ProcessExited
                        : isAntiCheatLikely
                            ? ProcessOperationUserMessages.PersistentRulesProtectedProcessWarning
                            : isAccessDenied
                                ? ProcessOperationUserMessages.AccessDenied
                                : "ThreadPilot could not apply the saved priority rule.",
                TechnicalMessage = ex.Message,
                IsAccessDenied = result.IsAccessDenied || isAccessDenied,
                IsAntiCheatLikely = result.IsAntiCheatLikely || isAntiCheatLikely,
                IsProcessExited = result.IsProcessExited || isProcessExited,
            };
        }

        private static PersistentRuleApplyResult CreateSuccessResult(PersistentProcessRule rule, ProcessModel process) =>
            new()
            {
                Success = true,
                RuleId = rule.Id,
                ProcessId = process.ProcessId,
                ProcessName = process.Name,
                UserMessage = "Persistent rule applied.",
                TechnicalMessage = $"Persistent rule '{rule.Name}' matched process {process.Name}.",
            };

        private static PersistentRuleApplyResult MergeAffinityFailure(
            PersistentRuleApplyResult result,
            AffinityApplyResult affinityResult) =>
            result with
            {
                ErrorCode = affinityResult.ErrorCode,
                UserMessage = affinityResult.IsAntiCheatLikely
                    ? ProcessOperationUserMessages.PersistentRulesProtectedProcessWarning
                    : affinityResult.UserMessage,
                TechnicalMessage = affinityResult.TechnicalMessage,
                IsAccessDenied = result.IsAccessDenied || affinityResult.IsAccessDenied,
                IsAntiCheatLikely = result.IsAntiCheatLikely || affinityResult.IsAntiCheatLikely,
                IsProcessExited = result.IsProcessExited ||
                    affinityResult.ErrorCode == AffinityApplyErrorCodes.ProcessExited ||
                    affinityResult.FailureReason == AffinityApplyFailureReason.ProcessTerminated,
            };
    }
}
