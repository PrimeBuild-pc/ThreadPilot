namespace ThreadPilot.Models
{
    /// <summary>
    /// Process priority levels
    /// </summary>
    public enum ProcessPriority
    {
        /// <summary>
        /// Lowest priority (idle)
        /// </summary>
        Idle = 0,
        
        /// <summary>
        /// Below normal priority
        /// </summary>
        BelowNormal = 1,
        
        /// <summary>
        /// Normal priority
        /// </summary>
        Normal = 2,
        
        /// <summary>
        /// Above normal priority
        /// </summary>
        AboveNormal = 3,
        
        /// <summary>
        /// High priority
        /// </summary>
        High = 4,
        
        /// <summary>
        /// Real-time priority (use with caution)
        /// </summary>
        RealTime = 5
    }
}