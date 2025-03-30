using System.Collections.Generic;

namespace ThreadPilot.Models
{
    /// <summary>
    /// Rule for process affinity
    /// </summary>
    public class ProcessAffinityRule
    {
        /// <summary>
        /// Process name pattern (supports wildcards * and ?)
        /// </summary>
        public string ProcessNamePattern { get; set; } = string.Empty;
        
        /// <summary>
        /// Optional process description
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// Process priority to set (if null, don't change priority)
        /// </summary>
        public ProcessPriority? Priority { get; set; }
        
        /// <summary>
        /// Affinity mask to set (if null, don't change affinity)
        /// </summary>
        public long? AffinityMask { get; set; }
        
        /// <summary>
        /// Whether this is an exclude list rather than an include list
        /// </summary>
        public bool IsExcludeList { get; set; }
        
        /// <summary>
        /// The list of cores to include or exclude
        /// </summary>
        public List<int> CoreList { get; set; } = new List<int>();
        
        /// <summary>
        /// Create affinity mask from core list
        /// </summary>
        /// <returns>Computed affinity mask</returns>
        public long ComputeAffinityMask()
        {
            long mask = 0;
            
            foreach (int core in CoreList)
            {
                mask |= 1L << core;
            }
            
            return mask;
        }
        
        /// <summary>
        /// Create a deep copy of this rule
        /// </summary>
        /// <returns>A new rule with the same properties</returns>
        public ProcessAffinityRule Clone()
        {
            var clone = new ProcessAffinityRule
            {
                ProcessNamePattern = ProcessNamePattern,
                Description = Description,
                Priority = Priority,
                AffinityMask = AffinityMask,
                IsExcludeList = IsExcludeList
            };
            
            // Copy core list
            clone.CoreList.AddRange(CoreList);
            
            return clone;
        }
    }
}