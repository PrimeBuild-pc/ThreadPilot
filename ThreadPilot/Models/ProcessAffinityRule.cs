namespace ThreadPilot.Models
{
    /// <summary>
    /// Represents a process affinity rule
    /// </summary>
    public class ProcessAffinityRule
    {
        /// <summary>
        /// Gets or sets the process name pattern (supports wildcard *)
        /// </summary>
        public string ProcessNamePattern { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the CPU affinity mask (bit field)
        /// </summary>
        public long AffinityMask { get; set; }
        
        /// <summary>
        /// Gets or sets the process priority
        /// </summary>
        public ProcessPriority Priority { get; set; } = ProcessPriority.Normal;
        
        /// <summary>
        /// Gets or sets a value indicating whether this rule is enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;
        
        /// <summary>
        /// Gets or sets the description
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// Creates a string representation of the rule
        /// </summary>
        /// <returns>A string representation</returns>
        public override string ToString()
        {
            return $"{ProcessNamePattern} -> {AffinityMask:X} ({Priority})";
        }
    }
}