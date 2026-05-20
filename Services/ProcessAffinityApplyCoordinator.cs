/*
 * ThreadPilot - process tab affinity apply coordination.
 */
namespace ThreadPilot.Services
{
    using Microsoft.Extensions.Logging;
    using ThreadPilot.Models;

    public interface IProcessAffinityApplyCoordinator
    {
        Task<AffinityApplyResult> ApplyCoreMaskAsync(
            ProcessModel process,
            CoreMask coreMask,
            CancellationToken cancellationToken = default);

        Task<AffinityApplyResult> ApplyCoreSelectionAsync(
            ProcessModel process,
            IReadOnlyList<bool> boolMask,
            string selectionReason,
            CancellationToken cancellationToken = default);
    }

    public sealed class ProcessAffinityApplyCoordinator : IProcessAffinityApplyCoordinator
    {
        private readonly IAffinityApplyService affinityApplyService;
        private readonly ICpuTopologyProvider? cpuTopologyProvider;
        private readonly CpuSelectionMigrationService cpuSelectionMigrationService;
        private readonly ILogger<ProcessAffinityApplyCoordinator> logger;

        public ProcessAffinityApplyCoordinator(
            IAffinityApplyService affinityApplyService,
            ICpuTopologyProvider? cpuTopologyProvider,
            CpuSelectionMigrationService cpuSelectionMigrationService,
            ILogger<ProcessAffinityApplyCoordinator> logger)
        {
            this.affinityApplyService = affinityApplyService ?? throw new ArgumentNullException(nameof(affinityApplyService));
            this.cpuTopologyProvider = cpuTopologyProvider;
            this.cpuSelectionMigrationService = cpuSelectionMigrationService ?? throw new ArgumentNullException(nameof(cpuSelectionMigrationService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<AffinityApplyResult> ApplyCoreMaskAsync(
            ProcessModel process,
            CoreMask coreMask,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(coreMask);

            if (HasSelectionPayload(coreMask.CpuSelection))
            {
                return this.affinityApplyService.ApplyAsync(process, coreMask.CpuSelection!);
            }

            return this.ApplyCoreSelectionAsync(
                process,
                coreMask.BoolMask.ToList(),
                $"Manual Process tab mask '{coreMask.Name}'",
                cancellationToken);
        }

        public async Task<AffinityApplyResult> ApplyCoreSelectionAsync(
            ProcessModel process,
            IReadOnlyList<bool> boolMask,
            string selectionReason,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(process);
            ArgumentNullException.ThrowIfNull(boolMask);

            if (boolMask.Count == 0 || !boolMask.Any(selected => selected))
            {
                return AffinityApplyResult.Failed(
                    AffinityApplyErrorCodes.InvalidSelection,
                    ProcessOperationUserMessages.InvalidTopology,
                    "Manual CPU selection is empty.",
                    isInvalidTopology: true,
                    failureReason: AffinityApplyFailureReason.InvalidMask);
            }

            var migratedSelection = await this.TryMigrateToCpuSelectionAsync(
                boolMask,
                selectionReason,
                cancellationToken).ConfigureAwait(false);
            if (migratedSelection != null)
            {
                return await this.affinityApplyService.ApplyAsync(process, migratedSelection).ConfigureAwait(false);
            }

            if (!TryBuildSafeLegacyMask(boolMask, out var legacyMask, out var legacyFailure))
            {
                return legacyFailure;
            }

            return await this.affinityApplyService.ApplyAsync(process, legacyMask).ConfigureAwait(false);
        }

        private async Task<CpuSelection?> TryMigrateToCpuSelectionAsync(
            IReadOnlyList<bool> boolMask,
            string selectionReason,
            CancellationToken cancellationToken)
        {
            if (this.cpuTopologyProvider == null)
            {
                return null;
            }

            try
            {
                var topology = await this.cpuTopologyProvider.GetTopologySnapshotAsync(cancellationToken).ConfigureAwait(false);
                var migrated = this.cpuSelectionMigrationService.MigrateFromLegacyCoreMask(boolMask, topology);
                if (!HasSelectionPayload(migrated.Selection))
                {
                    return null;
                }

                return migrated.Selection with
                {
                    Metadata = migrated.Selection.Metadata with
                    {
                        SelectionReason = string.IsNullOrWhiteSpace(selectionReason)
                            ? migrated.Selection.Metadata.SelectionReason
                            : selectionReason,
                    },
                };
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                this.logger.LogDebug(ex, "Could not migrate manual Process tab CPU selection to CpuSelection");
                return null;
            }
        }

        private static bool HasSelectionPayload(CpuSelection? selection) =>
            selection != null &&
            (selection.CpuSetIds.Count > 0 || selection.LogicalProcessors.Count > 0);

        private static bool TryBuildSafeLegacyMask(
            IReadOnlyList<bool> boolMask,
            out long legacyMask,
            out AffinityApplyResult failure)
        {
            legacyMask = 0;
            failure = default!;

            if (boolMask.Count > 64)
            {
                failure = AffinityApplyResult.Failed(
                    AffinityApplyErrorCodes.LegacyFallbackUnsafe,
                    ProcessOperationUserMessages.LegacyFallbackBlocked,
                    "Manual CPU selection exceeds the legacy single-group 64-bit affinity mask.",
                    isLegacyFallbackBlocked: true,
                    failureReason: AffinityApplyFailureReason.InvalidMask);
                return false;
            }

            for (var bit = 0; bit < boolMask.Count; bit++)
            {
                if (boolMask[bit])
                {
                    legacyMask |= 1L << bit;
                }
            }

            if (legacyMask == 0)
            {
                failure = AffinityApplyResult.Failed(
                    AffinityApplyErrorCodes.InvalidSelection,
                    ProcessOperationUserMessages.InvalidTopology,
                    "Manual CPU selection does not contain any enabled CPUs.",
                    isInvalidTopology: true,
                    failureReason: AffinityApplyFailureReason.InvalidMask);
                return false;
            }

            return true;
        }
    }
}
