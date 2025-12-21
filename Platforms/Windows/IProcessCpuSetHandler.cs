using System;

namespace ThreadPilot.Platforms.Windows
{
    /// <summary>
    /// Interface for handling CPU Set operations on a specific process
    /// </summary>
    public interface IProcessCpuSetHandler : IDisposable
    {
        /// <summary>
        /// Gets the process ID this handler manages
        /// </summary>
        uint ProcessId { get; }

        /// <summary>
        /// Gets the executable name
        /// </summary>
        string ExecutableName { get; }

        /// <summary>
        /// Applies a CPU affinity mask to the process using CPU Sets
        /// </summary>
        /// <param name="affinityMask">The affinity mask where each bit represents a logical processor</param>
        /// <param name="clearMask">If true, clears the CPU Set (allows all cores); if false, applies the mask</param>
        /// <returns>True if the operation succeeded, false otherwise</returns>
        bool ApplyCpuSetMask(long affinityMask, bool clearMask = false);

        /// <summary>
        /// Gets the average CPU usage for this process
        /// </summary>
        /// <returns>CPU usage percentage (0-1 range), or -1 if unavailable</returns>
        double GetAverageCpuUsage();

        /// <summary>
        /// Checks if the handler has valid handles to the process
        /// </summary>
        bool IsValid { get; }
    }
}
