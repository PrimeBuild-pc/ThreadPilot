namespace ThreadPilot.Models
{
    /// <summary>
    /// Process affinity rule
    /// </summary>
    public class ProcessAffinityRule
    {
        /// <summary>
        /// Rule ID
        /// </summary>
        public int Id { get; set; }
        
        /// <summary>
        /// Rule name
        /// </summary>
        public string? Name { get; set; }
        
        /// <summary>
        /// Process name pattern
        /// </summary>
        public string? ProcessNamePattern { get; set; }
        
        /// <summary>
        /// Process path pattern
        /// </summary>
        public string? ProcessPathPattern { get; set; }
        
        /// <summary>
        /// CPU affinity mask (used to determine which cores the process can use)
        /// </summary>
        public long AffinityMask { get; set; }
        
        /// <summary>
        /// Process priority
        /// </summary>
        public ProcessPriority Priority { get; set; }
        
        /// <summary>
        /// Is the rule enabled
        /// </summary>
        public bool IsEnabled { get; set; }
        
        /// <summary>
        /// Rule priority (higher priority rules will be applied first)
        /// </summary>
        public int RulePriority { get; set; }
    }
}