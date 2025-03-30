using System;

namespace ThreadPilot.Models
{
    /// <summary>
    /// Represents a process affinity rule
    /// </summary>
    public class ProcessAffinityRule
    {
        /// <summary>
        /// Gets or sets the rule ID
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();
        
        /// <summary>
        /// Gets or sets the rule name
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Gets or sets the process name pattern (supports wildcards * and ?)
        /// </summary>
        public string ProcessNamePattern { get; set; }
        
        /// <summary>
        /// Gets or sets the process affinity mask
        /// </summary>
        public long Affinity { get; set; }
        
        /// <summary>
        /// Gets or sets the process priority
        /// </summary>
        public ProcessPriority Priority { get; set; } = ProcessPriority.Normal;
        
        /// <summary>
        /// Gets or sets a value indicating whether to apply the priority setting
        /// </summary>
        public bool ApplyPriority { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether to apply the affinity setting
        /// </summary>
        public bool ApplyAffinity { get; set; } = true;
        
        /// <summary>
        /// Gets or sets a value indicating whether to apply the rule automatically when the process starts
        /// </summary>
        public bool ApplyOnProcessStart { get; set; } = true;
        
        /// <summary>
        /// Gets or sets a value indicating whether the rule is enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;
        
        /// <summary>
        /// Gets or sets a value indicating whether to use performance cores for this process
        /// </summary>
        public bool UsePerformanceCores { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether to use efficiency cores for this process
        /// </summary>
        public bool UseEfficiencyCores { get; set; }
        
        /// <summary>
        /// Gets or sets the creation date
        /// </summary>
        public DateTime CreationDate { get; set; } = DateTime.Now;
        
        /// <summary>
        /// Gets or sets the last modified date
        /// </summary>
        public DateTime LastModifiedDate { get; set; } = DateTime.Now;
        
        /// <summary>
        /// Gets or sets the number of cores to use (0 = all)
        /// </summary>
        public int CoreCount { get; set; }
        
        /// <summary>
        /// Creates a clone of the process affinity rule
        /// </summary>
        /// <returns>A new instance of the process affinity rule with the same values</returns>
        public ProcessAffinityRule Clone()
        {
            return new ProcessAffinityRule
            {
                Name = Name,
                ProcessNamePattern = ProcessNamePattern,
                Affinity = Affinity,
                Priority = Priority,
                ApplyPriority = ApplyPriority,
                ApplyAffinity = ApplyAffinity,
                ApplyOnProcessStart = ApplyOnProcessStart,
                IsEnabled = IsEnabled,
                UsePerformanceCores = UsePerformanceCores,
                UseEfficiencyCores = UseEfficiencyCores,
                CreationDate = CreationDate,
                LastModifiedDate = LastModifiedDate,
                CoreCount = CoreCount
            };
        }
    }
}