using System;
using System.Collections.Generic;

namespace ThreadPilot.Models
{
    /// <summary>
    /// Information about a running process
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
        /// Process executable path
        /// </summary>
        public string ExecutablePath { get; set; } = string.Empty;
        
        /// <summary>
        /// CPU usage percentage
        /// </summary>
        public double CpuUsage { get; set; }
        
        /// <summary>
        /// Memory usage in MB
        /// </summary>
        public double MemoryUsageMB { get; set; }
        
        /// <summary>
        /// Process start time
        /// </summary>
        public DateTime StartTime { get; set; }
        
        /// <summary>
        /// Process runtime
        /// </summary>
        public TimeSpan Runtime => DateTime.Now - StartTime;
        
        /// <summary>
        /// Whether the process is a system process
        /// </summary>
        public bool IsSystemProcess { get; set; }
        
        /// <summary>
        /// Process priority
        /// </summary>
        public ProcessPriority? Priority { get; set; }
        
        /// <summary>
        /// Process CPU affinity mask
        /// </summary>
        public long? AffinityMask { get; set; }
        
        /// <summary>
        /// List of CPU cores the process is allowed to run on
        /// </summary>
        public List<int> AffinityCores
        {
            get
            {
                var cores = new List<int>();
                
                if (AffinityMask.HasValue)
                {
                    long mask = AffinityMask.Value;
                    
                    for (int i = 0; i < 64; i++)
                    {
                        if ((mask & (1L << i)) != 0)
                        {
                            cores.Add(i);
                        }
                    }
                }
                
                return cores;
            }
        }
        
        /// <summary>
        /// Process status information
        /// </summary>
        public string Status { get; set; } = "Running";
        
        /// <summary>
        /// Whether the process is elevated (running as administrator)
        /// </summary>
        public bool IsElevated { get; set; }
    }
}