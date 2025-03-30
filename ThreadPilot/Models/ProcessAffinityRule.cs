using System;

namespace ThreadPilot.Models
{
    /// <summary>
    /// Model class for process affinity rules
    /// </summary>
    public class ProcessAffinityRule
    {
        /// <summary>
        /// Rule ID
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();
        
        /// <summary>
        /// Process name pattern (supports wildcards)
        /// </summary>
        public string ProcessNamePattern { get; set; } = string.Empty;
        
        /// <summary>
        /// Process affinity mask
        /// </summary>
        public long AffinityMask { get; set; }
        
        /// <summary>
        /// Process priority
        /// </summary>
        public int Priority { get; set; }
        
        /// <summary>
        /// Rule description
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// Indicates if the rule is enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;
        
        /// <summary>
        /// Rule creation date
        /// </summary>
        public DateTime CreatedOn { get; set; } = DateTime.Now;
        
        /// <summary>
        /// Rule last modified date
        /// </summary>
        public DateTime ModifiedOn { get; set; } = DateTime.Now;
    }
}