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
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using ThreadPilot.Models;

    /// <summary>
    /// Main orchestration service that coordinates process monitoring and power plan management.
    /// </summary>
    public class ProcessMonitorManagerService : IProcessMonitorManagerService
    {
        private readonly IProcessMonitorService processMonitorService;
        private readonly IProcessPowerPlanAssociationService associationService;
        private readonly IPowerPlanService powerPlanService;
        private readonly INotificationService notificationService;
        private readonly IApplicationSettingsService settingsService;
        private readonly IProcessService processService;
        private readonly ICoreMaskService coreMaskService;
        private readonly ILogger<ProcessMonitorManagerService> logger;
        private readonly IEnhancedLoggingService enhancedLogger;
        private readonly object lockObject = new();

        private readonly ConcurrentDictionary<int, ProcessModel> runningAssociatedProcesses = new();
        private readonly System.Threading.Timer delayTimer;
        private readonly SemaphoreSlim powerPlanChangeSemaphore = new(1, 1);
        private readonly SemaphoreSlim stateMutationSemaphore = new(1, 1);

        private bool isRunning;
        private string status = "Stopped";
        private bool disposed;
        private ProcessMonitorConfiguration? configuration;
        private int pendingPowerPlanReevaluation;

        public event EventHandler<ProcessPowerPlanChangeEventArgs>? ProcessPowerPlanChanged;

        public event EventHandler<ServiceStatusEventArgs>? ServiceStatusChanged;

        public bool IsRunning => this.isRunning;

        public string Status => this.status;

        public IEnumerable<ProcessModel> RunningAssociatedProcesses => this.runningAssociatedProcesses.Values.ToList();

        public ProcessMonitorManagerService(
            IProcessMonitorService processMonitorService,
            IProcessPowerPlanAssociationService associationService,
            IPowerPlanService powerPlanService,
            INotificationService notificationService,
            IApplicationSettingsService settingsService,
            IProcessService processService,
            ICoreMaskService coreMaskService,
            ILogger<ProcessMonitorManagerService> logger,
            IEnhancedLoggingService enhancedLogger)
        {
            this.processMonitorService = processMonitorService ?? throw new ArgumentNullException(nameof(processMonitorService));
            this.associationService = associationService ?? throw new ArgumentNullException(nameof(associationService));
            this.powerPlanService = powerPlanService ?? throw new ArgumentNullException(nameof(powerPlanService));
            this.notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            this.processService = processService ?? throw new ArgumentNullException(nameof(processService));
            this.coreMaskService = coreMaskService ?? throw new ArgumentNullException(nameof(coreMaskService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.enhancedLogger = enhancedLogger ?? throw new ArgumentNullException(nameof(enhancedLogger));

            // Initialize delay timer (used for delayed power plan changes)
            this.delayTimer = new System.Threading.Timer(this.DelayedPowerPlanChangeCallback, null, Timeout.Infinite, Timeout.Infinite);

            // Subscribe to events
            this.processMonitorService.ProcessStarted += this.OnProcessStarted;
            this.processMonitorService.ProcessStopped += this.OnProcessStopped;
            this.processMonitorService.MonitoringStatusChanged += this.OnMonitoringStatusChanged;
            this.associationService.ConfigurationChanged += this.OnConfigurationChanged;
        }

        public async Task StartAsync()
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException(nameof(ProcessMonitorManagerService));
            }

            await this.stateMutationSemaphore.WaitAsync();
            try
            {
                if (this.isRunning)
                {
                    return;
                }

                this.logger.LogInformation("Starting Process Monitor Manager Service");
                await this.enhancedLogger.LogSystemEventAsync(
                    LogEventTypes.System.ServiceStarted,
                    "Process Monitor Manager Service starting");
                this.SetStatus(true, "Starting...");

                // Load configuration
                await this.associationService.LoadConfigurationAsync();
                this.configuration = this.associationService.Configuration;
                this.logger.LogInformation(
                    "Configuration loaded with {AssociationCount} associations",
                    this.configuration.Associations.Count);

                await this.enhancedLogger.LogSystemEventAsync(
                    LogEventTypes.System.ConfigurationLoaded,
                    $"Process monitoring configuration loaded with {this.configuration.Associations.Count} associations");

                // Start process monitoring
                await this.processMonitorService.StartMonitoringAsync();
                this.logger.LogInformation("Process monitoring started");

                Interlocked.Exchange(ref this.pendingPowerPlanReevaluation, 0);

                await this.enhancedLogger.LogProcessMonitoringEventAsync(
                    LogEventTypes.ProcessMonitoring.MonitoringStarted,
                    "ProcessMonitorService", 0, "WMI-based process monitoring started");

                this.isRunning = true;
                this.SetStatus(true, "Running");

                this.logger.LogInformation("Process Monitor Manager Service started successfully");

                await this.enhancedLogger.LogSystemEventAsync(
                    LogEventTypes.System.ServiceStarted,
                    "Process Monitor Manager Service started successfully");
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to start Process Monitor Manager Service");
                await this.enhancedLogger.LogErrorAsync(ex, "ProcessMonitorManagerService.StartAsync",
                    new Dictionary<string, object> { ["ServiceName"] = "ProcessMonitorManagerService" });
                this.isRunning = false;
                this.SetStatus(false, "Failed to start", $"Error: {ex.Message}", ex);
                throw;
            }
            finally
            {
                this.stateMutationSemaphore.Release();
            }

            // Evaluate current processes after startup lock is released
            await this.EvaluateCurrentProcessesAsync();
        }

        public async Task StopAsync()
        {
            await this.stateMutationSemaphore.WaitAsync();
            try
            {
                if (!this.isRunning)
                {
                    return;
                }

                this.SetStatus(false, "Stopping...");

                // Mark as stopped early to prevent new event handling while shutting down
                this.isRunning = false;
                Interlocked.Exchange(ref this.pendingPowerPlanReevaluation, 0);

                // Stop process monitoring
                await this.processMonitorService.StopMonitoringAsync();

                // Clear running processes
                foreach (var processId in this.runningAssociatedProcesses.Keys)
                {
                    this.coreMaskService.UnregisterMaskApplication(processId);
                    this.processService.UntrackProcess(processId);
                }
                this.runningAssociatedProcesses.Clear();

                // Restore default power plan if configured
                if (this.configuration?.DefaultPowerPlanGuid != null)
                {
                    await this.ForceDefaultPowerPlanAsync();
                }

                this.SetStatus(false, "Stopped");
            }
            catch (Exception ex)
            {
                this.SetStatus(false, "Error stopping", $"Error: {ex.Message}", ex);
                throw;
            }
            finally
            {
                this.stateMutationSemaphore.Release();
            }
        }

        public async Task EvaluateCurrentProcessesAsync()
        {
            if (!this.isRunning || this.configuration == null)
            {
                return;
            }

            try
            {
                var currentProcesses = await this.processMonitorService.GetRunningProcessesAsync();
                var associatedProcesses = new List<ProcessModel>();
                var currentProcessIds = new HashSet<int>(currentProcesses.Select(p => p.ProcessId));

                // Remove stale tracked processes that are no longer running
                foreach (var trackedPid in this.runningAssociatedProcesses.Keys)
                {
                    if (!currentProcessIds.Contains(trackedPid) && this.runningAssociatedProcesses.TryRemove(trackedPid, out _))
                    {
                        this.coreMaskService.UnregisterMaskApplication(trackedPid);
                        this.processService.UntrackProcess(trackedPid);
                    }
                }

                // Find all currently running processes that have associations
                foreach (var process in currentProcesses)
                {
                    var association = this.configuration.FindMatchingAssociation(process);
                    if (association != null)
                    {
                        associatedProcesses.Add(process);
                        this.runningAssociatedProcesses[process.ProcessId] = process;
                    }
                }

                // Determine which power plan should be active
                await this.DeterminePowerPlanAsync(associatedProcesses);
            }
            catch (Exception ex)
            {
                this.SetStatus(this.isRunning, "Error evaluating processes", $"Error: {ex.Message}", ex);
            }
        }

        public async Task ForceDefaultPowerPlanAsync()
        {
            if (this.configuration?.DefaultPowerPlanGuid == null)
            {
                return;
            }

            try
            {
                await this.powerPlanChangeSemaphore.WaitAsync();

                var currentPowerPlan = await this.powerPlanService.GetActivePowerPlan();
                var success = await this.powerPlanService.SetActivePowerPlanByGuidAsync(
                    this.configuration.DefaultPowerPlanGuid,
                    this.configuration.PreventDuplicatePowerPlanChanges);

                if (success)
                {
                    var newPowerPlan = await this.powerPlanService.GetPowerPlanByGuidAsync(this.configuration.DefaultPowerPlanGuid);
                    // Note: We don't have a specific process for this event, so we'll use a dummy one
                    var dummyProcess = new ProcessModel { Name = "System", ProcessId = -1 };
                    var dummyAssociation = new ProcessPowerPlanAssociation("System", this.configuration.DefaultPowerPlanGuid, this.configuration.DefaultPowerPlanName);

                    this.ProcessPowerPlanChanged?.Invoke(this, new ProcessPowerPlanChangeEventArgs(
                        dummyProcess, dummyAssociation, currentPowerPlan, newPowerPlan, "DefaultRestored"));

                    // Show notification for default power plan restoration
                    await this.notificationService.ShowPowerPlanChangeNotificationAsync(
                        currentPowerPlan?.Name ?? "Unknown",
                        newPowerPlan?.Name ?? this.configuration.DefaultPowerPlanName,
                        string.Empty);
                }
            }
            catch (Exception ex)
            {
                this.SetStatus(this.isRunning, "Error setting default power plan", $"Error: {ex.Message}", ex);
            }
            finally
            {
                this.powerPlanChangeSemaphore.Release();
            }
        }

        public async Task<PowerPlanModel?> GetCurrentActivePowerPlanAsync()
        {
            return await this.powerPlanService.GetActivePowerPlan();
        }

        public async Task RefreshConfigurationAsync()
        {
            await this.associationService.LoadConfigurationAsync();
            this.configuration = this.associationService.Configuration;
            this.processMonitorService.UpdateSettings();

            if (this.isRunning)
            {
                await this.EvaluateCurrentProcessesAsync();
            }
        }

        private void OnProcessStarted(object? sender, ProcessEventArgs e)
        {
            TaskSafety.FireAndForget(this.OnProcessStartedAsync(e), ex =>
            {
                this.SetStatus(this.isRunning, "Error handling process start", $"Error: {ex.Message}", ex);
            });
        }

        private async Task OnProcessStartedAsync(ProcessEventArgs e)
        {
            if (!this.isRunning || this.configuration == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(e.Process.Name) || e.Process.ProcessId <= 0)
            {
                return;
            }

            try
            {
                await this.enhancedLogger.LogProcessMonitoringEventAsync(
                    LogEventTypes.ProcessMonitoring.Started,
                    e.Process.Name, e.Process.ProcessId, "Process started and detected by monitoring");

                var association = this.configuration.FindMatchingAssociation(e.Process);
                if (association != null)
                {
                    this.runningAssociatedProcesses[e.Process.ProcessId] = e.Process;

                    await this.enhancedLogger.LogProcessMonitoringEventAsync(
                        LogEventTypes.ProcessMonitoring.AssociationTriggered,
                        e.Process.Name, e.Process.ProcessId,
                        $"Process matched association for power plan: {association.PowerPlanName}");

                    // Apply CPU affinity mask if configured
                    await this.ApplyCoreMaskAndPriorityAsync(e.Process, association);

                    // Schedule power plan change with delay if configured
                    if (this.configuration.PowerPlanChangeDelayMs > 0)
                    {
                        Interlocked.Exchange(ref this.pendingPowerPlanReevaluation, 1);
                        this.delayTimer.Change(this.configuration.PowerPlanChangeDelayMs, Timeout.Infinite);

                        await this.enhancedLogger.LogSystemEventAsync(
                            LogEventTypes.System.ConfigurationLoaded,
                            $"Power plan change scheduled with {this.configuration.PowerPlanChangeDelayMs}ms delay for process {e.Process.Name}");
                    }
                    else
                    {
                        await this.ChangePowerPlanForProcess(e.Process, association, "ProcessStarted");
                    }
                }
            }
            catch (Exception ex)
            {
                await this.enhancedLogger.LogErrorAsync(ex, "ProcessMonitorManagerService.OnProcessStarted",
                    new Dictionary<string, object>
                    {
                        ["ProcessName"] = e.Process.Name,
                        ["ProcessId"] = e.Process.ProcessId,
                    });
                this.SetStatus(this.isRunning, "Error handling process start", $"Error: {ex.Message}", ex);
            }
        }

        private void OnProcessStopped(object? sender, ProcessEventArgs e)
        {
            TaskSafety.FireAndForget(this.OnProcessStoppedAsync(e), ex =>
            {
                this.SetStatus(this.isRunning, "Error handling process stop", $"Error: {ex.Message}", ex);
            });
        }

        private async Task OnProcessStoppedAsync(ProcessEventArgs e)
        {
            if (!this.isRunning || this.configuration == null)
            {
                return;
            }

            try
            {
                if (this.runningAssociatedProcesses.TryRemove(e.Process.ProcessId, out _))
                {
                    this.coreMaskService.UnregisterMaskApplication(e.Process.ProcessId);
                    this.processService.UntrackProcess(e.Process.ProcessId);

                    // Check if there are any other associated processes still running
                    var remainingProcesses = this.runningAssociatedProcesses.Values.ToList();
                    await this.DeterminePowerPlanAsync(remainingProcesses);
                }
            }
            catch (Exception ex)
            {
                this.SetStatus(this.isRunning, "Error handling process stop", $"Error: {ex.Message}", ex);
            }
        }

        private void OnMonitoringStatusChanged(object? sender, MonitoringStatusEventArgs e)
        {
            var details = e.StatusMessage ?? (e.IsMonitoring ? "Monitoring active" : "Monitoring inactive");
            this.SetStatus(this.isRunning, $"Monitor: {details}", e.StatusMessage, e.Error);
        }

        private void OnConfigurationChanged(object? sender, ConfigurationChangedEventArgs e)
        {
            this.configuration = this.associationService.Configuration;

            if (this.isRunning)
            {
                TaskSafety.FireAndForget(this.EvaluateCurrentProcessesAsync(), ex =>
                {
                    this.SetStatus(this.isRunning, "Error evaluating processes", $"Error: {ex.Message}", ex);
                });
            }

            // Keep process monitor settings synchronized with configuration edits
            this.processMonitorService.UpdateSettings();
        }

        private void DelayedPowerPlanChangeCallback(object? state)
        {
            TaskSafety.FireAndForget(this.DelayedPowerPlanChangeCallbackAsync(), ex =>
            {
                this.SetStatus(this.isRunning, "Error in delayed power plan callback", $"Error: {ex.Message}", ex);
            });
        }

        private async Task DelayedPowerPlanChangeCallbackAsync()
        {
            if (!this.isRunning)
            {
                return;
            }

            if (Interlocked.Exchange(ref this.pendingPowerPlanReevaluation, 0) == 0)
            {
                return;
            }

            var runningProcesses = this.runningAssociatedProcesses.Values.ToList();
            await this.DeterminePowerPlanAsync(runningProcesses);
        }

        private async Task DeterminePowerPlanAsync(IList<ProcessModel> associatedProcesses)
        {
            if (this.configuration == null)
            {
                return;
            }

            try
            {
                if (associatedProcesses.Any())
                {
                    // Find the highest priority association among running processes
                    var associations = associatedProcesses
                        .Select(p => this.configuration.FindMatchingAssociation(p))
                        .Where(a => a != null)
                        .OrderByDescending(a => a!.Priority)
                        .ToList();

                    if (associations.Any())
                    {
                        var topAssociation = associations.First()!;
                        var matchingProcess = associatedProcesses.First(p => topAssociation.MatchesProcess(p));
                        await this.ChangePowerPlanForProcess(matchingProcess, topAssociation, "ProcessStarted");
                    }
                }
                else
                {
                    // No associated processes running, revert to default
                    if (!string.IsNullOrEmpty(this.configuration.DefaultPowerPlanGuid))
                    {
                        await this.ForceDefaultPowerPlanAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                this.SetStatus(this.isRunning, "Error determining power plan", $"Error: {ex.Message}", ex);
            }
        }

        private async Task ChangePowerPlanForProcess(ProcessModel process, ProcessPowerPlanAssociation association, string action)
        {
            try
            {
                await this.powerPlanChangeSemaphore.WaitAsync();

                var currentPowerPlan = await this.powerPlanService.GetActivePowerPlan();
                var success = await this.powerPlanService.SetActivePowerPlanByGuidAsync(
                    association.PowerPlanGuid,
                    this.configuration?.PreventDuplicatePowerPlanChanges ?? true);

                if (success)
                {
                    var newPowerPlan = await this.powerPlanService.GetPowerPlanByGuidAsync(association.PowerPlanGuid);
                    this.ProcessPowerPlanChanged?.Invoke(this, new ProcessPowerPlanChangeEventArgs(
                        process, association, currentPowerPlan, newPowerPlan, action));

                    // Show notification for power plan change
                    await this.notificationService.ShowPowerPlanChangeNotificationAsync(
                        currentPowerPlan?.Name ?? "Unknown",
                        newPowerPlan?.Name ?? association.PowerPlanName,
                        process.Name);
                }
            }
            catch (Exception ex)
            {
                this.SetStatus(this.isRunning, "Error changing power plan", $"Error: {ex.Message}", ex);
            }
            finally
            {
                this.powerPlanChangeSemaphore.Release();
            }
        }

        private void SetStatus(bool isRunning, string status, string? details = null, Exception? error = null)
        {
            lock (this.lockObject)
            {
                this.status = status;
            }

            this.ServiceStatusChanged?.Invoke(this, new ServiceStatusEventArgs(isRunning, status, details, error));

            // Show error notification if there's an error
            if (error != null)
            {
                TaskSafety.FireAndForget(
                    this.notificationService.ShowErrorNotificationAsync(
                        "Process Monitor Error",
                        details ?? status,
                        error),
                    ex =>
                {
                    this.logger.LogError(ex, "Failed to show error notification");
                });
            }
        }

        public void UpdateSettings()
        {
            // Update the process monitor service with new settings
            this.processMonitorService.UpdateSettings();

            this.logger.LogDebug("ProcessMonitorManagerService settings updated");
        }

        /// <summary>
        /// Applies CPU affinity mask and process priority from association when a process starts
        /// Based on CPUSetSetter's ProgramRule.SetMask pattern.
        /// </summary>
        private async Task ApplyCoreMaskAndPriorityAsync(ProcessModel process, ProcessPowerPlanAssociation association)
        {
            try
            {
                // Apply CPU affinity mask if configured
                if (!string.IsNullOrEmpty(association.CoreMaskId))
                {
                    var coreMask = this.coreMaskService.AvailableMasks.FirstOrDefault(m => m.Id == association.CoreMaskId);
                    if (coreMask != null)
                    {
                        try
                        {
                            var affinity = coreMask.ToProcessorAffinity();
                            if (affinity > 0)
                            {
                                await this.processService.SetProcessorAffinity(process, affinity);
                                this.processService.TrackAppliedMask(process.ProcessId, coreMask.Id);
                                this.coreMaskService.RegisterMaskApplication(process.ProcessId, coreMask.Id);

                                this.logger.LogInformation(
                                    "Applied CPU mask '{MaskName}' (affinity: 0x{Affinity:X}) to process {ProcessName} (PID: {ProcessId})",
                                    coreMask.Name, affinity, process.Name, process.ProcessId);

                                await this.enhancedLogger.LogProcessMonitoringEventAsync(
                                    LogEventTypes.ProcessMonitoring.AssociationTriggered,
                                    process.Name, process.ProcessId,
                                    $"CPU mask '{coreMask.Name}' applied automatically from association");
                            }
                        }
                        catch (Exception ex)
                        {
                            var blockedReason = BuildAffinityOrPriorityBlockedMessage(ex, process.Name, "affinity");
                            if (!string.IsNullOrEmpty(blockedReason))
                            {
                                await this.notificationService.ShowNotificationAsync(
                                    "Affinity blocked",
                                    blockedReason,
                                    NotificationType.Warning);
                            }

                            this.logger.LogWarning(
                                ex,
                                "Failed to apply CPU mask '{MaskName}' to process {ProcessName} (PID: {ProcessId})",
                                coreMask.Name, process.Name, process.ProcessId);

                            await this.enhancedLogger.LogErrorAsync(ex, "ProcessMonitorManagerService.ApplyCoreMaskAndPriorityAsync",
                                new Dictionary<string, object>
                                {
                                    ["ProcessName"] = process.Name,
                                    ["ProcessId"] = process.ProcessId,
                                    ["MaskName"] = coreMask.Name,
                                });
                        }
                    }
                    else
                    {
                        this.logger.LogWarning(
                            "Core mask ID '{CoreMaskId}' not found for process {ProcessName}, skipping affinity application",
                            association.CoreMaskId, process.Name);
                    }
                }

                // Apply process priority if configured
                if (!string.IsNullOrEmpty(association.ProcessPriority))
                {
                    if (Enum.TryParse<ProcessPriorityClass>(association.ProcessPriority, out var priority))
                    {
                        try
                        {
                            var currentPriority = process.Priority;

                            if (!Enum.IsDefined(typeof(ProcessPriorityClass), currentPriority))
                            {
                                try
                                {
                                    await this.processService.RefreshProcessInfo(process);
                                    currentPriority = process.Priority;
                                }
                                catch (Exception refreshEx)
                                {
                                    this.logger.LogDebug(
                                        refreshEx,
                                        "Could not refresh process priority before tracking for {ProcessName} (PID: {ProcessId})",
                                        process.Name, process.ProcessId);
                                }
                            }

                            if (Enum.IsDefined(typeof(ProcessPriorityClass), currentPriority))
                            {
                                this.processService.TrackPriorityChange(process.ProcessId, currentPriority);
                            }

                            await this.processService.SetProcessPriority(process, priority);

                            this.logger.LogInformation(
                                "Applied priority '{Priority}' to process {ProcessName} (PID: {ProcessId})",
                                priority, process.Name, process.ProcessId);

                            await this.enhancedLogger.LogProcessMonitoringEventAsync(
                                LogEventTypes.ProcessMonitoring.AssociationTriggered,
                                process.Name, process.ProcessId,
                                $"Priority '{priority}' applied automatically from association");
                        }
                        catch (Exception ex)
                        {
                            var blockedReason = BuildAffinityOrPriorityBlockedMessage(ex, process.Name, "priority");
                            if (!string.IsNullOrEmpty(blockedReason))
                            {
                                await this.notificationService.ShowNotificationAsync(
                                    "Priority blocked",
                                    blockedReason,
                                    NotificationType.Warning);
                            }

                            this.logger.LogWarning(
                                ex,
                                "Failed to apply priority '{Priority}' to process {ProcessName} (PID: {ProcessId})",
                                priority, process.Name, process.ProcessId);

                            await this.enhancedLogger.LogErrorAsync(ex, "ProcessMonitorManagerService.ApplyCoreMaskAndPriorityAsync",
                                new Dictionary<string, object>
                                {
                                    ["ProcessName"] = process.Name,
                                    ["ProcessId"] = process.ProcessId,
                                    ["Priority"] = priority.ToString(),
                                });
                        }
                    }
                    else
                    {
                        this.logger.LogWarning(
                            "Invalid priority value '{Priority}' for process {ProcessName}, skipping priority application",
                            association.ProcessPriority, process.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(
                    ex,
                    "Error applying CPU mask and priority to process {ProcessName} (PID: {ProcessId})",
                    process.Name, process.ProcessId);

                await this.enhancedLogger.LogErrorAsync(ex, "ProcessMonitorManagerService.ApplyCoreMaskAndPriorityAsync",
                    new Dictionary<string, object>
                    {
                        ["ProcessName"] = process.Name,
                        ["ProcessId"] = process.ProcessId,
                        ["AssociationId"] = association.Id,
                    });
            }
        }

        private static string BuildAffinityOrPriorityBlockedMessage(Exception ex, string processName, string operation)
        {
            var message = ex.Message ?? string.Empty;
            var lowered = message.ToLowerInvariant();

            if (lowered.Contains("access denied") ||
                lowered.Contains("anti-cheat") ||
                lowered.Contains("anti cheat") ||
                lowered.Contains("protected") ||
                lowered.Contains("insufficient privileges") ||
                ex is UnauthorizedAccessException)
            {
                return $"{operation} change blocked by Anti-Cheat/System for '{processName}'.";
            }

            return string.Empty;
        }

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            try
            {
                // Dispose can be called from the WPF UI thread; stop on the thread pool to avoid
                // deadlocking on a captured SynchronizationContext during async shutdown.
                Task.Run(this.StopAsync).GetAwaiter().GetResult();
            }
            finally
            {
                this.processMonitorService.ProcessStarted -= this.OnProcessStarted;
                this.processMonitorService.ProcessStopped -= this.OnProcessStopped;
                this.processMonitorService.MonitoringStatusChanged -= this.OnMonitoringStatusChanged;
                this.associationService.ConfigurationChanged -= this.OnConfigurationChanged;

                this.delayTimer?.Dispose();
                this.powerPlanChangeSemaphore?.Dispose();
                this.stateMutationSemaphore?.Dispose();
                this.processMonitorService?.Dispose();

                this.disposed = true;
            }
        }
    }
}

