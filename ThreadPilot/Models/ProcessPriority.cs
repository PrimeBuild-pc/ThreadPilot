using System;

namespace ThreadPilot.Models
{
    /// <summary>
    /// Process priority enum
    /// </summary>
    public enum ProcessPriority
    {
        /// <summary>
        /// Idle priority
        /// </summary>
        Idle,
        
        /// <summary>
        /// Below normal priority
        /// </summary>
        BelowNormal,
        
        /// <summary>
        /// Normal priority
        /// </summary>
        Normal,
        
        /// <summary>
        /// Above normal priority
        /// </summary>
        AboveNormal,
        
        /// <summary>
        /// High priority
        /// </summary>
        High,
        
        /// <summary>
        /// Realtime priority
        /// </summary>
        Realtime
    }
}