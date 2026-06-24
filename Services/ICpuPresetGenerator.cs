namespace ThreadPilot.Services
{
    using ThreadPilot.Models;

    public interface ICpuPresetGenerator
    {
        IReadOnlyList<CpuPreset> Generate(
            CpuTopologySnapshot topology,
            CpuPresetGenerationOptions? options = null);
    }
}
