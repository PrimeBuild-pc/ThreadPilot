using System;
using System.Collections.Generic;

namespace ThreadPilot.Models
{
    /// <summary>
    /// Represents a power profile configuration
    /// </summary>
    public class PowerProfile
    {
        /// <summary>
        /// Gets or sets the profile name
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the profile description
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the profile category
        /// </summary>
        public string Category { get; set; } = "Custom";
        
        /// <summary>
        /// Gets or sets the profile creation date
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        /// <summary>
        /// Gets or sets a value indicating whether the profile is a system default
        /// </summary>
        public bool IsSystemDefault { get; set; }
        
        /// <summary>
        /// Gets or sets the Windows power plan to use
        /// </summary>
        public string WindowsPowerPlan { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets a value indicating whether to park unused cores
        /// </summary>
        public bool ParkUnusedCores { get; set; }
        
        /// <summary>
        /// Gets or sets the maximum number of active cores (0 = all cores)
        /// </summary>
        public int MaxActiveCores { get; set; }
        
        /// <summary>
        /// Gets or sets process affinity rules
        /// </summary>
        public List<ProcessAffinityRule> AffinityRules { get; set; } = new List<ProcessAffinityRule>();
    }
}