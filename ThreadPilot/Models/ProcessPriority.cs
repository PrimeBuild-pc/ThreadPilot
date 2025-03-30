namespace ThreadPilot.Models
{
    /// <summary>
    /// Represents Windows process priority levels
    /// Maps to the actual Windows process priority values
    /// </summary>
    public enum ProcessPriority
    {
        Idle = 64,
        BelowNormal = 16384,
        Normal = 32,
        AboveNormal = 32768,
        High = 128,
        Realtime = 256
    }
}