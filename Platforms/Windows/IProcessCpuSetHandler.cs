namespace ThreadPilot.Platforms.Windows
{
    using System;
    using ThreadPilot.Models;

    public interface IProcessCpuSetHandler : IDisposable
    {
        uint ProcessId { get; }

        string ExecutableName { get; }

        bool ApplyCpuSetMask(long affinityMask, bool clearMask = false);

        CpuSetApplyResult ApplyCpuSetMaskDetailed(long affinityMask, bool clearMask = false);

        bool ApplyCpuSelection(CpuSelection? selection, bool clearSelection = false);

        CpuSetApplyResult ApplyCpuSelectionDetailed(CpuSelection? selection, bool clearSelection = false);

        double GetAverageCpuUsage();

        bool IsValid { get; }
    }
}
