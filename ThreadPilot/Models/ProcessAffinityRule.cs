using System.Collections.Generic;

namespace ThreadPilot.Models
{
    /// <summary>
    /// Represents a rule for automatically setting process affinity and priority
    /// </summary>
    public class ProcessAffinityRule
    {
        /// <summary>
        /// Rule name/identifier
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Process executable name to match (without .exe extension)
        /// </summary>
        public string ProcessName { get; set; } = string.Empty;
        
        /// <summary>
        /// Optional process file path for more specific matching
        /// </summary>
        public string ProcessPath { get; set; } = string.Empty;
        
        /// <summary>
        /// Whether to use process path for matching in addition to name
        /// </summary>
        public bool MatchByPath { get; set; }
        
        /// <summary>
        /// Whether this rule is enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;
        
        /// <summary>
        /// List of core indices to assign to matched processes
        /// </summary>
        public List<int> AssignedCores { get; set; } = new List<int>();
        
        /// <summary>
        /// Process priority to set for matched processes
        /// </summary>
        public ProcessPriority Priority { get; set; } = ProcessPriority.Normal;
        
        /// <summary>
        /// Whether to set process priority
        /// </summary>
        public bool SetPriority { get; set; } = true;
        
        /// <summary>
        /// Whether to set process affinity
        /// </summary>
        public bool SetAffinity { get; set; } = true;
        
        /// <summary>
        /// Whether to apply rule on process startup
        /// </summary>
        public bool ApplyOnStartup { get; set; } = true;
        
        /// <summary>
        /// Whether to reapply rule periodically
        /// </summary>
        public bool ReapplyPeriodically { get; set; }
        
        /// <summary>
        /// Reapplication interval in seconds (if periodic reapplication is enabled)
        /// </summary>
        public int ReapplyIntervalSeconds { get; set; } = 60;
        
        /// <summary>
        /// Check if a process matches this rule
        /// </summary>
        /// <param name="processInfo">Process to check</param>
        /// <returns>True if process matches this rule</returns>
        public bool MatchesProcess(ProcessInfo processInfo)
        {
            // First check if rule is enabled
            if (!IsEnabled)
            {
                return false;
            }
            
            // Check if process name matches (case-insensitive)
            bool nameMatches = !string.IsNullOrEmpty(ProcessName) && 
                              processInfo.Name.Equals(ProcessName, System.StringComparison.OrdinalIgnoreCase);
            
            // If not matching by path, name match is sufficient
            if (!MatchByPath)
            {
                return nameMatches;
            }
            
            // If matching by path, check both name and path
            bool pathMatches = !string.IsNullOrEmpty(ProcessPath) && 
                              processInfo.Path.StartsWith(ProcessPath, System.StringComparison.OrdinalIgnoreCase);
            
            return nameMatches && pathMatches;
        }
        
        /// <summary>
        /// Gets the CPU affinity mask based on the assigned cores
        /// </summary>
        public long GetAffinityMask()
        {
            long mask = 0;
            
            foreach (int coreIndex in AssignedCores)
            {
                if (coreIndex >= 0 && coreIndex < 64)
                {
                    mask |= (1L << coreIndex);
                }
            }
            
            return mask;
        }
    }
}