namespace ThreadPilot.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using ThreadPilot.Models;

    public class ProcessMonitorService : IProcessMonitorService
    {
        private readonly IProcessService processService;
        private readonly IApplicationSettingsService settingsService;
        private readonly ILogger<ProcessMonitorService>? logger;
        private readonly ConcurrentDictionary<int, ProcessModel> runningProcesses = new();
        private readonly SemaphoreSlim wmiStartSemaphore = new(1, 1);
        private readonly Dictionary<int, ProcessModel> pollBuffer = new();

        private readonly Func<IProcessWatchers> watcherFactory;
        private readonly IMonitoringWakeEvents wakeEvents;
        private IProcessWatchers? watchers;
        private System.Threading.Timer? fallbackTimer;
        private System.Threading.Timer? watchdogTimer;
        private long lastWmiEventTicks;
        private int recoveryRequested;
        private CancellationTokenSource? cancellationTokenSource;

        private bool isMonitoring;
        private bool isWmiAvailable;
        private bool isFallbackPollingActive;
        private int disposedFlag;
        private int disposeRequestedFlag;

        // Configuration - will be updated from settings
        private int fallbackPollingIntervalMs = 5000; // Default 5 seconds
        private int currentFallbackPollingIntervalMs = 5000;
        private int idlePollingMultiplier = 1;
        private const int MaxIdlePollingMultiplier = 6;
        private bool enableWmiMonitoring = true;
        private bool enableFallbackPolling = true;
        private int isFallbackPollingInProgress;
        private int isWmiRecoveryInProgress;
        private int wmiRecoveryFailureCount;
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
            : this(processService, settingsService, logger, () => new WmiProcessWatchers(), new WindowsMonitoringWakeEvents())
        {
        }

        internal ProcessMonitorService(
            IProcessService processService,
            IApplicationSettingsService settingsService,
            ILogger<ProcessMonitorService>? logger,
            Func<IProcessWatchers> watcherFactory,
            IMonitoringWakeEvents wakeEvents)
        {
            this.watcherFactory = watcherFactory;
            this.wakeEvents = wakeEvents;
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

            await this.wmiStartSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                if (this.IsDisposed || Volatile.Read(ref this.disposeRequestedFlag) != 0)
                {
                    throw new ObjectDisposedException(nameof(ProcessMonitorService));
                }

                if (this.isMonitoring)
                {
                    return;
                }

                this.isMonitoring = true;
                this.cancellationTokenSource = new CancellationTokenSource();
                this.lastWmiRetryAttemptUtc = DateTime.MinValue;

                this.UpdateMonitoringSettings();

                bool wmiStarted = false;
                if (this.enableWmiMonitoring)
                {
                    // Try to start WMI monitoring first
                    wmiStarted = await this.TryStartWmiMonitoringAsync().ConfigureAwait(false);
                }

                if (!wmiStarted && this.enableFallbackPolling)
                {
                    this.lastWmiRetryAttemptUtc = DateTime.UtcNow;
                    this.wmiRecoveryFailureCount = 0;
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

                // Subscribe before taking the snapshot, so starts during enumeration are not lost.
                await this.InitializeProcessListAsync().ConfigureAwait(false);
                this.wakeEvents.Wake += this.OnWake;
                this.wakeEvents.Start();
                this.watchdogTimer = new System.Threading.Timer(_ => this.QueueHealthCheck(), null, 10000, 10000);
                this.OnMonitoringStatusChanged();
            }
            catch
            {
                this.isMonitoring = false;
                this.wakeEvents.Wake -= this.OnWake;
                this.wakeEvents.Dispose();
                this.watchdogTimer?.Dispose();
                this.watchdogTimer = null;
                this.StopWmiWatchers();
                this.StopFallbackPolling();
                this.cancellationTokenSource?.Cancel();
                this.cancellationTokenSource?.Dispose();
                this.cancellationTokenSource = null;
                this.runningProcesses.Clear();
                throw;
            }
            finally
            {
                this.wmiStartSemaphore.Release();
            }
        }

        public async Task StopMonitoringAsync()
        {
            if (this.IsDisposed)
            {
                return;
            }

            var semaphoreHeld = false;
            try
            {
                await this.wmiStartSemaphore.WaitAsync().ConfigureAwait(false);
                semaphoreHeld = true;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            try
            {
                if (!this.isMonitoring)
                {
                    return;
                }

                this.isMonitoring = false;

                this.wakeEvents.Wake -= this.OnWake;
                this.wakeEvents.Dispose();
                this.watchdogTimer?.Dispose();
                this.watchdogTimer = null;
                Interlocked.Exchange(ref this.recoveryRequested, 0);

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
                this.OnMonitoringStatusChanged();
            }
            finally
            {
                if (semaphoreHeld)
                {
                    try
                    {
                        this.wmiStartSemaphore.Release();
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                }
            }
        }

        public async Task<IEnumerable<ProcessModel>> GetRunningProcessesAsync()
        {
            if (this.isMonitoring)
            {
                return this.runningProcesses.Values.ToArray();
            }

            try
            {
                return await this.processService.GetProcessesAsync().ConfigureAwait(false);
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
                var previousIds = this.runningProcesses.Keys.ToArray();
                var processes = await this.processService.GetProcessesAsync().ConfigureAwait(false);
                var currentIds = processes.Select(p => p.ProcessId).ToHashSet();
                foreach (var process in processes)
                {
                    this.runningProcesses[process.ProcessId] = process;
                }

                foreach (var pid in previousIds.Where(pid => !currentIds.Contains(pid)))
                {
                    if (this.runningProcesses.TryRemove(pid, out var stopped))
                    {
                        this.ProcessStopped?.Invoke(this, new ProcessEventArgs(stopped));
                    }
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

            try
            {
                if (this.isWmiAvailable && this.watchers != null)
                {
                    return true;
                }

                await Task.Run(() =>
                {
                    // Ensure any previous watchers are fully cleaned up before re-creating them
                    this.StopWmiWatchers();

                    var current = this.watcherFactory();
                    this.watchers = current;
                    current.Start(
                        (pid, name) =>
                        {
                            if (ReferenceEquals(this.watchers, current))
                            {
                                this.HandleProcessStarted(pid, name);
                            }
                        },
                        pid =>
                        {
                            if (ReferenceEquals(this.watchers, current))
                            {
                                this.HandleProcessStopped(pid);
                            }
                        },
                        reason =>
                        {
                            if (ReferenceEquals(this.watchers, current))
                            {
                                this.OnWmiWatcherStopped(reason);
                            }
                        });
                }).ConfigureAwait(false);

                this.isWmiAvailable = Volatile.Read(ref this.recoveryRequested) == 0;
                Interlocked.Exchange(ref this.lastWmiEventTicks, DateTime.UtcNow.Ticks);
                if (!this.isWmiAvailable)
                {
                    throw new InvalidOperationException("WMI subscription stopped during startup");
                }

                this.wmiRecoveryFailureCount = 0;
                this.lastWmiRetryAttemptUtc = DateTime.MinValue;

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

        private void OnWmiWatcherStopped(string reason)
        {
            if (!this.isMonitoring || this.IsDisposed)
            {
                return;
            }

            this.isWmiAvailable = false;
            Interlocked.Exchange(ref this.recoveryRequested, 1);
            this.OnMonitoringStatusChanged($"WMI watcher stopped ({reason}); recovery requested");
            this.QueueHealthCheck();
        }

        private void StopWmiWatchers()
        {
            try
            {
                var retired = this.watchers;
                this.watchers = null;
                this.isWmiAvailable = false;
                retired?.Dispose();
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
        }

        private void HandleProcessStarted(int processId, string processName)
        {
            if (!this.isMonitoring || this.IsDisposed)
            {
                return;
            }

            try
            {
                Interlocked.Exchange(ref this.lastWmiEventTicks, DateTime.UtcNow.Ticks);
                var process = !string.IsNullOrWhiteSpace(processName)
                    ? new ProcessModel
                    {
                        ProcessId = processId,
                        Name = NormalizeProcessName(processName),
                        Classification = ProcessClassification.Unknown,
                    }
                    : null;

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

        private void HandleProcessStopped(int processId)
        {
            if (!this.isMonitoring || this.IsDisposed)
            {
                return;
            }

            try
            {
                Interlocked.Exchange(ref this.lastWmiEventTicks, DateTime.UtcNow.Ticks);
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
            await this.wmiStartSemaphore.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                var currentProcesses = await this.processService.GetProcessesAsync().ConfigureAwait(false);
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

                this.UpdateAdaptivePollingInterval(detectedChanges);
            }
            catch (Exception ex)
            {
                this.OnMonitoringStatusChanged($"Error in fallback polling: {ex.Message}", ex);
            }
            finally
            {
                this.wmiStartSemaphore.Release();
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

        private void OnWake(object? sender, EventArgs e)
        {
            if (!this.isMonitoring || this.IsDisposed)
            {
                return;
            }

            Interlocked.Exchange(ref this.recoveryRequested, 1);
            this.OnMonitoringStatusChanged("Resume/session unlock: monitoring recovery requested");
            this.QueueHealthCheck();
        }

        private void QueueHealthCheck()
        {
            TaskSafety.FireAndForget(this.CheckHealthAsync(DateTime.UtcNow), ex =>
                this.OnMonitoringStatusChanged($"Monitoring watchdog failed: {ex.Message}", ex));
        }

        internal async Task CheckHealthAsync(DateTime now)
        {
            if (this.IsDisposed || !this.isMonitoring || Interlocked.Exchange(ref this.isWmiRecoveryInProgress, 1) == 1)
            {
                return;
            }

            try
            {
                await this.wmiStartSemaphore.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (this.IsDisposed || !this.isMonitoring)
                    {
                        return;
                    }

                    var requested = Interlocked.Exchange(ref this.recoveryRequested, 0) == 1;
                    var silent = this.enableWmiMonitoring && this.isWmiAvailable &&
                        now - new DateTime(Interlocked.Read(ref this.lastWmiEventTicks), DateTimeKind.Utc) >= TimeSpan.FromMinutes(2);
                    if (!requested && !silent && (this.isWmiAvailable || !this.enableWmiMonitoring ||
                        now - this.lastWmiRetryAttemptUtc < GetWmiRetryDelay(this.wmiRecoveryFailureCount)))
                    {
                        return;
                    }

                    this.OnMonitoringStatusChanged(requested ? "Recovering monitoring after wake/watcher stop" :
                        silent ? "Recovering monitoring after WMI event silence" : "Retrying unavailable WMI monitoring");
                    this.StopWmiWatchers();
                    this.lastWmiRetryAttemptUtc = now;
                    if (this.enableWmiMonitoring && await this.TryStartWmiMonitoringAsync().ConfigureAwait(false))
                    {
                        this.wmiRecoveryFailureCount = 0;
                    }
                    else
                    {
                        this.wmiRecoveryFailureCount++;
                        this.StartFallbackPolling();
                    }

                    // A restarted subscription cannot replay starts missed while Windows/WMI was unavailable.
                    await this.InitializeProcessListAsync().ConfigureAwait(false);
                    this.OnMonitoringStatusChanged("Monitoring recovery: process snapshot refreshed", snapshotRefreshed: true);
                }
                finally
                {
                    this.wmiStartSemaphore.Release();
                }
            }
            finally
            {
                Interlocked.Exchange(ref this.isWmiRecoveryInProgress, 0);
                // A wake/Stopped notification may have arrived while the recovery gate was held.
                // Leave it for the next watchdog tick rather than spin if WMI repeatedly fails.
            }
        }

        internal static TimeSpan GetWmiRetryDelay(int failureCount) =>
            failureCount switch
            {
                <= 0 => TimeSpan.FromSeconds(10),
                1 => TimeSpan.FromSeconds(30),
                2 => TimeSpan.FromMinutes(1),
                _ => TimeSpan.FromMinutes(5),
            };

        private void OnMonitoringStatusChanged(string? message = null, Exception? error = null, bool snapshotRefreshed = false)
        {
            if (message != null)
            {
                this.logger?.LogInformation(error, "Process monitoring: {Message}", message);
            }

            this.MonitoringStatusChanged?.Invoke(this, new MonitoringStatusEventArgs(
                this.isMonitoring, this.isWmiAvailable, this.isFallbackPollingActive, message, error, snapshotRefreshed));
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
            TaskSafety.FireAndForget(this.UpdateSettingsAsync(), ex =>
                this.OnMonitoringStatusChanged($"Error updating monitoring settings: {ex.Message}", ex));
        }

        private async Task UpdateSettingsAsync()
        {
            await this.wmiStartSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                if (this.IsDisposed)
                {
                    return;
                }

                var previousInterval = this.fallbackPollingIntervalMs;
                var previousWmiEnabled = this.enableWmiMonitoring;
                var previousFallbackEnabled = this.enableFallbackPolling;

                this.UpdateMonitoringSettings();
                this.wmiRecoveryFailureCount = 0;
                this.lastWmiRetryAttemptUtc = DateTime.MinValue;

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
                    if (!await this.TryStartWmiMonitoringAsync().ConfigureAwait(false))
                    {
                        this.StartFallbackPolling();
                    }
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
            finally
            {
                this.wmiStartSemaphore.Release();
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
            if (Interlocked.Exchange(ref this.disposeRequestedFlag, 1) == 1)
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

            Interlocked.Exchange(ref this.disposedFlag, 1);
            // Queued timer callbacks may still enter the gate after Stop. Keep the managed
            // semaphore alive so they can observe isMonitoring=false and leave safely.
            this.wakeEvents.Dispose();
        }
    }
}
