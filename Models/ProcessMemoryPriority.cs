/*
 * ThreadPilot - process memory priority model.
 */
namespace ThreadPilot.Models
{
    /// <summary>
    /// Documented Windows process memory priority levels.
    /// CPU priority influences CPU scheduling; memory priority influences how aggressively
    /// Windows may reclaim or page a process's memory under pressure.
    /// </summary>
    public enum ProcessMemoryPriority
    {
        VeryLow = 1,
        Low = 2,
        Medium = 3,
        BelowNormal = 4,
        Normal = 5,
    }
}
