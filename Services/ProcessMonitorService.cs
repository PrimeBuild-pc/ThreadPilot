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
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Management;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using ThreadPilot.Models;

    /// <summary>
    /// Process monitoring service using WMI events with fallback polling.
    /// </summary>
    public class ProcessMonitorService : IProcessMonitorService
    {
        private readonly IProcessService processService;
        private readonly IApplicationSettingsService settingsService;
        private readonly ILogger<ProcessMonitorService>? logger;
        private readonly object lockObject = new();
        private readonly ConcurrentDictionary<int, ProcessModel> runningProcesses = new();
        private readonly SemaphoreSlim wmiStartSemaphore = new(1, 1);
        private readonly Dictionary<int, ProcessModel> pollBuffer = new();

        private ManagementEventWatcher? processStartWatcher;
        private ManagementEventWatcher? processStopWatcher;
        private System.Threading.Timer? fallbackTimer;
        private CancellationTokenSource? cancellationTokenSource;

        private bool isMonitoring;
        private bool isWmiAvailable;
        private bool isFallbackPollingActive;
        private int disposedFlag;

        // Configuration - will be updated from settings
        private int fallbackPollingIntervalMs = 5000; // Default 5 seconds
        private int currentFallbackPollingIntervalMs = 5000;
        private int idlePollingMultiplier = 1;
        private readonly int wmiRetryDelayMs = 10000; // 10 seconds
        private const int MaxIdlePollingMultiplier = 6;
        private bool enableWmiMonitoring = true;
        private bool enableFallbackPolling = true;
        private int isFallbackPollingInProgress;
        private int isWmiRecoveryInProgress;
        private DateTime lastWmiRetryAttemptUtc = DateTime.MinValue;

        public event EventHandler<ProcessEventArgs>? ProcessStarted;

        public event EventHandler<ProcessEventArgs>? ProcessStopped;

        public event EventHandler<MonitoringStatusEventArgs>? MonitoringStatusChanged;

        private bool IsDisposed => Interlocked.CompareExchange(ref this.disposedFlag, 0, 0) == 1;

        public bool IsMonitoring => this.isMonitoring;

        public bool IsWmiAvailable => this.isWmiAvailable;

        public bool IsFallbackPollingActive => this.isFallbackPollingActive;

        public ProcessMonitorService(
            IProcessService processService,
            IApplicationSettingsService settingsService,
            ILogger<ProcessMonitorService>? logger = null)
        {
            this.processService = processService ?? throw new ArgumentNullException(nameof(processService));
            this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            this.logger = logger;

            // Initialize polling interval from settings
            this.UpdateMonitoringSettings();
        }

        public async Task StartMonitoringAsync()
        {
            if (this.IsDisposed)
            {
                throw new ObjectDisposedException(nameof(ProcessMonitorService));
            }

            lock (this.lockObject)
            {
                if (this.isMonitoring)
                {
                    return;
                }

                this.isMonitoring = true;
            }

            this.cancellationTokenSource = new CancellationTokenSource();
            this.lastWmiRetryAttemptUtc = DateTime.MinValue;
            Interlocked.Exchange(ref this.isFallbackPollingInProgress, 0);
            Interlocked.Exchange(ref this.isWmiRecoveryInProgress, 0);

            this.UpdateMonitoringSettings();

            // Initialize current process list
            await this.InitializeProcessListAsync().ConfigureAwait(false);

            bool wmiStarted = false;
            if (this.enableWmiMonitoring)
            {
                // Try to start WMI monitoring first
                wmiStarted = await this.TryStartWmiMonitoringAsync().ConfigureAwait(false);
            }

            if (!wmiStarted && this.enableFallbackPolling)
            {
                // Fall back to polling if WMI is not available
                this.StartFallbackPolling();
            }
            else if (!wmiStarted && !this.enableFallbackPolling)
            {
                var reason = this.enableWmiMonitoring
                    ? "WMI monitoring unavailable and fallback polling is disabled"
                    : "Both WMI monitoring and fallback polling are disabled";

                this.OnMonitoringStatusChanged(reason);
            }

            this.OnMonitoringStatusChanged();
        }

        public async Task StopMonitoringAsync()
        {
            if (this.IsDisposed)
            {
                return;
            }

            var semaphoreHeld = false;
            await this.wmiStartSemaphore.WaitAsync().ConfigureAwait(false);
            semaphoreHeld = true;

            try
            {
                lock (this.lockObject)
                {
                    if (!this.isMonitoring)
                    {
                        return;
                    }

                    this.isMonitoring = false;
                }

                // Stop WMI watchers
                this.StopWmiWatchers();

                // Stop fallback polling
                this.StopFallbackPolling();

                // Cancel any ongoing operations
                this.cancellationTokenSource?.Cancel();
                this.cancellationTokenSource?.Dispose();
                this.cancellationTokenSource = null;

                this.runningProcesses.Clear();
                this.pollBuffer.Clear();
                Interlocked.Exchange(ref this.isFallbackPollingInProgress, 0);
                Interlocked.Exchange(ref this.isWmiRecoveryInProgress, 0);
                this.OnMonitoringStatusChanged();
            }
            finally
            {
                if (semaphoreHeld)
                {
                    this.wmiStartSemaphore.Release();
                }
            }
        }

        public async Task<IEnumerable<ProcessModel>> GetRunningProcessesAsync()
        {
            try
            {
                var processes = await this.processService.GetProcessesAsync().ConfigureAwait(false);
                return processes;
            }
            catch (Exception)
            {
                return Enumerable.Empty<ProcessModel>();
            }
        }

        public async Task<bool> IsProcessRunningAsync(string executableName)
        {
            try
            {
                var processes = await this.GetRunningProcessesAsync().ConfigureAwait(false);
                return processes.Any(p => string.Equals(p.Name, executableName, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }

        private async Task InitializeProcessListAsync()
        {
            try
            {
                var processes = await this.GetRunningProcessesAsync().ConfigureAwait(false);
                this.runningProcesses.Clear();

                foreach (var process in processes)
                {
                    this.runningProcesses.TryAdd(process.ProcessId, process);
                }
            }
            catch (Exception ex)
            {
                this.OnMonitoringStatusChanged($"Failed to initialize process list: {ex.Message}", ex);
            }
        }

        private async Task<bool> TryStartWmiMonitoringAsync()
        {
            if (this.IsDisposed || !this.isMonitoring || !this.enableWmiMonitoring)
            {
                return false;
            }

            await this.wmiStartSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                if (this.IsDisposed || !this.isMonitoring || !this.enableWmiMonitoring)
                {
                    return false;
                }

                if (this.isWmiAvailable && this.processStartWatcher != null && this.processStopWatcher != null)
                {
                    return true;
                }

                await Task.Run(() =>
                {
                    // Ensure any previous watchers are fully cleaned up before re-creating them
                    this.StopWmiWatchers();

                    // Create WMI event watchers for process start and stop
                    var startQuery = new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace");
                    var stopQuery = new WqlEventQuery("SELECT * FROM Win32_ProcessStopTrace");

                    this.processStartWatcher = new ManagementEventWatcher(startQuery);
                    this.processStopWatcher = new ManagementEventWatcher(stopQuery);

                    this.processStartWatcher.EventArrived += this.OnProcessStarted;
                    this.processStopWatcher.EventArrived += this.OnProcessStopped;
                    this.processStartWatcher.Stopped += this.OnWmiWatcherStopped;
                    this.processStopWatcher.Stopped += this.OnWmiWatcherStopped;

                    this.processStartWatcher.Start();
                    this.processStopWatcher.Start();
                }).ConfigureAwait(false);

                this.isWmiAvailable = true;

                // Prefer WMI when available to reduce polling overhead
                if (this.isFallbackPollingActive)
                {
                    this.StopFallbackPolling();
                }

                this.OnMonitoringStatusChanged("WMI monitoring started successfully");
                return true;
            }
            catch (Exception ex)
            {
                this.isWmiAvailable = false;
                this.OnMonitoringStatusChanged($"WMI monitoring failed: {ex.Message}", ex);

                // Clean up any partially created watchers
                this.StopWmiWatchers();

                return false;
            }
            finally
            {
                this.wmiStartSemaphore.Release();
            }
        }

        private void StartFallbackPolling()
        {
            if (this.IsDisposed || !this.isMonitoring || !this.enableFallbackPolling)
            {
                return;
            }

            // Update polling interval from current settings
            this.UpdateMonitoringSettings();

            if (this.isFallbackPollingActive)
            {
                this.currentFallbackPollingIntervalMs = this.fallbackPollingIntervalMs;
                this.fallbackTimer?.Change(0, this.currentFallbackPollingIntervalMs);
                return;
            }

            this.isFallbackPollingActive = true;
            this.idlePollingMultiplier = 1;
            this.currentFallbackPollingIntervalMs = this.fallbackPollingIntervalMs;
            this.fallbackTimer = new System.Threading.Timer(this.FallbackPollingCallback, null, 0, this.currentFallbackPollingIntervalMs);
            this.OnMonitoringStatusChanged($"Fallback polling started (interval: {this.fallbackPollingIntervalMs}ms)");
        }

        private void OnWmiWatcherStopped(object sender, StoppedEventArgs e)
        {
            if (!this.isMonitoring || this.IsDisposed)
            {
                return;
            }

            this.isWmiAvailable = false;
            this.OnMonitoringStatusChanged($"WMI watcher stopped ({e.Status})");

            if (this.enableFallbackPolling && !this.isFallbackPollingActive)
            {
                this.StartFallbackPolling();
            }
        }

        private void StopWmiWatchers()
        {
            try
            {
                if (this.processStartWatcher != null)
                {
                    this.processStartWatcher.EventArrived -= this.OnProcessStarted;
                    this.processStartWatcher.Stopped -= this.OnWmiWatcherStopped;
                }

                this.processStartWatcher?.Stop();
                this.processStartWatcher?.Dispose();
                this.processStartWatcher = null;

                if (this.processStopWatcher != null)
                {
                    this.processStopWatcher.EventArrived -= this.OnProcessStopped;
                    this.processStopWatcher.Stopped -= this.OnWmiWatcherStopped;
                }

                this.processStopWatcher?.Stop();
                this.processStopWatcher?.Dispose();
                this.processStopWatcher = null;

                this.isWmiAvailable = false;
            }
            catch (Exception ex)
            {
                this.OnMonitoringStatusChanged($"Error stopping WMI watchers: {ex.Message}", ex);
            }
        }

        private void StopFallbackPolling()
        {
            this.fallbackTimer?.Dispose();
            this.fallbackTimer = null;
            this.isFallbackPollingActive = false;
            Interlocked.Exchange(ref this.isFallbackPollingInProgress, 0);
        }

        private void OnProcessStarted(object sender, EventArrivedEventArgs e)
        {
            TaskSafety.FireAndForget(this.HandleProcessStartedAsync(e), ex =>
            {
                this.OnMonitoringStatusChanged($"Error handling process start event: {ex.Message}", ex);
            });
        }

        private async Task HandleProcessStartedAsync(EventArrivedEventArgs e)
        {
            if (!this.isMonitoring || this.IsDisposed)
            {
                return;
            }

            try
            {
                var processId = Convert.ToInt32(e.NewEvent["ProcessID"]);
                var processName = e.NewEvent["ProcessName"]?.ToString() ?? string.Empty;

                // Get detailed process information
                var process = await this.CreateProcessModelFromId(processId, processName)
                    ?? (!string.IsNullOrWhiteSpace(processName)
                        ? new ProcessModel { ProcessId = processId, Name = NormalizeProcessName(processName) }
                        : null);

                if (process != null)
                {
                    this.runningProcesses.TryAdd(processId, process);
                    this.ProcessStarted?.Invoke(this, new ProcessEventArgs(process));
                }
            }
            catch (Exception ex)
            {
                this.OnMonitoringStatusChanged($"Error handling process start event: {ex.Message}", ex);
            }
        }

        private void OnProcessStopped(object sender, EventArrivedEventArgs e)
        {
            if (!this.isMonitoring || this.IsDisposed)
            {
                return;
            }

            try
            {
                var processId = Convert.ToInt32(e.NewEvent["ProcessID"]);

                if (this.runningProcesses.TryRemove(processId, out var process))
                {
                    this.ProcessStopped?.Invoke(this, new ProcessEventArgs(process));
                }
            }
            catch (Exception ex)
            {
                this.OnMonitoringStatusChanged($"Error handling process stop event: {ex.Message}", ex);
            }
        }

        private void FallbackPollingCallback(object? state)
        {
            if (!this.isMonitoring || this.IsDisposed || this.cancellationTokenSource?.Token.IsCancellationRequested == true)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref this.disposedFlag, 0, 0) == 1)
            {
                return;
            }

            // Prevent overlapping polling iterations when processing takes longer than interval
            if (Interlocked.Exchange(ref this.isFallbackPollingInProgress, 1) == 1)
            {
                return;
            }

            var cancellationToken = this.cancellationTokenSource?.Token ?? CancellationToken.None;
            TaskSafety.FireAndForget(this.RunFallbackPollingAsync(cancellationToken), ex =>
            {
                this.OnMonitoringStatusChanged($"Error in fallback polling: {ex.Message}", ex);
            });
        }

        private async Task RunFallbackPollingAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                var currentProcesses = await this.GetRunningProcessesAsync().ConfigureAwait(false);
                var detectedChanges = 0;

                this.pollBuffer.Clear();
                foreach (var process in currentProcesses)
                {
                    this.pollBuffer[process.ProcessId] = process;
                }

                // Check for new processes
                foreach (var process in currentProcesses)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    if (!this.runningProcesses.ContainsKey(process.ProcessId))
                    {
                        this.runningProcesses.TryAdd(process.ProcessId, process);
                        this.ProcessStarted?.Invoke(this, new ProcessEventArgs(process));
                        detectedChanges++;
                    }
                }

                // Check for stopped processes
                var stoppedProcesses = this.runningProcesses.Keys
                    .Where(pid => !this.pollBuffer.ContainsKey(pid))
                    .ToList();

                foreach (var pid in stoppedProcesses)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    if (this.runningProcesses.TryRemove(pid, out var stoppedProcess))
                    {
                        this.ProcessStopped?.Invoke(this, new ProcessEventArgs(stoppedProcess));
                        detectedChanges++;
                    }
                }

                // Periodically retry WMI monitoring recovery while polling is active
                await this.TryRecoverWmiMonitoringAsync().ConfigureAwait(false);

                this.UpdateAdaptivePollingInterval(detectedChanges);
            }
            catch (Exception ex)
            {
                this.OnMonitoringStatusChanged($"Error in fallback polling: {ex.Message}", ex);
            }
            finally
            {
                Interlocked.Exchange(ref this.isFallbackPollingInProgress, 0);
            }
        }

        private void UpdateAdaptivePollingInterval(int detectedChanges)
        {
            if (!this.isFallbackPollingActive || this.fallbackTimer == null)
            {
                return;
            }

            var previousMultiplier = this.idlePollingMultiplier;
            this.idlePollingMultiplier = detectedChanges > 0
                ? 1
                : Math.Min(MaxIdlePollingMultiplier, this.idlePollingMultiplier + 1);

            var nextInterval = Math.Clamp(
                this.fallbackPollingIntervalMs * this.idlePollingMultiplier,
                this.fallbackPollingIntervalMs,
                60000);

            if (nextInterval == this.currentFallbackPollingIntervalMs)
            {
                return;
            }

            this.currentFallbackPollingIntervalMs = nextInterval;
            this.fallbackTimer.Change(this.currentFallbackPollingIntervalMs, this.currentFallbackPollingIntervalMs);

            if (previousMultiplier != this.idlePollingMultiplier)
            {
                this.OnMonitoringStatusChanged($"Adaptive polling interval changed to {this.currentFallbackPollingIntervalMs}ms");
            }
        }

        private async Task TryRecoverWmiMonitoringAsync()
        {
            if (!this.isMonitoring || this.IsDisposed || this.isWmiAvailable || !this.enableWmiMonitoring)
            {
                return;
            }

            var now = DateTime.UtcNow;
            if ((now - this.lastWmiRetryAttemptUtc).TotalMilliseconds < this.wmiRetryDelayMs)
            {
                return;
            }

            if (Interlocked.Exchange(ref this.isWmiRecoveryInProgress, 1) == 1)
            {
                return;
            }

            this.lastWmiRetryAttemptUtc = now;

            try
            {
                var recovered = await this.TryStartWmiMonitoringAsync().ConfigureAwait(false);
                if (recovered)
                {
                    this.OnMonitoringStatusChanged("WMI monitoring recovered successfully");
                }
            }
            finally
            {
                Interlocked.Exchange(ref this.isWmiRecoveryInProgress, 0);
            }
        }

        private async Task<ProcessModel?> CreateProcessModelFromId(int processId, string processName)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                var normalizedName = !string.IsNullOrWhiteSpace(processName)
                    ? NormalizeProcessName(processName)
                    : NormalizeProcessName(process.ProcessName);

                return new ProcessModel
                {
                    ProcessId = process.Id,
                    Name = normalizedName,
                    CpuUsage = 0,
                    MemoryUsage = process.PrivateMemorySize64,
                    Priority = process.PriorityClass,
                    ProcessorAffinity = (long)process.ProcessorAffinity,
                    MainWindowHandle = process.MainWindowHandle,
                    MainWindowTitle = process.MainWindowTitle ?? string.Empty,
                    HasVisibleWindow = process.MainWindowHandle != IntPtr.Zero && !string.IsNullOrWhiteSpace(process.MainWindowTitle),
                    ExecutablePath = process.MainModule?.FileName ?? string.Empty,
                };
            }
            catch (Exception ex)
            {
                this.logger?.LogDebug(ex, "Process {ProcessId} terminated before access", processId);
                return null;
            }
        }

        private void OnMonitoringStatusChanged(string? message = null, Exception? error = null)
        {
            this.MonitoringStatusChanged?.Invoke(this, new MonitoringStatusEventArgs(
                this.isMonitoring, this.isWmiAvailable, this.isFallbackPollingActive, message, error));
        }

        private void UpdateMonitoringSettings()
        {
            var settings = this.settingsService.Settings;
            this.fallbackPollingIntervalMs = Math.Clamp(settings.FallbackPollingIntervalMs, 1000, 60000);
            this.enableWmiMonitoring = settings.EnableWmiMonitoring;
            this.enableFallbackPolling = settings.EnableFallbackPolling;
        }

        public void UpdateSettings()
        {
            var previousInterval = this.fallbackPollingIntervalMs;
            var previousWmiEnabled = this.enableWmiMonitoring;
            var previousFallbackEnabled = this.enableFallbackPolling;

            this.UpdateMonitoringSettings();

            if (!this.isMonitoring)
            {
                return;
            }

            if (!this.enableWmiMonitoring && this.isWmiAvailable)
            {
                this.StopWmiWatchers();
                this.OnMonitoringStatusChanged("WMI monitoring disabled by settings");
            }
            else if (this.enableWmiMonitoring && !previousWmiEnabled && !this.isWmiAvailable)
            {
                TaskSafety.FireAndForget(this.TryStartWmiMonitoringAsync(), ex =>
                {
                    this.OnMonitoringStatusChanged($"Error recovering WMI monitoring: {ex.Message}", ex);
                });
            }

            if (!this.enableFallbackPolling && this.isFallbackPollingActive)
            {
                this.StopFallbackPolling();
                this.OnMonitoringStatusChanged("Fallback polling disabled by settings");
            }
            else if (this.enableFallbackPolling && !previousFallbackEnabled && (!this.isWmiAvailable || !this.enableWmiMonitoring))
            {
                this.StartFallbackPolling();
            }

            // If fallback polling is active, restart it with new interval
            if (this.isFallbackPollingActive && this.fallbackTimer != null && previousInterval != this.fallbackPollingIntervalMs)
            {
                this.idlePollingMultiplier = 1;
                this.currentFallbackPollingIntervalMs = this.fallbackPollingIntervalMs;
                this.fallbackTimer.Change(0, this.currentFallbackPollingIntervalMs);
                this.OnMonitoringStatusChanged($"Polling interval updated to {this.fallbackPollingIntervalMs}ms");
            }
        }

        private static string NormalizeProcessName(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
            {
                return string.Empty;
            }

            // Keep process naming consistent with Process.ProcessName (no extension)
            return Path.GetFileNameWithoutExtension(processName.Trim());
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref this.disposedFlag, 1) == 1)
            {
                return;
            }

            try
            {
                this.StopMonitoringAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                this.OnMonitoringStatusChanged($"Error during process monitor disposal: {ex.Message}", ex);
            }

            this.wmiStartSemaphore.Dispose();
        }
    }
}

