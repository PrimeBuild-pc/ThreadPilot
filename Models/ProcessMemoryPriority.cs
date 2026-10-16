/*
 * ThreadPilot - process memory priority model.
 */
namespace ThreadPilot.Models
{
    public enum ProcessMemoryPriority
    {
        VeryLow = 1,
        Low = 2,
        Medium = 3,
        BelowNormal = 4,
        Normal = 5,
    }

    public enum ProcessIoPriority
    {
        VeryLow = 0,
        Low = 1,
        Normal = 2,
        High = 3,
    }

    // Windows 11 "efficiency mode" (EcoQoS). Off forces full speed (HighQoS); SystemManaged lets Windows decide.
    public enum ProcessEfficiencyMode
    {
        SystemManaged = 0,
        On = 1,
        Off = 2,
    }
}
