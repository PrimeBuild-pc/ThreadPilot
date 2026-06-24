namespace ThreadPilot.Models
{
    public sealed record CpuPreset
    {
        public string PresetId { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public CpuSelection Selection { get; init; } = new();

        public string Reason { get; init; } = string.Empty;

        public string? SourcePresetId { get; init; }

        public string? Warning { get; init; }

        public CpuTopologySignature? GeneratedByTopologySignature { get; init; }

        public bool IsUserEditable { get; init; } = true;

        public bool IsGenerated { get; init; } = true;

        public bool ReviewRequired { get; init; }
    }
}
