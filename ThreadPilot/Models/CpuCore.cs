using System.Collections.Generic;

namespace ThreadPilot.Models
{
    /// <summary>
    /// Represents a CPU core (logical processor)
    /// </summary>
    public class CpuCore
    {
        /// <summary>
        /// Core/Thread index (zero-based)
        /// </summary>
        public int Index { get; set; }
        
        /// <summary>
        /// True if this is a physical core, false if it's a logical core (hyperthreading/SMT)
        /// </summary>
        public bool IsPhysicalCore { get; set; }
        
        /// <summary>
        /// Physical core number this logical core belongs to
        /// </summary>
        public int PhysicalCoreIndex { get; set; }
        
        /// <summary>
        /// Current utilization percentage
        /// </summary>
        public int UtilizationPercentage { get; set; }
        
        /// <summary>
        /// Current frequency in MHz
        /// </summary>
        public int CurrentFrequency { get; set; }
        
        /// <summary>
        /// Current temperature in Celsius (if available)
        /// </summary>
        public float? Temperature { get; set; }
        
        /// <summary>
        /// List of processes currently assigned to this core
        /// </summary>
        public List<int> AssignedProcessIds { get; set; } = new List<int>();
        
        /// <summary>
        /// True if core parking is active for this core (if available)
        /// </summary>
        public bool IsParked { get; set; }
        
        /// <summary>
        /// NUMA node this core belongs to
        /// </summary>
        public int NumaNode { get; set; }
        
        /// <summary>
        /// User-friendly name for display
        /// </summary>
        public string DisplayName => $"Core {Index}";
        
        /// <summary>
        /// Detailed label for display
        /// </summary>
        public string DetailedLabel => $"Core {Index} ({(IsPhysicalCore ? "Physical" : "Logical")} - {UtilizationPercentage}%)";
    }
}