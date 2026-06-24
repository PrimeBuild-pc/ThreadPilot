namespace ThreadPilot.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using CommunityToolkit.Mvvm.ComponentModel;

    public partial class CpuCoreModel : ObservableObject
    {
        public int LogicalCoreId { get; set; }

        public int PhysicalCoreId { get; set; }

        public int SocketId { get; set; }

        public int? CcdId { get; set; } // Core Complex Die (AMD)

        public int? ClusterId { get; set; } // Intel Cluster

        public CpuCoreType CoreType { get; set; } = CpuCoreType.Unknown;

        public bool IsHyperThreaded { get; set; }

        public int? HyperThreadSibling { get; set; }

        public string Label { get; set; } = string.Empty;

        public string LogicalProcessorName { get; set; } = string.Empty; // e.g., "Core0_T0", "Core0_T1" (T0 = physical, T1+ = SMT)

        [ObservableProperty]
        private bool isEnabled = true;

        [ObservableProperty]
        private bool isSelected = false;

        public long AffinityMask => 1L << this.LogicalCoreId;
    }

    public enum CpuCoreType
    {
        Unknown,
        Standard,
        PerformanceCore, // Intel P-cores
        EfficiencyCore,  // Intel E-cores
        Zen,             // AMD Zen cores
        ZenPlus,         // AMD Zen+ cores
        Zen2,            // AMD Zen2 cores
        Zen3,            // AMD Zen3 cores
        Zen4,             // AMD Zen4 cores
    }

    public class CpuTopologyModel
    {
        public List<CpuCoreModel> LogicalCores { get; set; } = new();

        public int TotalLogicalCores => this.LogicalCores.Count;

        public int TotalPhysicalCores => this.LogicalCores.GroupBy(c => c.PhysicalCoreId).Count();

        public int TotalSockets => this.LogicalCores.GroupBy(c => c.SocketId).Count();

        public int SocketCount => this.TotalSockets; // Alias for TotalSockets

        public bool HasHyperThreading => this.LogicalCores.Any(c => c.IsHyperThreaded);

        public bool HasSmt => this.HasHyperThreading; // SMT is AMD's term for HyperThreading

        public bool HasIntelHybrid => this.LogicalCores.Any(c => c.CoreType == CpuCoreType.PerformanceCore || c.CoreType == CpuCoreType.EfficiencyCore);

        public bool HasHybridArchitecture => this.HasIntelHybrid; // Alias for HasIntelHybrid

        public bool HasAmdCcd => this.LogicalCores.Any(c => c.CcdId.HasValue);

        public int CcdCount => this.LogicalCores.Where(c => c.CcdId.HasValue).Select(c => c.CcdId!.Value).Distinct().Count();

        public string Architecture => this.CpuArchitecture; // Alias for CpuArchitecture

        public string CpuArchitecture { get; set; } = "Unknown";

        public string CpuBrand { get; set; } = "Unknown";

        public bool TopologyDetectionSuccessful { get; set; } = false;

        public IEnumerable<int> AvailableCcds => this.LogicalCores
            .Where(c => c.CcdId.HasValue)
            .Select(c => c.CcdId!.Value)
            .Distinct()
            .OrderBy(id => id);

        public IEnumerable<CpuCoreModel> PerformanceCores => this.LogicalCores
            .Where(c => c.CoreType == CpuCoreType.PerformanceCore);

        public IEnumerable<CpuCoreModel> EfficiencyCores => this.LogicalCores
            .Where(c => c.CoreType == CpuCoreType.EfficiencyCore);

        public IEnumerable<CpuCoreModel> PhysicalCores => this.LogicalCores
            .GroupBy(c => c.PhysicalCoreId)
            .Select(g => g.OrderBy(c => c.LogicalCoreId).First());

        public IEnumerable<CpuCoreModel> GetCoresByCcd(int ccdId) => this.LogicalCores
            .Where(c => c.CcdId == ccdId);

        public IEnumerable<CpuCoreModel> GetCoresBySocket(int socketId) => this.LogicalCores
            .Where(c => c.SocketId == socketId);

        public long CalculateAffinityMask(IEnumerable<CpuCoreModel> cores)
        {
            return cores.Aggregate(0L, (mask, core) => mask | core.AffinityMask);
        }

        public long GetPhysicalCoresAffinityMask() => this.CalculateAffinityMask(this.PhysicalCores);

        public long GetPerformanceCoresAffinityMask() => this.CalculateAffinityMask(this.PerformanceCores);

        public long GetEfficiencyCoresAffinityMask() => this.CalculateAffinityMask(this.EfficiencyCores);

        public long GetCcdAffinityMask(int ccdId) => this.CalculateAffinityMask(this.GetCoresByCcd(ccdId));
    }

    public class CpuAffinityPreset
    {
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public long AffinityMask { get; set; }

        public bool IsAvailable { get; set; } = true;

        public string UnavailableReason { get; set; } = string.Empty;
    }
}

