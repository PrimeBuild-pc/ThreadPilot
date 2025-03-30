namespace ThreadPilot.Models
{
    /// <summary>
    /// Enumeration of process priorities
    /// </summary>
    public enum ProcessPriority
    {
        /// <summary>
        /// Idle process priority (lowest)
        /// </summary>
        Idle,
        
        /// <summary>
        /// Below normal process priority
        /// </summary>
        BelowNormal,
        
        /// <summary>
        /// Normal process priority (default)
        /// </summary>
        Normal,
        
        /// <summary>
        /// Above normal process priority
        /// </summary>
        AboveNormal,
        
        /// <summary>
        /// High process priority
        /// </summary>
        High,
        
        /// <summary>
        /// Real-time process priority (highest)
        /// </summary>
        RealTime
    }
}