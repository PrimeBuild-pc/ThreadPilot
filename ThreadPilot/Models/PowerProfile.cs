using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ThreadPilot.Models
{
    /// <summary>
    /// Power profile class
    /// </summary>
    public class PowerProfile
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public PowerProfile()
        {
            AffinityRules = new List<ProcessAffinityRule>();
        }
        
        /// <summary>
        /// Profile name
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Profile description
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets a value indicating whether the profile is bundled with the application
        /// </summary>
        public bool IsBundled { get; set; }
        
        /// <summary>
        /// Last modified date
        /// </summary>
        public DateTime LastModified { get; set; }
        
        /// <summary>
        /// Profile file path
        /// </summary>
        [JsonIgnore]
        public string FilePath { get; set; } = string.Empty;
        
        /// <summary>
        /// Profile GUID
        /// </summary>
        public string Guid { get; set; } = string.Empty;
        
        /// <summary>
        /// Process affinity rules
        /// </summary>
        public List<ProcessAffinityRule> AffinityRules { get; set; }
        
        /// <summary>
        /// Creates a clone of the profile
        /// </summary>
        /// <returns>Clone of the profile</returns>
        public PowerProfile Clone()
        {
            var clone = new PowerProfile
            {
                Name = $"{Name} (Copy)",
                Description = Description,
                IsBundled = false,
                LastModified = DateTime.Now,
                Guid = System.Guid.NewGuid().ToString()
            };
            
            // Clone affinity rules
            foreach (var rule in AffinityRules)
            {
                clone.AffinityRules.Add(rule.Clone());
            }
            
            return clone;
        }
    }
}