using System.Collections.Generic;

namespace ThreadPilot.Models
{
    /// <summary>
    /// Represents a rule for applying process affinity
    /// </summary>
    public class ProcessAffinityRule
    {
        /// <summary>
        /// Gets or sets the rule ID
        /// </summary>
        public int Id { get; set; }
        
        /// <summary>
        /// Gets or sets the rule name
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Gets or sets the process name pattern to match
        /// </summary>
        public string ProcessNamePattern { get; set; }
        
        /// <summary>
        /// Gets or sets whether the rule is applied by exact match
        /// </summary>
        public bool ExactMatch { get; set; }
        
        /// <summary>
        /// Gets or sets whether the rule is case sensitive
        /// </summary>
        public bool CaseSensitive { get; set; }
        
        /// <summary>
        /// Gets or sets the list of cores to include
        /// </summary>
        public List<int> IncludedCores { get; set; } = new List<int>();
        
        /// <summary>
        /// Gets or sets the process priority to apply
        /// </summary>
        public ProcessPriority? Priority { get; set; }
        
        /// <summary>
        /// Gets or sets whether the rule is enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;
        
        /// <summary>
        /// Gets or sets whether to include child processes
        /// </summary>
        public bool IncludeChildren { get; set; }
        
        /// <summary>
        /// Gets or sets the rule category for organization
        /// </summary>
        public string Category { get; set; }
        
        /// <summary>
        /// Gets or sets the rule description
        /// </summary>
        public string Description { get; set; }
        
        /// <summary>
        /// Gets or sets whether the rule is applied only once
        /// </summary>
        public bool ApplyOnce { get; set; }
        
        /// <summary>
        /// Gets or sets whether the rule has been applied
        /// </summary>
        public bool HasBeenApplied { get; set; }
    }
}