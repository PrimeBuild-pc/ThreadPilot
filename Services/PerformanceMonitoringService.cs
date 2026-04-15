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
        private readonly IApplicationSettingsService settingsService;
        private readonly IEnhancedLoggingService enhancedLoggingService;
        private readonly Queue<SystemPerformanceMetrics> historicalData;
        private readonly PerformanceCounter totalCpuCounter;
        private readonly PerformanceCounter memoryCounter;
        private readonly List<PerformanceCounter> cpuCoreCounters;
        private System.Threading.Timer? monitoringTimer;
        private readonly object totalMemoryCacheLock = new();
        private readonly TimeSpan totalPhysicalMemoryCacheDuration = TimeSpan.FromMinutes(5);
        private long cachedTotalPhysicalMemory;
        private DateTime totalPhysicalMemoryCacheUtc = DateTime.MinValue;
        private readonly object processCountCacheLock = new();
        private readonly TimeSpan processCountCacheDuration = TimeSpan.FromSeconds(5);
        private int cachedProcessCount;
        private DateTime processCountCacheUtc = DateTime.MinValue;
        private readonly object runtimeTelemetryLock = new();
        private int isMonitoringTickInProgress;
        private bool runtimeTelemetryInitialized;
        private int previousGen0Collections;
        private int previousGen1Collections;
        private int previousGen2Collections;
        private long previousTotalAllocatedBytes;
        private double maxObservedGcPauseMs;
        private DateTime lastGcPauseAlertUtc = DateTime.MinValue;
        private bool isMonitoring;
        private bool disposed;

        private static readonly TimeSpan GcPauseAlertCooldown = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan WmiQueryTimeout = TimeSpan.FromSeconds(5);
        private const int HistoricalDataCapacity = 1000;
        private const double Gen2PauseAlertThresholdMs = 100;

        public event EventHandler<PerformanceMetricsUpdatedEventArgs>? MetricsUpdated;

        public PerformanceMonitoringService(
            ILogger<PerformanceMonitoringService> logger,
            IProcessService processService,
            ICpuTopologyService cpuTopologyService,
            IApplicationSettingsService settingsService,
            IEnhancedLoggingService enhancedLoggingService)
        {
            this.logger = logger;
            this.processService = processService;
            this.cpuTopologyService = cpuTopologyService;
            this.settingsService = settingsService;
            this.enhancedLoggingService = enhancedLoggingService;
            this.historicalData = new Queue<SystemPerformanceMetrics>(HistoricalDataCapacity);

            // Initialize performance counters
            this.totalCpuCounter = this.CreatePrimedCounter("Processor", "% Processor Time", "_Total");
            this.memoryCounter = this.CreatePrimedCounter("Memory", "Available MBytes");
            this.cpuCoreCounters = new List<PerformanceCounter>();

            this.InitializeCpuCoreCounters();
        }

        public async Task<SystemPerformanceMetrics> GetSystemMetricsAsync(bool lightweight = false)
        {
            try
            {
                var metrics = new SystemPerformanceMetrics
                {
                    Timestamp = DateTime.UtcNow,
                    TotalCpuUsage = await this.GetTotalCpuUsageAsync().ConfigureAwait(false),
                    AvailableMemory = await this.GetAvailableMemoryAsync().ConfigureAwait(false),
                };

                // Calculate memory percentage
                metrics.TotalMemory = await this.GetTotalPhysicalMemoryAsync().ConfigureAwait(false);
                metrics.TotalMemoryUsage = Math.Max(0, metrics.TotalMemory - metrics.AvailableMemory);
                metrics.MemoryUsagePercentage = metrics.TotalMemory > 0
                    ? ((double)(metrics.TotalMemory - metrics.AvailableMemory) / metrics.TotalMemory) * 100
                    : 0;

                this.PopulateRuntimeTelemetry(metrics);

                if (!lightweight)
                {
                    metrics.CpuCoreUsages = await this.GetCpuCoreUsageAsync().ConfigureAwait(false);
                    metrics.ActiveProcessCount = await this.GetActiveProcessCountAsync().ConfigureAwait(false);

                    // Get top processes
                    var topCpuProcesses = await this.GetTopCpuProcessesAsync(1).ConfigureAwait(false);
                    metrics.TopCpuProcess = topCpuProcesses.FirstOrDefault();

                    var topMemoryProcesses = await this.GetTopMemoryProcessesAsync(1).ConfigureAwait(false);
                    metrics.TopMemoryProcess = topMemoryProcesses.FirstOrDefault();

                    // Store in historical data
                    if (this.historicalData.Count >= HistoricalDataCapacity)
                    {
                        this.historicalData.Dequeue();
                    }

                    this.historicalData.Enqueue(metrics);
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
                var topology = await this.cpuTopologyService.DetectTopologyAsync().ConfigureAwait(false);

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
                var scope = CreateCimv2ScopeWithTimeout();
                using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"));
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
                using var memSearcher = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT TotalVirtualMemorySize, FreeVirtualMemory FROM Win32_OperatingSystem"));
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
                var processes = await this.processService.GetProcessesAsync().ConfigureAwait(false);
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
                        ThreadCount = GetThreadCountSafe(p.ProcessId),
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
                var processes = await this.processService.GetProcessesAsync().ConfigureAwait(false);
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
                        ThreadCount = GetThreadCountSafe(p.ProcessId),
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
            Interlocked.Exchange(ref this.isMonitoringTickInProgress, 0);

            // PERFORMANCE OPTIMIZATION: Increased interval from 1s to 2s for better performance
            this.monitoringTimer = new System.Threading.Timer(
                async _ =>
            {
                if (Interlocked.Exchange(ref this.isMonitoringTickInProgress, 1) == 1)
                {
                    return;
                }

                try
                {
                    var metrics = await this.GetSystemMetricsAsync().ConfigureAwait(false);
                    await this.EmitGcDiagnosticsIfNeededAsync(metrics).ConfigureAwait(false);
                    this.MetricsUpdated?.Invoke(this, new PerformanceMetricsUpdatedEventArgs(metrics));
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "Error during performance monitoring update");
                }
                finally
                {
                    Interlocked.Exchange(ref this.isMonitoringTickInProgress, 0);
                }
            }, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));
        }

        public Task StopMonitoringAsync()
        {
            if (!this.isMonitoring)
            {
                return Task.CompletedTask;
            }

            this.logger.LogInformation("Stopping performance monitoring");
            this.isMonitoring = false;
            Interlocked.Exchange(ref this.isMonitoringTickInProgress, 0);

            this.monitoringTimer?.Dispose();
            this.monitoringTimer = null;
            return Task.CompletedTask;
        }

        public Task<List<SystemPerformanceMetrics>> GetHistoricalDataAsync(TimeSpan duration)
        {
            var cutoffTime = DateTime.UtcNow - duration;
            var data = this.historicalData.Where(m => m.Timestamp >= cutoffTime).ToList();
            return Task.FromResult(data);
        }

        public Task ClearHistoricalDataAsync()
        {
            this.historicalData.Clear();
            this.logger.LogInformation("Historical performance data cleared");
            return Task.CompletedTask;
        }

        private void InitializeCpuCoreCounters()
        {
            var tempCounters = new List<PerformanceCounter>();

            try
            {
                var coreCount = Environment.ProcessorCount;
                for (int i = 0; i < coreCount; i++)
                {
                    tempCounters.Add(this.CreatePrimedCounter("Processor", "% Processor Time", i.ToString()));
                }

                this.cpuCoreCounters.Clear();
                this.cpuCoreCounters.AddRange(tempCounters);

                this.logger.LogInformation("Initialized {CoreCount} CPU core performance counters", coreCount);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error initializing CPU core counters");
                foreach (var counter in tempCounters)
                {
                    try
                    {
                        counter.Dispose();
                    }
                    catch
                    {
                        // Best effort cleanup for partially initialized counters.
                    }
                }

                throw;
            }
        }

        private PerformanceCounter CreatePrimedCounter(string categoryName, string counterName, string? instanceName = null)
        {
            try
            {
                var counter = string.IsNullOrWhiteSpace(instanceName)
                    ? new PerformanceCounter(categoryName, counterName)
                    : new PerformanceCounter(categoryName, counterName, instanceName);

                _ = counter.NextValue();
                return counter;
            }
            catch (Exception ex)
            {
                this.logger.LogError(
                    ex,
                    "Failed to initialize PerformanceCounter category '{Category}' counter '{Counter}' instance '{Instance}'",
                    categoryName,
                    counterName,
                    instanceName ?? "<none>");
                throw;
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
            var now = DateTime.UtcNow;

            lock (this.totalMemoryCacheLock)
            {
                if (this.cachedTotalPhysicalMemory > 0 &&
                    (now - this.totalPhysicalMemoryCacheUtc) < this.totalPhysicalMemoryCacheDuration)
                {
                    return this.cachedTotalPhysicalMemory;
                }
            }

            try
            {
                var scope = CreateCimv2ScopeWithTimeout();
                using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"));
                foreach (var obj in searcher.Get())
                {
                    var totalMemory = Convert.ToInt64(obj["TotalPhysicalMemory"]);

                    lock (this.totalMemoryCacheLock)
                    {
                        this.cachedTotalPhysicalMemory = totalMemory;
                        this.totalPhysicalMemoryCacheUtc = DateTime.UtcNow;
                    }

                    return totalMemory;
                }

                return 0;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error getting total physical memory");
                return 0;
            }
        }

        private async Task<int> GetActiveProcessCountAsync()
        {
            var now = DateTime.UtcNow;
            lock (this.processCountCacheLock)
            {
                if ((now - this.processCountCacheUtc) < this.processCountCacheDuration)
                {
                    return this.cachedProcessCount;
                }
            }

            try
            {
                var scope = CreateCimv2ScopeWithTimeout();
                using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT Count(*) AS Count FROM Win32_Process"));
                var result = searcher.Get().Cast<ManagementBaseObject>().FirstOrDefault();
                var countValue = result?["Count"];
                var count = countValue != null ? Convert.ToInt32(countValue) : 0;

                lock (this.processCountCacheLock)
                {
                    this.cachedProcessCount = count;
                    this.processCountCacheUtc = now;
                }

                return count;
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Failed to read process count via WMI, falling back to Process.GetProcesses");
                return Process.GetProcesses().Length;
            }
        }

        private static ManagementScope CreateCimv2ScopeWithTimeout()
        {
            var options = new ConnectionOptions { Timeout = WmiQueryTimeout };
            var scope = new ManagementScope(@"\\.\root\cimv2", options);
            scope.Connect();
            return scope;
        }

        private static int GetThreadCountSafe(int processId)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                return process.Threads.Count;
            }
            catch
            {
                return 0;
            }
        }

        private void PopulateRuntimeTelemetry(SystemPerformanceMetrics metrics)
        {
            try
            {
                var gen0Collections = GC.CollectionCount(0);
                var gen1Collections = GC.CollectionCount(1);
                var gen2Collections = GC.CollectionCount(2);
                var totalAllocatedBytes = GC.GetTotalAllocatedBytes();
                var gcInfo = GC.GetGCMemoryInfo();
                var lastGcPauseMs = GetLastGcPauseMilliseconds(gcInfo);

                metrics.Gen0Collections = gen0Collections;
                metrics.Gen1Collections = gen1Collections;
                metrics.Gen2Collections = gen2Collections;
                metrics.TotalAllocatedBytes = totalAllocatedBytes;
                metrics.ManagedHeapSizeBytes = gcInfo.HeapSizeBytes;
                metrics.GcCommittedBytes = gcInfo.TotalCommittedBytes;
                metrics.LastGcPauseMs = lastGcPauseMs;

                lock (this.runtimeTelemetryLock)
                {
                    if (this.runtimeTelemetryInitialized)
                    {
                        metrics.Gen0CollectionsDelta = Math.Max(0, gen0Collections - this.previousGen0Collections);
                        metrics.Gen1CollectionsDelta = Math.Max(0, gen1Collections - this.previousGen1Collections);
                        metrics.Gen2CollectionsDelta = Math.Max(0, gen2Collections - this.previousGen2Collections);
                        metrics.AllocatedBytesDelta = Math.Max(0, totalAllocatedBytes - this.previousTotalAllocatedBytes);
                    }
                    else
                    {
                        metrics.Gen0CollectionsDelta = 0;
                        metrics.Gen1CollectionsDelta = 0;
                        metrics.Gen2CollectionsDelta = 0;
                        metrics.AllocatedBytesDelta = 0;
                        this.runtimeTelemetryInitialized = true;
                    }

                    this.previousGen0Collections = gen0Collections;
                    this.previousGen1Collections = gen1Collections;
                    this.previousGen2Collections = gen2Collections;
                    this.previousTotalAllocatedBytes = totalAllocatedBytes;

                    this.maxObservedGcPauseMs = Math.Max(this.maxObservedGcPauseMs, lastGcPauseMs);
                    metrics.MaxGcPauseMs = this.maxObservedGcPauseMs;
                }

                using var currentProcess = Process.GetCurrentProcess();
                metrics.HandleCount = currentProcess.HandleCount;
                metrics.ProcessWorkingSetBytes = currentProcess.WorkingSet64;
            }
            catch (Exception ex)
            {
                this.logger.LogDebug(ex, "Failed to collect runtime GC telemetry sample");
            }
        }

        private async Task EmitGcDiagnosticsIfNeededAsync(SystemPerformanceMetrics metrics)
        {
            try
            {
                if (!this.settingsService.Settings.EnablePerformanceCounters)
                {
                    return;
                }

                if (metrics.Gen2CollectionsDelta <= 0 || metrics.LastGcPauseMs < Gen2PauseAlertThresholdMs)
                {
                    return;
                }

                var now = DateTime.UtcNow;
                if ((now - this.lastGcPauseAlertUtc) < GcPauseAlertCooldown)
                {
                    return;
                }

                this.lastGcPauseAlertUtc = now;

                this.logger.LogWarning(
                    "Gen2 GC pause alert: LastPauseMs={LastPauseMs}, Gen2Delta={Gen2Delta}, HeapBytes={HeapBytes}, AllocDeltaBytes={AllocDeltaBytes}",
                    metrics.LastGcPauseMs,
                    metrics.Gen2CollectionsDelta,
                    metrics.ManagedHeapSizeBytes,
                    metrics.AllocatedBytesDelta);

                await this.enhancedLoggingService.LogSystemEventAsync(
                    LogEventTypes.Performance.SlowOperation,
                    $"Gen2 GC pause {metrics.LastGcPauseMs:F2}ms (delta={metrics.Gen2CollectionsDelta}, heap={metrics.ManagedHeapSizeBytes} bytes)",
                    LogLevel.Warning).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this.logger.LogDebug(ex, "Failed to emit GC diagnostics alert");
            }
        }

        private static double GetLastGcPauseMilliseconds(GCMemoryInfo gcInfo)
        {
            var pauseDurations = gcInfo.PauseDurations;
            if (pauseDurations.Length == 0)
            {
                return 0;
            }

            return pauseDurations[pauseDurations.Length - 1].TotalMilliseconds;
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
            Interlocked.Exchange(ref this.isMonitoringTickInProgress, 0);
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

