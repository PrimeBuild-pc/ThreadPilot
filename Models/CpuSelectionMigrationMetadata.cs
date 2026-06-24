namespace ThreadPilot.Models
{
    public sealed record CpuSelectionMigrationMetadata
    {
        public bool CreatedFromLegacyAffinityMask { get; init; }

        public bool CreatedFromLegacyCoreMask { get; init; }

        public bool ReviewRequired { get; init; }

        public string MigrationConfidence { get; init; } = string.Empty;

        public string Reason { get; init; } = string.Empty;

        public CpuTopologySignature? TopologySignature { get; init; }

        public long? SourceLegacyAffinityMask { get; init; }
    }
}
