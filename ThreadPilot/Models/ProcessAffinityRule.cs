using System;
using System.Collections.Generic;

namespace ThreadPilot.Models
{
    /// <summary>
    /// Process affinity rule
    /// </summary>
    public class ProcessAffinityRule
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public ProcessAffinityRule()
        {
            CoreIndices = new List<int>();
        }
        
        /// <summary>
        /// Rule name
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Process name pattern (regular expression)
        /// </summary>
        public string ProcessNamePattern { get; set; } = string.Empty;
        
        /// <summary>
        /// Core indices
        /// </summary>
        public List<int> CoreIndices { get; set; }
        
        /// <summary>
        /// Process priority
        /// </summary>
        public ProcessPriority ProcessPriority { get; set; } = ProcessPriority.Normal;
        
        /// <summary>
        /// Gets or sets a value indicating whether the rule is enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;
        
        /// <summary>
        /// Creates a clone of the rule
        /// </summary>
        /// <returns>Clone of the rule</returns>
        public ProcessAffinityRule Clone()
        {
            var clone = new ProcessAffinityRule
            {
                Name = Name,
                ProcessNamePattern = ProcessNamePattern,
                ProcessPriority = ProcessPriority,
                IsEnabled = IsEnabled
            };
            
            // Clone core indices
            foreach (var coreIndex in CoreIndices)
            {
                clone.CoreIndices.Add(coreIndex);
            }
            
            return clone;
        }
    }
}