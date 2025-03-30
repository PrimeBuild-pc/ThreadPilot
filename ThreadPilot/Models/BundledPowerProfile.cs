using System.Collections.Generic;

namespace ThreadPilot.Models
{
    /// <summary>
    /// Bundled power profile
    /// </summary>
    public class BundledPowerProfile
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public BundledPowerProfile()
        {
            ProcessAffinityRules = new List<ProcessAffinityRule>();
        }
        
        /// <summary>
        /// Profile ID
        /// </summary>
        public int Id { get; set; }
        
        /// <summary>
        /// Profile name
        /// </summary>
        public string? Name { get; set; }
        
        /// <summary>
        /// Profile description
        /// </summary>
        public string? Description { get; set; }
        
        /// <summary>
        /// Is the profile enabled
        /// </summary>
        public bool IsEnabled { get; set; }
        
        /// <summary>
        /// Windows power profile GUID
        /// </summary>
        public string? WindowsPowerProfileGuid { get; set; }
        
        /// <summary>
        /// Windows power profile name
        /// </summary>
        public string? WindowsPowerProfileName { get; set; }
        
        /// <summary>
        /// Power profile file path
        /// </summary>
        public string? PowerProfileFilePath { get; set; }
        
        /// <summary>
        /// Process affinity rules
        /// </summary>
        public List<ProcessAffinityRule> ProcessAffinityRules { get; }
        
        /// <summary>
        /// Should unpark all CPU cores
        /// </summary>
        public bool ShouldUnparkAllCores { get; set; }
    }
}