/*
 * ThreadPilot - Advanced Windows Process and Power Plan Manager
 * Copyright (C) 2025 Prime Build
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, version 3 only.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
namespace ThreadPilot.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Management;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using ThreadPilot.Models;

    /// <summary>
    /// Service for real-time performance monitoring.
    /// </summary>
    public class PerformanceMonitoringService : IPerformanceMonitoringService, IDisposable
    {
        private readonly ILogger<PerformanceMonitoringService> logger;
        private readonly IProcessService processService;
        private readonly ICpuTopologyService cpuTopologyService;
        private readonly List<SystemPerformanceMetrics> historicalData;
        private readonly PerformanceCounter totalCpuCounter;
        private readonly PerformanceCounter memoryCounter;
        private readonly List<PerformanceCounter> cpuCoreCounters;
        private System.Threading.Timer? monitoringTimer;
        private bool isMonitoring;
        private bool disposed;

        public event EventHandler<PerformanceMetricsUpdatedEventArgs>? MetricsUpdated;

        public PerformanceMonitoringService(
            ILogger<PerformanceMonitoringService> logger,
            IProcessService processService,
            ICpuTopologyService cpuTopologyService)
        {
            this.logger = logger;
            this.processService = processService;
            this.cpuTopologyService = cpuTopologyService;
            this.historicalData = new List<SystemPerformanceMetrics>();

            // Initialize performance counters
            this.totalCpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            this.memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
            this.cpuCoreCounters = new List<PerformanceCounter>();

            this.InitializeCpuCoreCounters();
        }

        public async Task<SystemPerformanceMetrics> GetSystemMetricsAsync()
        {
            try
            {
                var metrics = new SystemPerformanceMetrics
                {
                    Timestamp = DateTime.UtcNow,
                    TotalCpuUsage = await this.GetTotalCpuUsageAsync(),
                    CpuCoreUsages = await this.GetCpuCoreUsageAsync(),
                    TotalMemoryUsage = await this.GetTotalMemoryUsageAsync(),
                    AvailableMemory = await this.GetAvailableMemoryAsync(),
                    ActiveProcessCount = Process.GetProcesses().Length,
                };

                // Calculate memory percentage
                metrics.TotalMemory = await this.GetTotalPhysicalMemoryAsync();
                metrics.MemoryUsagePercentage = metrics.TotalMemory > 0
                    ? ((double)(metrics.TotalMemory - metrics.AvailableMemory) / metrics.TotalMemory) * 100
                    : 0;

                // Get top processes
                var topCpuProcesses = await this.GetTopCpuProcessesAsync(1);
                metrics.TopCpuProcess = topCpuProcesses.FirstOrDefault();

                var topMemoryProcesses = await this.GetTopMemoryProcessesAsync(1);
                metrics.TopMemoryProcess = topMemoryProcesses.FirstOrDefault();

                // Store in historical data
                this.historicalData.Add(metrics);

                // Keep only last 1000 entries (about 16 minutes at 1-second intervals)
                if (this.historicalData.Count > 1000)
                {
                    this.historicalData.RemoveAt(0);
                }

                return metrics;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error getting system metrics");
                return new SystemPerformanceMetrics();
            }
        }

        public async Task<List<CpuCoreUsage>> GetCpuCoreUsageAsync()
        {
            var coreUsages = new List<CpuCoreUsage>();

            try
            {
                var topology = await this.cpuTopologyService.DetectTopologyAsync();

                for (int i = 0; i < this.cpuCoreCounters.Count; i++)
                {
                    var counter = this.cpuCoreCounters[i];
                    var usage = counter.NextValue();

                    var coreUsage = new CpuCoreUsage
                    {
                        CoreId = i,
                        CoreName = $"Core {i}",
                        Usage = usage,
                        CoreType = DetermineCoreType(i, topology),
                        IsHyperThreaded = IsHyperThreadedCore(i, topology),
                        PhysicalCoreId = GetPhysicalCoreId(i, topology),
                    };

                    coreUsages.Add(coreUsage);
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error getting CPU core usage");
            }

            return coreUsages;
        }

        public async Task<MemoryUsageInfo> GetMemoryUsageAsync()
        {
            try
            {
                var memoryInfo = new MemoryUsageInfo();

                // Get physical memory info
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem");
                foreach (var obj in searcher.Get())
                {
                    memoryInfo.TotalPhysicalMemory = Convert.ToInt64(obj["TotalPhysicalMemory"]);
                }

                // Get available memory
                memoryInfo.AvailablePhysicalMemory = (long)this.memoryCounter.NextValue() * 1024 * 1024; // Convert MB to bytes
                memoryInfo.UsedPhysicalMemory = memoryInfo.TotalPhysicalMemory - memoryInfo.AvailablePhysicalMemory;
                memoryInfo.PhysicalMemoryUsagePercentage = memoryInfo.TotalPhysicalMemory > 0
                    ? ((double)memoryInfo.UsedPhysicalMemory / memoryInfo.TotalPhysicalMemory) * 100
                    : 0;

                // Get virtual memory info
                using var memSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");
                foreach (var obj in memSearcher.Get())
                {
                    memoryInfo.TotalVirtualMemory = Convert.ToInt64(obj["TotalVirtualMemorySize"]) * 1024; // Convert KB to bytes
                    memoryInfo.AvailableVirtualMemory = Convert.ToInt64(obj["FreeVirtualMemory"]) * 1024;
                }

                memoryInfo.UsedVirtualMemory = memoryInfo.TotalVirtualMemory - memoryInfo.AvailableVirtualMemory;
                memoryInfo.VirtualMemoryUsagePercentage = memoryInfo.TotalVirtualMemory > 0
                    ? ((double)memoryInfo.UsedVirtualMemory / memoryInfo.TotalVirtualMemory) * 100
                    : 0;

                return memoryInfo;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error getting memory usage");
                return new MemoryUsageInfo();
            }
        }

        public async Task<List<ProcessPerformanceInfo>> GetTopCpuProcessesAsync(int count = 10)
        {
            try
            {
                var processes = await this.processService.GetProcessesAsync();
                return processes
                    .OrderByDescending(p => p.CpuUsage)
                    .Take(count)
                    .Select(p => new ProcessPerformanceInfo
                    {
                        ProcessId = p.ProcessId,
                        ProcessName = p.Name,
                        WindowTitle = p.MainWindowTitle,
                        CpuUsage = p.CpuUsage,
                        MemoryUsage = p.MemoryUsage,
                        ThreadCount = p.Process?.Threads?.Count ?? 0,
                        ExecutablePath = p.ExecutablePath ?? string.Empty,
                        Priority = p.Priority.ToString(),
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error getting top CPU processes");
                return new List<ProcessPerformanceInfo>();
            }
        }

        public async Task<List<ProcessPerformanceInfo>> GetTopMemoryProcessesAsync(int count = 10)
        {
            try
            {
                var processes = await this.processService.GetProcessesAsync();
                return processes
                    .OrderByDescending(p => p.MemoryUsage)
                    .Take(count)
                    .Select(p => new ProcessPerformanceInfo
                    {
                        ProcessId = p.ProcessId,
                        ProcessName = p.Name,
                        WindowTitle = p.MainWindowTitle,
                        CpuUsage = p.CpuUsage,
                        MemoryUsage = p.MemoryUsage,
                        ThreadCount = p.Process?.Threads?.Count ?? 0,
                        ExecutablePath = p.ExecutablePath ?? string.Empty,
                        Priority = p.Priority.ToString(),
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error getting top memory processes");
                return new List<ProcessPerformanceInfo>();
            }
        }

        public async Task StartMonitoringAsync()
        {
            if (this.isMonitoring)
            {
                return;
            }

            this.logger.LogInformation("Starting performance monitoring");
            this.isMonitoring = true;

            // PERFORMANCE OPTIMIZATION: Increased interval from 1s to 2s for better performance
            this.monitoringTimer = new System.Threading.Timer(
                async _ =>
            {
                try
                {
                    var metrics = await this.GetSystemMetricsAsync();
                    this.MetricsUpdated?.Invoke(this, new PerformanceMetricsUpdatedEventArgs(metrics));
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "Error during performance monitoring update");
                }
            }, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));
        }

        public async Task StopMonitoringAsync()
        {
            if (!this.isMonitoring)
            {
                return;
            }

            this.logger.LogInformation("Stopping performance monitoring");
            this.isMonitoring = false;

            this.monitoringTimer?.Dispose();
            this.monitoringTimer = null;
        }

        public async Task<List<SystemPerformanceMetrics>> GetHistoricalDataAsync(TimeSpan duration)
        {
            var cutoffTime = DateTime.UtcNow - duration;
            return this.historicalData.Where(m => m.Timestamp >= cutoffTime).ToList();
        }

        public async Task ClearHistoricalDataAsync()
        {
            this.historicalData.Clear();
            this.logger.LogInformation("Historical performance data cleared");
        }

        private void InitializeCpuCoreCounters()
        {
            try
            {
                var coreCount = Environment.ProcessorCount;
                for (int i = 0; i < coreCount; i++)
                {
                    var counter = new PerformanceCounter("Processor", "% Processor Time", i.ToString());
                    this.cpuCoreCounters.Add(counter);
                }

                this.logger.LogInformation("Initialized {CoreCount} CPU core performance counters", coreCount);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error initializing CPU core counters");
            }
        }

        private async Task<double> GetTotalCpuUsageAsync()
        {
            try
            {
                return this.totalCpuCounter.NextValue();
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error getting total CPU usage");
                return 0;
            }
        }

        private async Task<long> GetTotalMemoryUsageAsync()
        {
            try
            {
                var totalMemory = await this.GetTotalPhysicalMemoryAsync();
                var availableMemory = await this.GetAvailableMemoryAsync();
                return totalMemory - availableMemory;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error getting total memory usage");
                return 0;
            }
        }

        private async Task<long> GetAvailableMemoryAsync()
        {
            try
            {
                return (long)this.memoryCounter.NextValue() * 1024 * 1024; // Convert MB to bytes
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error getting available memory");
                return 0;
            }
        }

        private async Task<long> GetTotalPhysicalMemoryAsync()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                foreach (var obj in searcher.Get())
                {
                    return Convert.ToInt64(obj["TotalPhysicalMemory"]);
                }
                return 0;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error getting total physical memory");
                return 0;
            }
        }

        private static string DetermineCoreType(int coreId, CpuTopologyModel? topology)
        {
            if (topology?.HasIntelHybrid == true)
            {
                // Intel hybrid architecture
                if (coreId < topology.PerformanceCores.Count())
                {
                    return "P-Core";
                }
                else
                {
                    return "E-Core";
                }
            }

            return "Standard";
        }

        private static bool IsHyperThreadedCore(int coreId, CpuTopologyModel? topology)
        {
            if (topology?.HasHyperThreading != true)
            {
                return false;
            }

            // Simplified logic - in reality this would be more complex
            return coreId >= topology.TotalPhysicalCores;
        }

        private static int GetPhysicalCoreId(int coreId, CpuTopologyModel? topology)
        {
            if (topology?.HasHyperThreading == true)
            {
                return coreId / 2; // Simplified - assumes 2 threads per core
            }

            return coreId;
        }

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.monitoringTimer?.Dispose();
            this.totalCpuCounter?.Dispose();
            this.memoryCounter?.Dispose();

            foreach (var counter in this.cpuCoreCounters)
            {
                counter?.Dispose();
            }

            this.cpuCoreCounters.Clear();
            this.disposed = true;
        }
    }
}

