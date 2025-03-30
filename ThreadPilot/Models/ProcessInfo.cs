using System;
using System.Collections.Generic;

namespace ThreadPilot.Models
{
    /// <summary>
    /// Process information class
    /// </summary>
    public class ProcessInfo
    {
        /// <summary>
        /// Process ID
        /// </summary>
        public int Id { get; set; }
        
        /// <summary>
        /// Process name
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Process description
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// Process CPU usage percentage
        /// </summary>
        public float CpuUsagePercentage { get; set; }
        
        /// <summary>
        /// Process memory usage in MB
        /// </summary>
        public long MemoryUsageMB { get; set; }
        
        /// <summary>
        /// Process thread count
        /// </summary>
        public int ThreadCount { get; set; }
        
        /// <summary>
        /// Process priority
        /// </summary>
        public ProcessPriority Priority { get; set; }
        
        /// <summary>
        /// Process affinity mask (each bit represents a core)
        /// </summary>
        public long Affinity { get; set; }
        
        /// <summary>
        /// Gets the list of CPU cores associated with the process affinity
        /// </summary>
        /// <returns>List of core indices</returns>
        public IEnumerable<int> GetAffinityCores()
        {
            var coreIndices = new List<int>();
            
            for (int i = 0; i < 64; i++)
            {
                if ((Affinity & (1L << i)) != 0)
                {
                    coreIndices.Add(i);
                }
            }
            
            return coreIndices;
        }
        
        /// <summary>
        /// Sets the process affinity for the specified cores
        /// </summary>
        /// <param name="coreIndices">Core indices</param>
        public void SetAffinity(IEnumerable<int> coreIndices)
        {
            long affinityMask = 0;
            
            foreach (var coreIndex in coreIndices)
            {
                affinityMask |= (1L << coreIndex);
            }
            
            Affinity = affinityMask;
        }
    }
}