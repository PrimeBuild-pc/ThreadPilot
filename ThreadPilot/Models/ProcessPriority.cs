namespace ThreadPilot.Models
{
    /// <summary>
    /// Represents the process priority level
    /// Maps to the .NET ProcessPriorityClass enum
    /// </summary>
    public enum ProcessPriority
    {
        /// <summary>
        /// Idle process priority
        /// </summary>
        Idle = 64,
        
        /// <summary>
        /// Below normal process priority
        /// </summary>
        BelowNormal = 16384,
        
        /// <summary>
        /// Normal process priority
        /// </summary>
        Normal = 32,
        
        /// <summary>
        /// Above normal process priority
        /// </summary>
        AboveNormal = 32768,
        
        /// <summary>
        /// High process priority
        /// </summary>
        High = 128,
        
        /// <summary>
        /// Realtime process priority (use with caution)
        /// </summary>
        RealTime = 256
    }
}