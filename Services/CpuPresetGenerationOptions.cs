namespace ThreadPilot.Services
{
    public sealed record CpuPresetGenerationOptions
    {
        public bool ExcludeCpu0ForGaming { get; init; } = true;

        public IReadOnlySet<string> DeletedGeneratedPresetIds { get; init; } =
            new HashSet<string>(StringComparer.Ordinal);

        public bool IncludeExperimentalPresets { get; init; }
    }
}
