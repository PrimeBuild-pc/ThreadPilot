using System;
using System.Collections.Generic;

namespace ThreadPilot.Models
{
    /// <summary>
    /// Represents a collection of power profiles and process affinity rules
    /// that can be applied together as a performance optimization bundle
    /// </summary>
    public class BundledPowerProfile
    {
        /// <summary>
        /// Bundle name
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Bundle description
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// Category/tag for the bundle (e.g., Gaming, Productivity, etc.)
        /// </summary>
        public string Category { get; set; } = string.Empty;
        
        /// <summary>
        /// The power profile to apply as part of this bundle
        /// </summary>
        public PowerProfile PowerProfile { get; set; }
        
        /// <summary>
        /// List of process affinity rules to apply as part of this bundle
        /// </summary>
        public List<ProcessAffinityRule> AffinityRules { get; set; } = new List<ProcessAffinityRule>();
        
        /// <summary>
        /// Whether to apply this bundle automatically on application startup
        /// </summary>
        public bool ApplyOnStartup { get; set; }
        
        /// <summary>
        /// Whether this bundle is currently active
        /// </summary>
        public bool IsActive { get; set; }
        
        /// <summary>
        /// When this bundle was last applied
        /// </summary>
        public DateTime LastApplied { get; set; }
        
        /// <summary>
        /// Optional list of process names that should trigger this bundle when launched
        /// </summary>
        public List<string> TriggerProcessNames { get; set; } = new List<string>();
        
        /// <summary>
        /// Whether to restore previous settings when trigger processes exit
        /// </summary>
        public bool RestoreOnTriggerExit { get; set; }
        
        /// <summary>
        /// The author/creator of the bundle
        /// </summary>
        public string Author { get; set; } = string.Empty;
        
        /// <summary>
        /// Creation date of the bundle
        /// </summary>
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        
        /// <summary>
        /// Last modified date of the bundle
        /// </summary>
        public DateTime ModifiedDate { get; set; } = DateTime.Now;
        
        /// <summary>
        /// Optional custom registry tweaks to apply as part of this bundle
        /// </summary>
        public Dictionary<string, string> RegistryTweaks { get; set; } = new Dictionary<string, string>();
        
        /// <summary>
        /// Whether to disable core parking when this bundle is applied
        /// </summary>
        public bool DisableCoreParking { get; set; }
        
        /// <summary>
        /// Processor performance boost mode to set when this bundle is applied
        /// </summary>
        public int ProcessorBoostMode { get; set; } = 1; // Default to Enabled
        
        /// <summary>
        /// System responsiveness value to set when this bundle is applied
        /// </summary>
        public int SystemResponsiveness { get; set; } = 20; // Windows default
        
        /// <summary>
        /// Network throttling index to set when this bundle is applied
        /// </summary>
        public int NetworkThrottlingIndex { get; set; } = 10;
        
        /// <summary>
        /// Check if this bundle should be automatically applied for a running process
        /// </summary>
        /// <param name="processName">Process name to check</param>
        /// <returns>True if the bundle should be applied</returns>
        public bool ShouldApplyForProcess(string processName)
        {
            if (string.IsNullOrEmpty(processName))
            {
                return false;
            }
            
            foreach (string trigger in TriggerProcessNames)
            {
                if (string.Equals(trigger, processName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            
            return false;
        }
    }
}