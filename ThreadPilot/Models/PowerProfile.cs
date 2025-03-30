using System;
using System.Collections.Generic;

namespace ThreadPilot.Models
{
    /// <summary>
    /// Power and affinity profile
    /// </summary>
    public class PowerProfile
    {
        /// <summary>
        /// Profile name
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Profile description
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// Profile version
        /// </summary>
        public string Version { get; set; } = "1.0";
        
        /// <summary>
        /// Profile creation date
        /// </summary>
        public string CreationDate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        
        /// <summary>
        /// Profile last modified date
        /// </summary>
        public string LastModifiedDate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        
        /// <summary>
        /// Optional icon name
        /// </summary>
        public string Icon { get; set; } = string.Empty;
        
        /// <summary>
        /// Windows power scheme name to apply (if any)
        /// </summary>
        public string WindowsPowerScheme { get; set; } = string.Empty;
        
        /// <summary>
        /// Source file path (if imported)
        /// </summary>
        public string SourceFilePath { get; set; } = string.Empty;
        
        /// <summary>
        /// Import date (if imported)
        /// </summary>
        public string ImportDate { get; set; } = string.Empty;
        
        /// <summary>
        /// Whether this is a system default profile
        /// </summary>
        public bool IsSystemDefault { get; set; }
        
        /// <summary>
        /// Collection of process rules
        /// </summary>
        public List<ProcessAffinityRule> ProcessRules { get; set; } = new List<ProcessAffinityRule>();
        
        /// <summary>
        /// Create a deep copy of this profile
        /// </summary>
        /// <returns>A new profile with the same properties</returns>
        public PowerProfile Clone()
        {
            var clone = new PowerProfile
            {
                Name = Name,
                Description = Description,
                Version = Version,
                CreationDate = CreationDate,
                LastModifiedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Icon = Icon,
                WindowsPowerScheme = WindowsPowerScheme,
                SourceFilePath = SourceFilePath,
                ImportDate = ImportDate,
                IsSystemDefault = IsSystemDefault
            };
            
            // Clone process rules
            foreach (var rule in ProcessRules)
            {
                clone.ProcessRules.Add(rule.Clone());
            }
            
            return clone;
        }
        
        /// <summary>
        /// Get a compact string representation of this profile
        /// </summary>
        /// <returns>Profile summary</returns>
        public override string ToString()
        {
            return $"{Name} ({ProcessRules.Count} rules)";
        }
    }
}