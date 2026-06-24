namespace ThreadPilot.Models
{
    using System.Diagnostics;

    public sealed class ProcessProfileSnapshot
    {
        public int ProfileSchemaVersion { get; set; } = CpuAffinityProfileSchemaVersions.Legacy;

        public string ProcessName { get; set; } = string.Empty;

        public ProcessPriorityClass Priority { get; set; }

        public long ProcessorAffinity { get; set; }

        public CpuSelection? CpuSelection { get; set; }

        public CpuSelectionMigrationMetadata? CpuSelectionMigration { get; set; }
    }
}
