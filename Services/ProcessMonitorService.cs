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
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Process monitoring service using WMI events with fallback polling
    /// </summary>
    public class ProcessMonitorService : IProcessMonitorService
    {
        private readonly IProcessService _processService;
        private readonly IApplicationSettingsService _settingsService;
        private readonly object _lockObject = new();
        private readonly ConcurrentDictionary<int, ProcessModel> _runningProcesses = new();
        private readonly SemaphoreSlim _wmiStartSemaphore = new(1, 1);

        private ManagementEventWatcher? _processStartWatcher;
        private ManagementEventWatcher? _processStopWatcher;
        private System.Threading.Timer? _fallbackTimer;
        private CancellationTokenSource? _cancellationTokenSource;

        private bool _isMonitoring;
        private bool _isWmiAvailable;
        private bool _isFallbackPollingActive;
        private bool _disposed;

        // Configuration - will be updated from settings
        private int _fallbackPollingIntervalMs = 5000; // Default 5 seconds
        private readonly int _wmiRetryDelayMs = 10000; // 10 seconds
        private bool _enableWmiMonitoring = true;
        private bool _enableFallbackPolling = true;
        private int _isFallbackPollingInProgress;
        private int _isWmiRecoveryInProgress;
        private DateTime _lastWmiRetryAttemptUtc = DateTime.MinValue;

        public event EventHandler<ProcessEventArgs>? ProcessStarted;
        public event EventHandler<ProcessEventArgs>? ProcessStopped;
        public event EventHandler<MonitoringStatusEventArgs>? MonitoringStatusChanged;

        public bool IsMonitoring => _isMonitoring;
        public bool IsWmiAvailable => _isWmiAvailable;
        public bool IsFallbackPollingActive => _isFallbackPollingActive;

        public ProcessMonitorService(IProcessService processService, IApplicationSettingsService settingsService)
        {
            _processService = processService ?? throw new ArgumentNullException(nameof(processService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

            // Initialize polling interval from settings
            UpdateMonitoringSettings();
        }

        public async Task StartMonitoringAsync()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ProcessMonitorService));
            
            lock (_lockObject)
            {
                if (_isMonitoring) return;
                _isMonitoring = true;
            }

            _cancellationTokenSource = new CancellationTokenSource();
            _lastWmiRetryAttemptUtc = DateTime.MinValue;
            Interlocked.Exchange(ref _isFallbackPollingInProgress, 0);
            Interlocked.Exchange(ref _isWmiRecoveryInProgress, 0);

            UpdateMonitoringSettings();

            // Initialize current process list
            await InitializeProcessListAsync();

            bool wmiStarted = false;
            if (_enableWmiMonitoring)
            {
                // Try to start WMI monitoring first
                wmiStarted = await TryStartWmiMonitoringAsync();
            }

            if (!wmiStarted && _enableFallbackPolling)
            {
                // Fall back to polling if WMI is not available
                StartFallbackPolling();
            }
            else if (!wmiStarted && !_enableFallbackPolling)
            {
                var reason = _enableWmiMonitoring
                    ? "WMI monitoring unavailable and fallback polling is disabled"
                    : "Both WMI monitoring and fallback polling are disabled";

                OnMonitoringStatusChanged(reason);
            }

            OnMonitoringStatusChanged();
        }

        public async Task StopMonitoringAsync()
        {
            if (_disposed) return;

            lock (_lockObject)
            {
                if (!_isMonitoring) return;
                _isMonitoring = false;
            }

            // Stop WMI watchers
            StopWmiWatchers();

            // Stop fallback polling
            StopFallbackPolling();

            // Cancel any ongoing operations
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            _runningProcesses.Clear();
            Interlocked.Exchange(ref _isFallbackPollingInProgress, 0);
            Interlocked.Exchange(ref _isWmiRecoveryInProgress, 0);
            OnMonitoringStatusChanged();

            await Task.CompletedTask;
        }

        public async Task<IEnumerable<ProcessModel>> GetRunningProcessesAsync()
        {
            try
            {
                var processes = await _processService.GetProcessesAsync();
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
                var processes = await GetRunningProcessesAsync();
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
                var processes = await GetRunningProcessesAsync();
                _runningProcesses.Clear();
                
                foreach (var process in processes)
                {
                    _runningProcesses.TryAdd(process.ProcessId, process);
                }
            }
            catch (Exception ex)
            {
                OnMonitoringStatusChanged($"Failed to initialize process list: {ex.Message}", ex);
            }
        }

        private async Task<bool> TryStartWmiMonitoringAsync()
        {
            if (_disposed || !_isMonitoring || !_enableWmiMonitoring)
            {
                return false;
            }

            await _wmiStartSemaphore.WaitAsync();
            try
            {
                if (_disposed || !_isMonitoring || !_enableWmiMonitoring)
                {
                    return false;
                }

                if (_isWmiAvailable && _processStartWatcher != null && _processStopWatcher != null)
                {
                    return true;
                }

                await Task.Run(() =>
                {
                    // Ensure any previous watchers are fully cleaned up before re-creating them
                    StopWmiWatchers();

                    // Create WMI event watchers for process start and stop
                    var startQuery = new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace");
                    var stopQuery = new WqlEventQuery("SELECT * FROM Win32_ProcessStopTrace");

                    _processStartWatcher = new ManagementEventWatcher(startQuery);
                    _processStopWatcher = new ManagementEventWatcher(stopQuery);

                    _processStartWatcher.EventArrived += OnProcessStarted;
                    _processStopWatcher.EventArrived += OnProcessStopped;
                    _processStartWatcher.Stopped += OnWmiWatcherStopped;
                    _processStopWatcher.Stopped += OnWmiWatcherStopped;

                    _processStartWatcher.Start();
                    _processStopWatcher.Start();
                });

                _isWmiAvailable = true;

                // Prefer WMI when available to reduce polling overhead
                if (_isFallbackPollingActive)
                {
                    StopFallbackPolling();
                }

                OnMonitoringStatusChanged("WMI monitoring started successfully");
                return true;
            }
            catch (Exception ex)
            {
                _isWmiAvailable = false;
                OnMonitoringStatusChanged($"WMI monitoring failed: {ex.Message}", ex);
                
                // Clean up any partially created watchers
                StopWmiWatchers();
                
                return false;
            }
            finally
            {
                _wmiStartSemaphore.Release();
            }
        }

        private void StartFallbackPolling()
        {
            if (_disposed || !_isMonitoring || !_enableFallbackPolling)
            {
                return;
            }

            // Update polling interval from current settings
            UpdateMonitoringSettings();

            if (_isFallbackPollingActive)
            {
                _fallbackTimer?.Change(0, _fallbackPollingIntervalMs);
                return;
            }

            _isFallbackPollingActive = true;
            _fallbackTimer = new System.Threading.Timer(FallbackPollingCallback, null, 0, _fallbackPollingIntervalMs);
            OnMonitoringStatusChanged($"Fallback polling started (interval: {_fallbackPollingIntervalMs}ms)");
        }

        private void OnWmiWatcherStopped(object sender, StoppedEventArgs e)
        {
            if (!_isMonitoring || _disposed)
            {
                return;
            }

            _isWmiAvailable = false;
            OnMonitoringStatusChanged($"WMI watcher stopped ({e.Status})");

            if (_enableFallbackPolling && !_isFallbackPollingActive)
            {
                StartFallbackPolling();
            }
        }

        private void StopWmiWatchers()
        {
            try
            {
                if (_processStartWatcher != null)
                {
                    _processStartWatcher.EventArrived -= OnProcessStarted;
                    _processStartWatcher.Stopped -= OnWmiWatcherStopped;
                }

                _processStartWatcher?.Stop();
                _processStartWatcher?.Dispose();
                _processStartWatcher = null;

                if (_processStopWatcher != null)
                {
                    _processStopWatcher.EventArrived -= OnProcessStopped;
                    _processStopWatcher.Stopped -= OnWmiWatcherStopped;
                }

                _processStopWatcher?.Stop();
                _processStopWatcher?.Dispose();
                _processStopWatcher = null;

                _isWmiAvailable = false;
            }
            catch (Exception ex)
            {
                OnMonitoringStatusChanged($"Error stopping WMI watchers: {ex.Message}", ex);
            }
        }

        private void StopFallbackPolling()
        {
            _fallbackTimer?.Dispose();
            _fallbackTimer = null;
            _isFallbackPollingActive = false;
            Interlocked.Exchange(ref _isFallbackPollingInProgress, 0);
        }

        private void OnProcessStarted(object sender, EventArrivedEventArgs e)
        {
            TaskSafety.FireAndForget(HandleProcessStartedAsync(e), ex =>
            {
                OnMonitoringStatusChanged($"Error handling process start event: {ex.Message}", ex);
            });
        }

        private async Task HandleProcessStartedAsync(EventArrivedEventArgs e)
        {
            if (!_isMonitoring || _disposed)
            {
                return;
            }

            try
            {
                var processId = Convert.ToInt32(e.NewEvent["ProcessID"]);
                var processName = e.NewEvent["ProcessName"]?.ToString() ?? string.Empty;

                // Get detailed process information
                var process = await CreateProcessModelFromId(processId, processName)
                    ?? (!string.IsNullOrWhiteSpace(processName)
                        ? new ProcessModel { ProcessId = processId, Name = NormalizeProcessName(processName) }
                        : null);

                if (process != null)
                {
                    _runningProcesses.TryAdd(processId, process);
                    ProcessStarted?.Invoke(this, new ProcessEventArgs(process));
                }
            }
            catch (Exception ex)
            {
                OnMonitoringStatusChanged($"Error handling process start event: {ex.Message}", ex);
            }
        }

        private void OnProcessStopped(object sender, EventArrivedEventArgs e)
        {
            if (!_isMonitoring || _disposed)
            {
                return;
            }

            try
            {
                var processId = Convert.ToInt32(e.NewEvent["ProcessID"]);
                
                if (_runningProcesses.TryRemove(processId, out var process))
                {
                    ProcessStopped?.Invoke(this, new ProcessEventArgs(process));
                }
            }
            catch (Exception ex)
            {
                OnMonitoringStatusChanged($"Error handling process stop event: {ex.Message}", ex);
            }
        }

        private void FallbackPollingCallback(object? state)
        {
            if (!_isMonitoring || _disposed || _cancellationTokenSource?.Token.IsCancellationRequested == true)
            {
                return;
            }

            // Prevent overlapping polling iterations when processing takes longer than interval
            if (Interlocked.Exchange(ref _isFallbackPollingInProgress, 1) == 1)
            {
                return;
            }

            var cancellationToken = _cancellationTokenSource?.Token ?? CancellationToken.None;
            TaskSafety.FireAndForget(RunFallbackPollingAsync(cancellationToken), ex =>
            {
                OnMonitoringStatusChanged($"Error in fallback polling: {ex.Message}", ex);
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

                var currentProcesses = await GetRunningProcessesAsync();
                var currentProcessDict = currentProcesses.ToDictionary(p => p.ProcessId, p => p);

                // Check for new processes
                foreach (var process in currentProcesses)
                {
                    if (!_runningProcesses.ContainsKey(process.ProcessId))
                    {
                        _runningProcesses.TryAdd(process.ProcessId, process);
                        ProcessStarted?.Invoke(this, new ProcessEventArgs(process));
                    }
                }

                // Check for stopped processes
                var stoppedProcesses = _runningProcesses.Keys
                    .Where(pid => !currentProcessDict.ContainsKey(pid))
                    .ToList();

                foreach (var pid in stoppedProcesses)
                {
                    if (_runningProcesses.TryRemove(pid, out var stoppedProcess))
                    {
                        ProcessStopped?.Invoke(this, new ProcessEventArgs(stoppedProcess));
                    }
                }

                // Periodically retry WMI monitoring recovery while polling is active
                await TryRecoverWmiMonitoringAsync();
            }
            catch (Exception ex)
            {
                OnMonitoringStatusChanged($"Error in fallback polling: {ex.Message}", ex);
            }
            finally
            {
                Interlocked.Exchange(ref _isFallbackPollingInProgress, 0);
            }
        }

        private async Task TryRecoverWmiMonitoringAsync()
        {
            if (!_isMonitoring || _disposed || _isWmiAvailable || !_enableWmiMonitoring)
            {
                return;
            }

            var now = DateTime.UtcNow;
            if ((now - _lastWmiRetryAttemptUtc).TotalMilliseconds < _wmiRetryDelayMs)
            {
                return;
            }

            if (Interlocked.Exchange(ref _isWmiRecoveryInProgress, 1) == 1)
            {
                return;
            }

            _lastWmiRetryAttemptUtc = now;

            try
            {
                var recovered = await TryStartWmiMonitoringAsync();
                if (recovered)
                {
                    OnMonitoringStatusChanged("WMI monitoring recovered successfully");
                }
            }
            finally
            {
                Interlocked.Exchange(ref _isWmiRecoveryInProgress, 0);
            }
        }

        private async Task<ProcessModel?> CreateProcessModelFromId(int processId, string processName)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                var model = new ProcessModel { Process = process };
                return model;
            }
            catch
            {
                // Process may have already terminated
                return null;
            }
        }

        private void OnMonitoringStatusChanged(string? message = null, Exception? error = null)
        {
            MonitoringStatusChanged?.Invoke(this, new MonitoringStatusEventArgs(
                _isMonitoring, _isWmiAvailable, _isFallbackPollingActive, message, error));
        }

        private void UpdateMonitoringSettings()
        {
            var settings = _settingsService.Settings;
            _fallbackPollingIntervalMs = Math.Clamp(settings.FallbackPollingIntervalMs, 1000, 60000);
            _enableWmiMonitoring = settings.EnableWmiMonitoring;
            _enableFallbackPolling = settings.EnableFallbackPolling;
        }

        public void UpdateSettings()
        {
            var previousInterval = _fallbackPollingIntervalMs;
            var previousWmiEnabled = _enableWmiMonitoring;
            var previousFallbackEnabled = _enableFallbackPolling;

            UpdateMonitoringSettings();

            if (!_isMonitoring)
            {
                return;
            }

            if (!_enableWmiMonitoring && _isWmiAvailable)
            {
                StopWmiWatchers();
                OnMonitoringStatusChanged("WMI monitoring disabled by settings");
            }
            else if (_enableWmiMonitoring && !previousWmiEnabled && !_isWmiAvailable)
            {
                TaskSafety.FireAndForget(TryStartWmiMonitoringAsync(), ex =>
                {
                    OnMonitoringStatusChanged($"Error recovering WMI monitoring: {ex.Message}", ex);
                });
            }

            if (!_enableFallbackPolling && _isFallbackPollingActive)
            {
                StopFallbackPolling();
                OnMonitoringStatusChanged("Fallback polling disabled by settings");
            }
            else if (_enableFallbackPolling && !previousFallbackEnabled && (!_isWmiAvailable || !_enableWmiMonitoring))
            {
                StartFallbackPolling();
            }

            // If fallback polling is active, restart it with new interval
            if (_isFallbackPollingActive && _fallbackTimer != null && previousInterval != _fallbackPollingIntervalMs)
            {
                _fallbackTimer.Change(0, _fallbackPollingIntervalMs);
                OnMonitoringStatusChanged($"Polling interval updated to {_fallbackPollingIntervalMs}ms");
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
            if (_disposed) return;

            try
            {
                StopMonitoringAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                OnMonitoringStatusChanged($"Error during process monitor disposal: {ex.Message}", ex);
            }

            _wmiStartSemaphore.Dispose();

            _disposed = true;
        }
    }
}

