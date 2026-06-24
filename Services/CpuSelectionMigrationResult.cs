namespace ThreadPilot.Services
{
    using ThreadPilot.Models;

    public sealed record CpuSelectionMigrationResult(
        CpuSelection Selection,
        CpuSelectionMigrationMetadata Metadata);
}
