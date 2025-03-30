namespace ThreadPilot.Models
{
    /// <summary>
    /// Represents the priority levels for processes
    /// </summary>
    public enum ProcessPriority
    {
        /// <summary>
        /// Idle priority level
        /// </summary>
        Idle = 0,
        
        /// <summary>
        /// Below normal priority level
        /// </summary>
        BelowNormal = 1,
        
        /// <summary>
        /// Normal priority level
        /// </summary>
        Normal = 2,
        
        /// <summary>
        /// Above normal priority level
        /// </summary>
        AboveNormal = 3,
        
        /// <summary>
        /// High priority level
        /// </summary>
        High = 4,
        
        /// <summary>
        /// Realtime priority level
        /// </summary>
        Realtime = 5
    }
}