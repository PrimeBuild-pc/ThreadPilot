/*
 * ThreadPilot - persistent process rule creation from explicit Process tab actions.
 */
namespace ThreadPilot.Services
{
    using System.Diagnostics;
    using Microsoft.Extensions.Logging;
    using ThreadPilot.Models;

    public interface IProcessRuleCreationService
    {
        Task<ProcessRuleCreationResult> SaveRuleAsync(
            ProcessModel process,
            ProcessRuleCreationPayload payload,
            CancellationToken cancellationToken = default);

        Task<ProcessRuleCreationResult> SaveCurrentSettingsAsRuleAsync(
            ProcessModel process,
            IReadOnlyList<bool>? currentCoreSelection,
            ProcessMemoryPriority? currentMemoryPriority,
            CancellationToken cancellationToken = default);
    }

    public sealed record ProcessRuleCreationPayload
    {
        public CpuSelection? CpuSelection { get; init; }

        public long? LegacyAffinityMask { get; init; }

        public ProcessPriorityClass? Priority { get; init; }

        public ProcessMemoryPriority? MemoryPriority { get; init; }
    }

    public sealed record ProcessRuleCreationResult
    {
        public bool Success { get; init; }

        public bool Created { get; init; }

        public bool Updated { get; init; }

        public PersistentProcessRule? Rule { get; init; }

        public string UserMessage { get; init; } = string.Empty;

        public string ErrorCode { get; init; } = string.Empty;

        public static ProcessRuleCreationResult Failed(string errorCode, string userMessage) =>
            new()
            {
                Success = false,
                ErrorCode = errorCode,
                UserMessage = userMessage,
            };
    }

    public sealed class ProcessRuleCreationService : IProcessRuleCreationService
    {
        public const string NoCurrentSettingsMessage =
            "There are no current settings to save as a rule.";

        public const string UnsafeAffinityMessage =
            "The current affinity selection cannot be saved safely on this CPU topology.";

        private const string RuleDescription = "Created from Process tab action.";

        private readonly IPersistentProcessRuleStore ruleStore;
        private readonly ICpuTopologyProvider? topologyProvider;
        private readonly CpuSelectionMigrationService migrationService;
        private readonly ILogger<ProcessRuleCreationService> logger;

