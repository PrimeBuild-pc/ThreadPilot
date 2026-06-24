namespace ThreadPilot.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using ThreadPilot.Models;

    public interface IPerformanceMonitoringService
    {
        Task<SystemPerformanceMetrics> GetSystemMetricsAsync(bool lightweight = false);

        Task<List<CpuCoreUsage>> GetCpuCoreUsageAsync();

        Task<MemoryUsageInfo> GetMemoryUsageAsync();

        Task<List<ProcessPerformanceInfo>> GetTopCpuProcessesAsync(int count = 10);

        Task<List<ProcessPerformanceInfo>> GetTopMemoryProcessesAsync(int count = 10);

        Task StartMonitoringAsync();

        Task StopMonitoringAsync();

        event EventHandler<PerformanceMetricsUpdatedEventArgs>? MetricsUpdated;

        Task<List<SystemPerformanceMetrics>> GetHistoricalDataAsync(TimeSpan duration);

        Task ClearHistoricalDataAsync();
    }

    public class SystemPerformanceMetrics
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public double TotalCpuUsage { get; set; }

        public long TotalMemoryUsage { get; set; }

        public long AvailableMemory { get; set; }

        public long TotalMemory { get; set; }

        public double MemoryUsagePercentage { get; set; }

        public int Gen0Collections { get; set; }

        public int Gen1Collections { get; set; }

        public int Gen2Collections { get; set; }

        public int Gen0CollectionsDelta { get; set; }

        public int Gen1CollectionsDelta { get; set; }

        public int Gen2CollectionsDelta { get; set; }

        public long TotalAllocatedBytes { get; set; }

        public long AllocatedBytesDelta { get; set; }

        public long ManagedHeapSizeBytes { get; set; }

        public long GcCommittedBytes { get; set; }

        public double LastGcPauseMs { get; set; }

        public double MaxGcPauseMs { get; set; }

        public int HandleCount { get; set; }

        public long ProcessWorkingSetBytes { get; set; }

        public List<CpuCoreUsage> CpuCoreUsages { get; set; } = new();

        public ProcessPerformanceInfo? TopCpuProcess { get; set; }

        public ProcessPerformanceInfo? TopMemoryProcess { get; set; }

        public int ActiveProcessCount { get; set; }

        public int ThreadCount { get; set; }

        public double DiskUsage { get; set; }

        public double NetworkUsage { get; set; }
    }

    public class CpuCoreUsage
    {
        public int CoreId { get; set; }

        public string CoreName { get; set; } = string.Empty;

        public double Usage { get; set; }

        public string CoreType { get; set; } = "Unknown"; // P-Core, E-Core, etc.

        public bool IsHyperThreaded { get; set; }

        public int PhysicalCoreId { get; set; }

        public double Frequency { get; set; }

        public double Temperature { get; set; }
    }

    public class MemoryUsageInfo
    {
        public long TotalPhysicalMemory { get; set; }

        public long AvailablePhysicalMemory { get; set; }

        public long UsedPhysicalMemory { get; set; }

        public double PhysicalMemoryUsagePercentage { get; set; }

        public long TotalVirtualMemory { get; set; }

        public long AvailableVirtualMemory { get; set; }

        public long UsedVirtualMemory { get; set; }

        public double VirtualMemoryUsagePercentage { get; set; }

        public long PageFileSize { get; set; }

        public long PageFileUsage { get; set; }

        public double PageFileUsagePercentage { get; set; }
    }

    public class ProcessPerformanceInfo
    {
        public int ProcessId { get; set; }

        public string ProcessName { get; set; } = string.Empty;

        public string WindowTitle { get; set; } = string.Empty;

        public double CpuUsage { get; set; }

        public long MemoryUsage { get; set; }

        public long VirtualMemoryUsage { get; set; }

        public int ThreadCount { get; set; }

        public int HandleCount { get; set; }

        public DateTime StartTime { get; set; }

        public TimeSpan RunTime { get; set; }

        public string ExecutablePath { get; set; } = string.Empty;

        public bool IsResponding { get; set; }

        public string Priority { get; set; } = string.Empty;

        public IntPtr ProcessorAffinity { get; set; }
    }

    public class PerformanceMetricsUpdatedEventArgs : EventArgs
    {
        public SystemPerformanceMetrics Metrics { get; }

        public DateTime UpdateTime { get; }

        public PerformanceMetricsUpdatedEventArgs(SystemPerformanceMetrics metrics)
        {
            this.Metrics = metrics;
            this.UpdateTime = DateTime.UtcNow;
        }
    }
}

