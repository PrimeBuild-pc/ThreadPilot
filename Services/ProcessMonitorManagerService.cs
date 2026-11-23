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

    public class ProcessMonitorManagerService : IProcessMonitorManagerService
    {
        private readonly IProcessMonitorService processMonitorService;
        private readonly IProcessPowerPlanAssociationService associationService;
        private readonly IPowerPlanService powerPlanService;
        private readonly INotificationService notificationService;
        private readonly IApplicationSettingsService settingsService;
        private readonly IProcessService processService;
        private readonly ICoreMaskService coreMaskService;
        private readonly IAffinityApplyService affinityApplyService;
        private readonly IPersistentRuleAutoApplyService persistentRuleAutoApplyService;
        private readonly PowerPlanTransitionGate powerPlanTransitionGate;
        private readonly ILogger<ProcessMonitorManagerService> logger;
        private readonly IEnhancedLoggingService enhancedLogger;
        private readonly object lockObject = new();

        private readonly ConcurrentDictionary<int, ProcessModel> runningAssociatedProcesses = new();
        private readonly System.Threading.Timer delayTimer;
        private readonly SemaphoreSlim powerPlanChangeSemaphore = new(1, 1);
        private readonly SemaphoreSlim stateMutationSemaphore = new(1, 1);

        private CancellationTokenSource automationLifetime = new();
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
            IAffinityApplyService affinityApplyService,
            IPersistentRuleAutoApplyService persistentRuleAutoApplyService,
            PowerPlanTransitionGate powerPlanTransitionGate,
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
            this.affinityApplyService = affinityApplyService ?? throw new ArgumentNullException(nameof(affinityApplyService));
            this.persistentRuleAutoApplyService = persistentRuleAutoApplyService ?? throw new ArgumentNullException(nameof(persistentRuleAutoApplyService));
            this.powerPlanTransitionGate = powerPlanTransitionGate ?? throw new ArgumentNullException(nameof(powerPlanTransitionGate));
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

                this.automationLifetime.Dispose();
                this.automationLifetime = new CancellationTokenSource();
                try
                {
                    await this.enhancedLogger.InitializeAsync();
                }
                catch (Exception ex)
                {
                    this.logger.LogWarning(ex, "File diagnostics could not be initialized; automation monitoring will continue");
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
                this.automationLifetime.Cancel();
                this.SetStatus(false, "Failed to start", $"Error: {ex.Message}", ex);
                throw;
            }
            finally
            {
                this.stateMutationSemaphore.Release();
            }

            // Evaluate current processes after startup lock is released
            await this.EvaluateCurrentProcessesAsync(resetRuleAttempts: true);
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
                this.automationLifetime.Cancel();
                Interlocked.Exchange(ref this.pendingPowerPlanReevaluation, 0);

                // Stop process monitoring
                await this.processMonitorService.StopMonitoringAsync();

                // Clear running processes
                foreach (var processId in this.runningAssociatedProcesses.Keys)
                {
                    this.coreMaskService.UnregisterMaskApplication(processId);
                    this.processService.UntrackProcess(processId);
                    this.persistentRuleAutoApplyService.MarkProcessExited(processId);
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

        public async Task SetAutomationMonitoringEnabledAsync(bool enabled)
        {
            if (enabled)
            {
                await this.StartAsync();
            }
            else
            {
                await this.StopAsync();
            }

            var settings = this.settingsService.Settings;
            settings.EnableAutomationMonitoring = enabled;
            await this.settingsService.UpdateSettingsAsync(settings);
        }

        public Task EvaluateCurrentProcessesAsync() => this.EvaluateCurrentProcessesAsync(resetRuleAttempts: false);

        private async Task EvaluateCurrentProcessesAsync(bool resetRuleAttempts)
        {
            if (!this.isRunning || this.configuration == null)
            {
                return;
            }

            try
            {
                var trackedBeforeScan = this.runningAssociatedProcesses.ToArray();
                var currentProcesses = (await this.processMonitorService.GetRunningProcessesAsync()).ToList();
                if (resetRuleAttempts)
                {
                    // A fresh monitoring lifetime or resume must also reapply rules after a short sleep,
                    // even if the last application is still inside the normal duplicate-event cooldown.
                    foreach (var process in currentProcesses)
                    {
                        this.persistentRuleAutoApplyService.MarkProcessExited(process.ProcessId);
                    }
                }

                await this.ApplyPersistentRulesForDiscoveredProcessesAsync(currentProcesses);
                var currentById = currentProcesses.DistinctBy(process => process.ProcessId)
                    .ToDictionary(process => process.ProcessId);

                // A snapshot can contain a different process that reused a tracked PID.
                foreach (var tracked in trackedBeforeScan)
                {
                    if ((!currentById.TryGetValue(tracked.Key, out var current) || !tracked.Value.IsSameInstance(current))
                        && this.runningAssociatedProcesses.TryRemove(tracked))
                    {
                        this.coreMaskService.UnregisterMaskApplication(tracked.Key);
                        this.processService.UntrackProcess(tracked.Key);
                        this.persistentRuleAutoApplyService.MarkProcessExited(tracked.Key);
                    }
                }

                // Find all currently running processes that have associations
                foreach (var process in currentProcesses)
                {
                    var association = this.configuration.FindMatchingAssociation(process);
                    if (association != null)
                    {
                        this.runningAssociatedProcesses[process.ProcessId] = process;
                    }
                }

                // Determine which power plan should be active
                await this.UpdatePowerPlanAsync();
            }
            catch (Exception ex)
            {
                this.SetStatus(this.isRunning, "Error evaluating processes", $"Error: {ex.Message}", ex);
            }
        }

        public Task ForceDefaultPowerPlanAsync() => this.UpdatePowerPlanAsync(forceDefault: true);

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

            var monitoringToken = this.automationLifetime.Token;
            try
            {
                this.logger.LogDebug(
                    "Process started: {ProcessName} (PID: {ProcessId})",
                    e.Process.Name,
                    e.Process.ProcessId);

                await this.ApplyPersistentRulesForProcessStartAsync(e.Process);

                var associationCandidates = this.configuration
                    .GetEnabledAssociations()
                    .Where(association => association.MatchesExecutable(e.Process.Name))
                    .ToList();
                if (associationCandidates.Count > 0)
                {
                    try
                    {
                        await this.processService.RefreshProcessInfo(e.Process);
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogDebug(
                            ex,
                            "Could not enrich process {ProcessName} (PID: {ProcessId}) for association matching",
                            e.Process.Name,
                            e.Process.ProcessId);
                    }
                }

                var association = associationCandidates.FirstOrDefault(candidate => candidate.MatchesProcess(e.Process));
                if (association != null && await this.TrackRunningAssociationAsync(e.Process, monitoringToken))
                {
                    await this.enhancedLogger.LogProcessMonitoringEventAsync(
                        LogEventTypes.ProcessMonitoring.AssociationTriggered,
                        e.Process.Name, e.Process.ProcessId,
                        $"Process matched association for power plan: {association.PowerPlanName}");

                    // Apply CPU affinity mask if configured
                    await this.ApplyCoreMaskAndPriorityAsync(e.Process, association);

                    if (monitoringToken.IsCancellationRequested || !this.IsTrackedAssociation(e.Process))
                    {
                        return;
                    }

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
                        await this.UpdatePowerPlanAsync(startedProcess: e.Process);
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

        private async Task<bool> TrackRunningAssociationAsync(ProcessModel process, CancellationToken monitoringToken)
        {
            if (monitoringToken.IsCancellationRequested)
            {
                return false;
            }

            // Register before checking liveness so a concurrent stop can remove the entry.
            this.runningAssociatedProcesses[process.ProcessId] = process;
            if (await this.processService.IsProcessStillRunning(process) && !monitoringToken.IsCancellationRequested)
            {
                return this.IsTrackedAssociation(process);
            }

            this.runningAssociatedProcesses.TryRemove(KeyValuePair.Create(process.ProcessId, process));
            return false;
        }

        private bool IsTrackedAssociation(ProcessModel process) =>
            this.runningAssociatedProcesses.TryGetValue(process.ProcessId, out var tracked) &&
            ReferenceEquals(tracked, process);

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
                this.persistentRuleAutoApplyService.MarkProcessExited(e.Process.ProcessId);
                this.coreMaskService.UnregisterMaskApplication(e.Process.ProcessId);
                this.processService.UntrackProcess(e.Process.ProcessId);

                if (this.runningAssociatedProcesses.TryRemove(e.Process.ProcessId, out _))
                {
                    await this.UpdatePowerPlanAsync();
                }
            }
            catch (Exception ex)
            {
                this.SetStatus(this.isRunning, "Error handling process stop", $"Error: {ex.Message}", ex);
            }
        }

        private void OnMonitoringStatusChanged(object? sender, MonitoringStatusEventArgs e)
        {
            if (e.StatusMessage != null)
            {
                TaskSafety.FireAndForget(
                    this.enhancedLogger.LogProcessMonitoringEventAsync(
                    "MonitoringHealth", "ProcessMonitorService", 0, e.StatusMessage), ex =>
                    this.logger.LogWarning(ex, "Could not persist monitoring health diagnostics"));
            }

            if (e.SnapshotRefreshed && this.isRunning)
            {
                TaskSafety.FireAndForget(this.EvaluateCurrentProcessesAsync(resetRuleAttempts: true), ex =>
                    this.logger.LogWarning(ex, "Could not apply rules after monitoring recovery"));
            }

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

            await this.UpdatePowerPlanAsync();
        }

        private async Task UpdatePowerPlanAsync(ProcessModel? startedProcess = null, bool forceDefault = false)
        {
            await this.powerPlanChangeSemaphore.WaitAsync();
            try
            {
                var configuration = this.configuration;
                if (configuration == null || (!forceDefault && !this.isRunning))
                {
                    return;
                }

                if (startedProcess != null)
                {
                    var association = configuration.FindMatchingAssociation(startedProcess);
                    if (association != null)
                    {
                        await this.TryChangePowerPlanForProcessAsync(startedProcess, association, configuration);
                    }

                    return;
                }

                if (!forceDefault)
                {
                    // Read the list after any in-flight plan change has completed. Stop events
                    // remove their processes before waiting for this lock.
                    var candidates = this.runningAssociatedProcesses.Values
                        .Select(process => (Process: process, Association: configuration.FindMatchingAssociation(process)))
                        .Where(candidate => candidate.Association != null)
                        .OrderByDescending(candidate => candidate.Association!.Priority);
                    foreach (var candidate in candidates)
                    {
                        if (await this.TryChangePowerPlanForProcessAsync(candidate.Process, candidate.Association!, configuration))
                        {
                            return;
                        }
                    }
                }

                if ((forceDefault || this.isRunning) && !string.IsNullOrEmpty(configuration.DefaultPowerPlanGuid))
                {
                    await this.RestoreDefaultPowerPlanAsync(configuration);
                }
            }
            catch (Exception ex)
            {
                this.SetStatus(this.isRunning, "Error determining power plan", $"Error: {ex.Message}", ex);
            }
            finally
            {
                this.powerPlanChangeSemaphore.Release();
            }
        }

        private async Task RestoreDefaultPowerPlanAsync(ProcessMonitorConfiguration configuration)
        {
            var guid = configuration.DefaultPowerPlanGuid;
            var currentPowerPlan = await this.powerPlanService.GetActivePowerPlan();
            var decision = this.powerPlanTransitionGate.ShouldApply(guid, currentPowerPlan?.Guid);
            if (!decision.ShouldApply)
            {
                this.logger.LogDebug("Default power plan restore suppressed for {PowerPlanGuid}: {Reason}", guid, decision.SuppressionReason);
                return;
            }

            this.powerPlanTransitionGate.RecordAttempt(guid);
            if (!await this.powerPlanService.SetActivePowerPlanByGuidAsync(guid, configuration.PreventDuplicatePowerPlanChanges))
            {
                this.logger.LogWarning("Failed to restore default power plan {PowerPlanGuid}", guid);
                return;
            }

            var newPowerPlan = await this.powerPlanService.GetPowerPlanByGuidAsync(guid);
            var systemProcess = new ProcessModel { Name = "System", ProcessId = -1 };
            var defaultAssociation = new ProcessPowerPlanAssociation("System", guid, configuration.DefaultPowerPlanName);
            this.ProcessPowerPlanChanged?.Invoke(this, new ProcessPowerPlanChangeEventArgs(
                systemProcess, defaultAssociation, currentPowerPlan, newPowerPlan, "DefaultRestored"));
            await this.notificationService.ShowPowerPlanChangeNotificationAsync(
                currentPowerPlan?.Name ?? "Unknown", newPowerPlan?.Name ?? configuration.DefaultPowerPlanName, string.Empty);
        }

        private async Task<bool> TryChangePowerPlanForProcessAsync(
            ProcessModel process, ProcessPowerPlanAssociation association, ProcessMonitorConfiguration configuration)
        {
            if (!this.isRunning || !this.IsTrackedAssociation(process))
            {
                return false;
            }

            var currentPowerPlan = await this.powerPlanService.GetActivePowerPlan();
            var stillRunning = await this.processService.IsProcessStillRunning(process);
            if (!stillRunning || !this.isRunning || !this.IsTrackedAssociation(process))
            {
                return false;
            }

            var decision = this.powerPlanTransitionGate.ShouldApply(association.PowerPlanGuid, currentPowerPlan?.Guid);
            if (!decision.ShouldApply)
            {
                this.logger.LogDebug(
                    "Power plan change suppressed for {PowerPlanGuid}: {Reason}", association.PowerPlanGuid, decision.SuppressionReason);
                return true;
            }

            this.powerPlanTransitionGate.RecordAttempt(association.PowerPlanGuid);
            var success = await this.powerPlanService.SetActivePowerPlanByGuidAsync(
                association.PowerPlanGuid, configuration.PreventDuplicatePowerPlanChanges);
            if (!success)
            {
                this.logger.LogWarning(
                    "Failed to change power plan to {PowerPlanGuid} for process {ProcessName} (PID: {ProcessId})",
                    association.PowerPlanGuid, process.Name, process.ProcessId);
                return true;
            }

            var newPowerPlan = await this.powerPlanService.GetPowerPlanByGuidAsync(association.PowerPlanGuid);
            this.ProcessPowerPlanChanged?.Invoke(this, new ProcessPowerPlanChangeEventArgs(
                process, association, currentPowerPlan, newPowerPlan, "ProcessStarted"));
            await this.notificationService.ShowPowerPlanChangeNotificationAsync(
                currentPowerPlan?.Name ?? "Unknown", newPowerPlan?.Name ?? association.PowerPlanName, process.Name);
            return true;
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

        private async Task ApplyPersistentRulesForDiscoveredProcessesAsync(IEnumerable<ProcessModel> processes)
        {
            try
            {
                var results = await this.persistentRuleAutoApplyService.ApplyForDiscoveredProcessesAsync(processes, this.automationLifetime.Token);
                await this.LogPersistentRuleResultsAsync(results);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Persistent rule auto-apply failed during process snapshot refresh");
            }
        }

        private async Task ApplyPersistentRulesForProcessStartAsync(ProcessModel process)
        {
            try
            {
                var results = await this.persistentRuleAutoApplyService.ApplyForProcessStartAsync(process, this.automationLifetime.Token);
                await this.LogPersistentRuleResultsAsync(results);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(
                    ex,
                    "Persistent rule auto-apply failed for process {ProcessName} (PID: {ProcessId})",
                    process.Name,
                    process.ProcessId);
            }
        }

        private async Task LogPersistentRuleResultsAsync(IReadOnlyList<PersistentRuleAutoApplyResult> results)
        {
            foreach (var result in results)
            {
                if (result.Success)
                {
                    await this.enhancedLogger.LogProcessMonitoringEventAsync(
                        LogEventTypes.ProcessMonitoring.AssociationTriggered,
                        result.ProcessName,
                        result.ProcessId,
                        $"Persistent rule '{result.RuleId}' applied automatically");
                }
                else
                {
                    this.logger.LogDebug(
                        "Persistent rule {RuleId} was not applied to process {ProcessName} (PID: {ProcessId}): {Message}",
                        result.RuleId,
                        result.ProcessName,
                        result.ProcessId,
                        result.UserMessage);
                }
            }
        }

        public void UpdateSettings()
        {
            // Update the process monitor service with new settings
            this.processMonitorService.UpdateSettings();

            this.logger.LogDebug("ProcessMonitorManagerService settings updated");
        }

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
                                var result = await this.affinityApplyService.ApplyAsync(process, affinity);
                                if (!result.Success)
                                {
                                    this.logger.LogWarning(
                                        "Failed to apply CPU mask '{MaskName}' to process {ProcessName} (PID: {ProcessId}): {Message}",
                                        coreMask.Name,
                                        process.Name,
                                        process.ProcessId,
                                        result.Message);
                                }
                                else
                                {
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

                            await this.processService.SetProcessPriority(process, priority, ProcessPriorityWriteSource.PowerPlanAssociation);

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
                return lowered.Contains("anti-cheat") || lowered.Contains("anti cheat") || lowered.Contains("protected")
                    ? ProcessOperationUserMessages.AntiCheatProtectedLikely
                    : ProcessOperationUserMessages.AccessDenied;
            }

            if (lowered.Contains("realtime priority is blocked"))
            {
                return ProcessOperationUserMessages.RealtimePriorityBlocked;
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
                this.automationLifetime.Dispose();

                this.disposed = true;
            }
        }
    }
}