        public ProcessRuleCreationService(
            IPersistentProcessRuleStore ruleStore,
            ICpuTopologyProvider? topologyProvider,
            CpuSelectionMigrationService migrationService,
            ILogger<ProcessRuleCreationService> logger)
        {
            this.ruleStore = ruleStore ?? throw new ArgumentNullException(nameof(ruleStore));
            this.topologyProvider = topologyProvider;
            this.migrationService = migrationService ?? throw new ArgumentNullException(nameof(migrationService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ProcessRuleCreationResult> SaveCurrentSettingsAsRuleAsync(
            ProcessModel process,
            IReadOnlyList<bool>? currentCoreSelection,
            ProcessMemoryPriority? currentMemoryPriority,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(process);

            var payload = new ProcessRuleCreationPayload
            {
                Priority = ShouldCaptureCurrentCpuPriority(process.Priority)
                    ? process.Priority
                    : null,
                MemoryPriority = currentMemoryPriority,
            };

            var affinityPayload = currentCoreSelection == null
                ? await this.BuildAffinityPayloadFromLegacyMaskAsync(
                    process.ProcessorAffinity,
                    "Saved current Process tab affinity",
                    cancellationToken).ConfigureAwait(false)
                : await this.BuildAffinityPayloadFromCoreSelectionAsync(
                    currentCoreSelection,
                    "Saved current Process tab affinity",
                    cancellationToken).ConfigureAwait(false);

            if (!affinityPayload.Success)
            {
                return affinityPayload;
            }

            if (affinityPayload.Payload != null)
            {
                payload = payload with
                {
                    CpuSelection = affinityPayload.Payload.CpuSelection,
                    LegacyAffinityMask = affinityPayload.Payload.LegacyAffinityMask,
                };
            }

            return await this.SaveRuleAsync(process, payload, cancellationToken).ConfigureAwait(false);
        }

        public async Task<ProcessRuleCreationResult> SaveRuleAsync(
            ProcessModel process,
            ProcessRuleCreationPayload payload,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(process);
            ArgumentNullException.ThrowIfNull(payload);

            var sanitizedPayload = SanitizePayload(payload);
            if (!sanitizedPayload.Success)
            {
                return sanitizedPayload;
            }

            payload = sanitizedPayload.Payload!;
            if (!HasActionablePayload(payload))
            {
                return ProcessRuleCreationResult.Failed("NoActionableRulePayload", NoCurrentSettingsMessage);
            }

            var rules = (await this.ruleStore.LoadAsync().ConfigureAwait(false)).ToList();
            var existingIndex = FindExistingRuleIndex(rules, process);
            var created = existingIndex < 0;
            var now = DateTime.UtcNow;
            var processName = string.IsNullOrWhiteSpace(process.Name)
                ? "process"
                : process.Name.Trim();
            var executablePath = string.IsNullOrWhiteSpace(process.ExecutablePath)
                ? null
                : process.ExecutablePath.Trim();
            var existing = created ? null : rules[existingIndex];
            var rule = new PersistentProcessRule
            {
                Id = existing?.Id ?? Guid.NewGuid().ToString("N"),
                Name = $"{processName} rule",
                IsEnabled = true,
                ProcessName = processName,
                ExecutablePath = executablePath,
                CpuSelection = payload.CpuSelection,
                LegacyAffinityMask = HasSelectionPayload(payload.CpuSelection) ? null : payload.LegacyAffinityMask,
                Priority = payload.Priority,
                MemoryPriority = payload.MemoryPriority,
                ApplyAffinityOnStart = HasSelectionPayload(payload.CpuSelection) || payload.LegacyAffinityMask.HasValue,
                ApplyPriorityOnStart = payload.Priority.HasValue,
                ApplyMemoryPriorityOnStart = payload.MemoryPriority.HasValue,
                CreatedAt = existing?.CreatedAt ?? now,
                UpdatedAt = now,
                Description = RuleDescription,
            };

            if (created)
            {
                rules.Add(rule);
            }
            else
            {
                rules[existingIndex] = rule;
            }

            cancellationToken.ThrowIfCancellationRequested();
            await this.ruleStore.SaveAsync(rules).ConfigureAwait(false);

            return new ProcessRuleCreationResult
            {
                Success = true,
                Created = created,
                Updated = !created,
                Rule = rule,
                UserMessage = created
                    ? $"Saved rule for {processName}."
                    : $"Updated saved rule for {processName}.",
            };
        }

        private static PayloadBuildResult BuildLegacyAffinityPayload(IReadOnlyList<bool> currentCoreSelection)
        {
            if (currentCoreSelection.Count == 0 || !currentCoreSelection.Any(selected => selected))
            {
                return PayloadBuildResult.Empty();
            }

            if (currentCoreSelection.Count > 64)
            {
                return PayloadBuildResult.Failed("UnsafeLegacyAffinity", UnsafeAffinityMessage);
            }

            long legacyMask = 0;
            for (var bit = 0; bit < currentCoreSelection.Count; bit++)
            {
                if (currentCoreSelection[bit])
                {
                    legacyMask |= 1L << bit;
                }
            }

            return legacyMask == 0
                ? PayloadBuildResult.Empty()
                : PayloadBuildResult.Succeeded(new ProcessRuleCreationPayload { LegacyAffinityMask = legacyMask });
        }

        private static PayloadSanitizationResult SanitizePayload(ProcessRuleCreationPayload payload)
        {
            if (payload.Priority.HasValue && ProcessPriorityGuardrails.IsBlocked(payload.Priority.Value))
            {
                return PayloadSanitizationResult.Failed(
                    "RealtimePriorityBlocked",
                    ProcessOperationUserMessages.RealtimePriorityBlocked);
            }

            var hasCpuSelection = HasSelectionPayload(payload.CpuSelection);
            var legacyMask = hasCpuSelection ? null : payload.LegacyAffinityMask;
            if (legacyMask.HasValue && legacyMask.Value == 0)
            {
                legacyMask = null;
            }

            return PayloadSanitizationResult.Succeeded(payload with
            {
                CpuSelection = hasCpuSelection ? payload.CpuSelection : null,
                LegacyAffinityMask = legacyMask,
            });
        }

        private static bool ShouldCaptureCurrentCpuPriority(ProcessPriorityClass priority) =>
            priority is ProcessPriorityClass.Idle
                or ProcessPriorityClass.BelowNormal
                or ProcessPriorityClass.AboveNormal
                or ProcessPriorityClass.High;

        private static bool HasActionablePayload(ProcessRuleCreationPayload payload) =>
            HasSelectionPayload(payload.CpuSelection) ||
            payload.LegacyAffinityMask.HasValue ||
            payload.Priority.HasValue ||
            payload.MemoryPriority.HasValue;

        private static bool HasSelectionPayload(CpuSelection? selection) =>
            selection != null &&
            (selection.CpuSetIds.Count > 0 || selection.LogicalProcessors.Count > 0);

        private static int FindExistingRuleIndex(IReadOnlyList<PersistentProcessRule> rules, ProcessModel process)
        {
            var executablePath = string.IsNullOrWhiteSpace(process.ExecutablePath)
                ? null
                : process.ExecutablePath.Trim();
            if (!string.IsNullOrWhiteSpace(executablePath))
            {
                for (var index = 0; index < rules.Count; index++)
                {
                    if (string.Equals(
                        rules[index].ExecutablePath,
                        executablePath,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return index;
                    }
                }

                var pathlessNameMatch = FindProcessNameMatchIndex(rules, process, requirePathUnavailable: true);
                return pathlessNameMatch;
            }

            return FindProcessNameMatchIndex(rules, process, requirePathUnavailable: false);
        }

        private static int FindProcessNameMatchIndex(
            IReadOnlyList<PersistentProcessRule> rules,
            ProcessModel process,
            bool requirePathUnavailable)
        {
            var processName = string.IsNullOrWhiteSpace(process.Name)
                ? null
                : process.Name.Trim();
            if (string.IsNullOrWhiteSpace(processName))
            {
                return -1;
            }

            for (var index = 0; index < rules.Count; index++)
            {
                if (requirePathUnavailable && !string.IsNullOrWhiteSpace(rules[index].ExecutablePath))
                {
                    continue;
                }

                if (string.Equals(rules[index].ProcessName, processName, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return -1;
        }

        private async Task<PayloadBuildResult> BuildAffinityPayloadFromCoreSelectionAsync(
            IReadOnlyList<bool> currentCoreSelection,
            string selectionReason,
            CancellationToken cancellationToken)
        {
            if (currentCoreSelection.Count == 0 || !currentCoreSelection.Any(selected => selected))
            {
                return PayloadBuildResult.Empty();
            }

            var selection = await this.TryMigrateCoreSelectionAsync(
                currentCoreSelection,
                selectionReason,
                cancellationToken).ConfigureAwait(false);
            if (selection != null)
            {
                return PayloadBuildResult.Succeeded(new ProcessRuleCreationPayload { CpuSelection = selection });
            }

            return BuildLegacyAffinityPayload(currentCoreSelection);
        }

        private async Task<PayloadBuildResult> BuildAffinityPayloadFromLegacyMaskAsync(
            long legacyMask,
            string selectionReason,
            CancellationToken cancellationToken)
        {
            if (legacyMask == 0)
            {
                return PayloadBuildResult.Empty();
            }

            var selection = await this.TryMigrateLegacyMaskAsync(
                legacyMask,
                selectionReason,
                cancellationToken).ConfigureAwait(false);
            if (selection != null)
            {
                return PayloadBuildResult.Succeeded(new ProcessRuleCreationPayload { CpuSelection = selection });
            }

            return PayloadBuildResult.Succeeded(new ProcessRuleCreationPayload { LegacyAffinityMask = legacyMask });
        }

        private async Task<CpuSelection?> TryMigrateCoreSelectionAsync(
            IReadOnlyList<bool> currentCoreSelection,
            string selectionReason,
            CancellationToken cancellationToken)
        {
            if (this.topologyProvider == null)
            {
                return null;
            }

            try
            {
                var topology = await this.topologyProvider.GetTopologySnapshotAsync(cancellationToken).ConfigureAwait(false);
                var migrated = this.migrationService.MigrateFromLegacyCoreMask(currentCoreSelection, topology);
                return WithSelectionReason(migrated.Selection, selectionReason);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                this.logger.LogDebug(ex, "Could not migrate current core selection to CpuSelection for saved rule");
                return null;
            }
        }

        private async Task<CpuSelection?> TryMigrateLegacyMaskAsync(
            long legacyMask,
            string selectionReason,
            CancellationToken cancellationToken)
        {
            if (this.topologyProvider == null)
            {
                return null;
            }

            try
            {
                var topology = await this.topologyProvider.GetTopologySnapshotAsync(cancellationToken).ConfigureAwait(false);
                var migrated = this.migrationService.MigrateFromLegacyAffinityMask(legacyMask, topology);
                return WithSelectionReason(migrated.Selection, selectionReason);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                this.logger.LogDebug(ex, "Could not migrate current legacy affinity mask to CpuSelection for saved rule");
                return null;
            }
        }

        private static CpuSelection? WithSelectionReason(CpuSelection? selection, string selectionReason)
        {
            if (!HasSelectionPayload(selection))
            {
                return null;
            }

            return selection! with
            {
                Metadata = selection.Metadata with
                {
                    SelectionReason = selectionReason,
                },
            };
        }

        private sealed record PayloadBuildResult(
            bool Success,
            ProcessRuleCreationPayload? Payload,
            string ErrorCode,
            string UserMessage)
        {
            public static PayloadBuildResult Empty() => new(true, null, string.Empty, string.Empty);

            public static PayloadBuildResult Succeeded(ProcessRuleCreationPayload payload) =>
                new(true, payload, string.Empty, string.Empty);

            public static PayloadBuildResult Failed(string errorCode, string userMessage) =>
                new(false, null, errorCode, userMessage);

            public static implicit operator ProcessRuleCreationResult(PayloadBuildResult result) =>
                ProcessRuleCreationResult.Failed(result.ErrorCode, result.UserMessage);
        }

        private sealed record PayloadSanitizationResult(
            bool Success,
            ProcessRuleCreationPayload? Payload,
            string ErrorCode,
            string UserMessage)
        {
            public static PayloadSanitizationResult Succeeded(ProcessRuleCreationPayload payload) =>
                new(true, payload, string.Empty, string.Empty);

            public static PayloadSanitizationResult Failed(string errorCode, string userMessage) =>
                new(false, null, errorCode, userMessage);

            public static implicit operator ProcessRuleCreationResult(PayloadSanitizationResult result) =>
                ProcessRuleCreationResult.Failed(result.ErrorCode, result.UserMessage);
        }
    }
}
