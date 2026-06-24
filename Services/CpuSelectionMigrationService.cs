namespace ThreadPilot.Services
{
    using ThreadPilot.Models;

    public sealed class CpuSelectionMigrationService
    {
        public CpuSelectionMigrationResult MigrateFromLegacyAffinityMask(
            long mask,
            CpuTopologySnapshot topology)
        {
            ArgumentNullException.ThrowIfNull(topology);

            var selection = CpuSelection.FromLegacyAffinityMask(mask, topology);
            var reviewRequired = topology.Signature.LogicalProcessorCount > 64 ||
                topology.Signature.ProcessorGroupCount > 1;

            return new CpuSelectionMigrationResult(
                selection,
                new CpuSelectionMigrationMetadata
                {
                    CreatedFromLegacyAffinityMask = true,
                    ReviewRequired = reviewRequired,
                    MigrationConfidence = reviewRequired ? "Medium" : "High",
                    Reason = reviewRequired
                        ? "Migrated from a legacy affinity mask on a topology that may not be fully represented by legacy masks."
                        : "Migrated from a legacy affinity mask.",
                    TopologySignature = topology.Signature,
                    SourceLegacyAffinityMask = mask,
                });
        }

        public CpuSelectionMigrationResult MigrateFromLegacyCoreMask(
            IReadOnlyList<bool> coreMask,
            CpuTopologySnapshot topology)
        {
            ArgumentNullException.ThrowIfNull(coreMask);
            ArgumentNullException.ThrowIfNull(topology);

            var orderedProcessors = topology.LogicalProcessors
                .OrderBy(processor => processor.GlobalIndex)
                .ThenBy(processor => processor.Group)
                .ThenBy(processor => processor.LogicalProcessorNumber)
                .ToList();
            var selectedProcessors = orderedProcessors
                .Take(Math.Min(coreMask.Count, orderedProcessors.Count))
                .Where((_, index) => coreMask[index])
                .ToList();
            var reviewRequired = coreMask.Count != orderedProcessors.Count;
            var selection = CpuSelection.FromProcessors(
                selectedProcessors,
                topology,
                "Migrated from legacy core mask");

            return new CpuSelectionMigrationResult(
                selection,
                new CpuSelectionMigrationMetadata
                {
                    CreatedFromLegacyCoreMask = true,
                    ReviewRequired = reviewRequired,
                    MigrationConfidence = reviewRequired ? "Medium" : "High",
                    Reason = reviewRequired
                        ? "Migrated from a legacy core mask whose length differs from the current topology."
                        : "Migrated from a legacy core mask.",
                    TopologySignature = topology.Signature,
                });
        }

        public long? BuildLegacyAffinityMaskIfRepresentable(CpuSelection selection) =>
            CpuSelection.ToLegacyAffinityMaskOrNull(selection);

        public bool ShouldRequireReview(
            CpuSelection selection,
            CpuTopologySignature? savedSignature,
            CpuTopologySnapshot currentTopology)
        {
            ArgumentNullException.ThrowIfNull(selection);
            ArgumentNullException.ThrowIfNull(currentTopology);

            if (savedSignature == null || savedSignature != currentTopology.Signature)
            {
                return true;
            }

            var currentProcessors = currentTopology.LogicalProcessors.ToHashSet();
            return selection.LogicalProcessors.Any(processor => !currentProcessors.Contains(processor));
        }

        public ProcessProfileSnapshot MigrateProcessProfile(
            ProcessProfileSnapshot profile,
            CpuTopologySnapshot topology)
        {
            ArgumentNullException.ThrowIfNull(profile);
            ArgumentNullException.ThrowIfNull(topology);

            if (profile.CpuSelection != null)
            {
                profile.ProfileSchemaVersion = CpuAffinityProfileSchemaVersions.CpuSelection;
                profile.CpuSelectionMigration ??= new CpuSelectionMigrationMetadata
                {
                    ReviewRequired = this.ShouldRequireReview(
                        profile.CpuSelection,
                        profile.CpuSelection.Metadata.TopologySignature,
                        topology),
                    MigrationConfidence = "High",
                    Reason = "Profile already contains a CpuSelection.",
                    TopologySignature = profile.CpuSelection.Metadata.TopologySignature,
                    SourceLegacyAffinityMask = profile.ProcessorAffinity,
                };
                return profile;
            }

            var migrated = this.MigrateFromLegacyAffinityMask(profile.ProcessorAffinity, topology);
            profile.ProfileSchemaVersion = CpuAffinityProfileSchemaVersions.CpuSelection;
            profile.CpuSelection = migrated.Selection;
            profile.CpuSelectionMigration = migrated.Metadata;
            return profile;
        }

        public ProcessProfileSnapshot PrepareProcessProfileForSave(
            ProcessProfileSnapshot profile,
            CpuTopologySnapshot topology)
        {
            ArgumentNullException.ThrowIfNull(profile);
            ArgumentNullException.ThrowIfNull(topology);

            if (profile.CpuSelection == null)
            {
                this.MigrateProcessProfile(profile, topology);
            }

            profile.ProfileSchemaVersion = CpuAffinityProfileSchemaVersions.CpuSelection;
            if (profile.CpuSelection != null)
            {
                var legacyMask = this.BuildLegacyAffinityMaskIfRepresentable(profile.CpuSelection);
                if (legacyMask.HasValue)
                {
                    profile.ProcessorAffinity = legacyMask.Value;
                }
            }

            profile.CpuSelectionMigration ??= new CpuSelectionMigrationMetadata
            {
                MigrationConfidence = "High",
                Reason = "Saved with CpuSelection profile schema.",
                TopologySignature = topology.Signature,
                SourceLegacyAffinityMask = profile.ProcessorAffinity,
            };

            return profile;
        }
    }
}
