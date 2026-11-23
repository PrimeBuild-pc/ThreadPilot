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

        private readonly ConcurrentDictionary<int, AssociatedProcess> runningAssociatedProcesses = new();
        private readonly System.Threading.Timer delayTimer;
        private readonly SemaphoreSlim powerPlanChangeSemaphore = new(1, 1);
        private readonly SemaphoreSlim stateMutationSemaphore = new(1, 1);

        private CancellationTokenSource automationLifetime = new();
        private volatile bool isRunning;
        private string status = "Stopped";
        private bool disposed;
        private ProcessMonitorConfiguration? configuration;
        private int pendingPowerPlanReevaluation;

        public event EventHandler<ProcessPowerPlanChangeEventArgs>? ProcessPowerPlanChanged;

        public event EventHandler<ServiceStatusEventArgs>? ServiceStatusChanged;

        public bool IsRunning => this.isRunning;

        public string Status => this.status;

        public IEnumerable<ProcessModel> RunningAssociatedProcesses => this.runningAssociatedProcesses.Values.Select(tracked => tracked.Process);

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
                lock (this.lockObject)
                {
                    foreach (var processId in this.runningAssociatedProcesses.Keys)
                    {
                        this.ClearProcessTracking(processId);
                    }

                    this.runningAssociatedProcesses.Clear();
                }

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

            if (enabled != this.isRunning)
            {
                return;
            }

            await this.settingsService.EditSettingsAsync(draft =>
            {
                if (enabled == this.isRunning)
                {
                    draft.EnableAutomationMonitoring = enabled;
                }
            });
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
                var lifetime = this.automationLifetime.Token;
                var trackedBeforeScan = this.runningAssociatedProcesses.ToDictionary();
                var currentProcesses = (await this.processMonitorService.GetRunningProcessesAsync()).ToList();
                if (lifetime.IsCancellationRequested)
                {
                    return;
                }

                var currentById = currentProcesses.DistinctBy(process => process.ProcessId)
                    .ToDictionary(process => process.ProcessId);
                lock (this.lockObject)
                {
                    // Keep processes reported by events while the snapshot was being read.
                    foreach (var tracked in trackedBeforeScan)
                    {
                        if ((!currentById.TryGetValue(tracked.Key, out var current) || !tracked.Value.Process.IsSameInstance(current))
                            && this.runningAssociatedProcesses.TryRemove(tracked))
                        {
                            this.ClearProcessTracking(tracked.Key);
                            if (current != null)
                            {
                                trackedBeforeScan.Remove(tracked.Key);
                            }
                        }
                    }
                }

                if (resetRuleAttempts)
                {
                    // A fresh monitoring lifetime or resume must also reapply rules after a short sleep,
                    // even if the last application is still inside the normal duplicate-event cooldown.
                    foreach (var process in currentProcesses)
                    {
                        this.persistentRuleAutoApplyService.MarkProcessExited(process.ProcessId);
                    }
                }

                await this.ApplyPersistentRulesForDiscoveredProcessesAsync(currentById.Values, resetRuleAttempts, lifetime);
                if (lifetime.IsCancellationRequested)
                {
                    return;
                }

                foreach (var process in currentById.Values)
                {
                    await this.ReconcileAssociationAsync(process, applyProcessSettings: resetRuleAttempts, lifetime, trackedBeforeScan);
                }

                await this.ReevaluatePowerPlanAsync(lifetime);
            }
            catch (Exception ex)
            {
                this.SetStatus(this.isRunning, "Error evaluating processes", $"Error: {ex.Message}", ex);
            }
        }

        public Task ForceDefaultPowerPlanAsync() => this.ReevaluatePowerPlanAsync(CancellationToken.None, forceDefault: true);

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
                this.logger.LogDebug(
                    "Process started: {ProcessName} (PID: {ProcessId})",
                    e.Process.Name,
                    e.Process.ProcessId);

                var lifetime = this.automationLifetime.Token;
                lock (this.lockObject)
                {
                    if (this.runningAssociatedProcesses.TryGetValue(e.Process.ProcessId, out var previous)
                        && !previous.Process.IsSameInstance(e.Process))
                    {
                        this.runningAssociatedProcesses.TryRemove(e.Process.ProcessId, out _);
                        this.ClearProcessTracking(e.Process.ProcessId);
                    }
                }

                if (string.IsNullOrWhiteSpace(e.Process.ExecutablePath)
                    && this.configuration.GetEnabledAssociations().Any(candidate =>
                        candidate.MatchByPath && candidate.MatchesExecutable(e.Process.Name)))
                {
                    await this.TryRefreshAssociationProcessAsync(e.Process);
                }

                await this.ApplyPersistentRulesForProcessStartAsync(e.Process, lifetime);

                var association = await this.ReconcileAssociationAsync(e.Process, applyProcessSettings: true, lifetime);
                if (association != null && !lifetime.IsCancellationRequested)
                {
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
                        await this.ReevaluatePowerPlanAsync(lifetime);
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

        private async Task<ProcessPowerPlanAssociation?> ReconcileAssociationAsync(
            ProcessModel process,
            bool applyProcessSettings,
            CancellationToken lifetime,
            IReadOnlyDictionary<int, AssociatedProcess>? trackedBeforeScan = null)
        {
            if (this.configuration == null || lifetime.IsCancellationRequested)
            {
                return null;
            }

            var candidates = this.configuration.GetEnabledAssociations()
                .Where(association => association.MatchesExecutable(process.Name))
                .ToList();
            if (candidates.Count == 0)
            {
                return null;
            }

            var needsPath = string.IsNullOrWhiteSpace(process.ExecutablePath) && candidates.Any(candidate => candidate.MatchByPath);
            var needsPriority = applyProcessSettings && candidates.Any(candidate => !string.IsNullOrEmpty(candidate.ProcessPriority));
            if (needsPath || needsPriority)
            {
                await this.TryRefreshAssociationProcessAsync(process);
            }

            var association = candidates.FirstOrDefault(candidate => candidate.MatchesProcess(process));
            if (association == null || lifetime.IsCancellationRequested || !await this.processService.IsProcessStillRunning(process))
            {
                return null;
            }

            AssociatedProcess tracked;
            bool applyEffects;
            lock (this.lockObject)
            {
                this.runningAssociatedProcesses.TryGetValue(process.ProcessId, out var current);
                if (lifetime.IsCancellationRequested)
                {
                    return null;
                }

                if (trackedBeforeScan != null)
                {
                    trackedBeforeScan.TryGetValue(process.ProcessId, out var previous);
                    if (!ReferenceEquals(previous, current) && (current == null || !current.Process.IsSameInstance(process)))
                    {
                        // A later event stopped or replaced this process while we were scanning.
                        return null;
                    }
                }

                tracked = current ?? new AssociatedProcess(process);
                if (current == null)
                {
                    this.runningAssociatedProcesses[process.ProcessId] = tracked;
                }
                else
                {
                    process.StartTimeUtc ??= tracked.Process.StartTimeUtc;
                    tracked.Process = process;
                }

                applyEffects = applyProcessSettings && !tracked.EffectsAttempted;
                tracked.EffectsAttempted |= applyEffects;
            }

            if (applyEffects)
            {
                var effects = await this.ApplyCoreMaskAndPriorityAsync(process, association);
                lock (this.lockObject)
                {
                    tracked.PreservedSettings = effects;
                }

                await this.enhancedLogger.LogProcessMonitoringEventAsync(
                    LogEventTypes.ProcessMonitoring.AssociationTriggered,
                    process.Name, process.ProcessId,
                    $"Process matched association for power plan: {association.PowerPlanName}");
            }

            return association;
        }

        private async Task TryRefreshAssociationProcessAsync(ProcessModel process)
        {
            try
            {
                await this.processService.RefreshProcessInfo(process);
            }
            catch (Exception ex)
            {
                this.logger.LogDebug(ex, "Could not enrich process {ProcessName} (PID: {ProcessId}) for association matching", process.Name, process.ProcessId);
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
                bool associated;
                lock (this.lockObject)
                {
                    var pid = e.Process.ProcessId;
                    associated = this.runningAssociatedProcesses.TryGetValue(pid, out var tracked);
                    if (tracked != null && !tracked.Process.IsSameInstance(e.Process))
                    {
                        return;
                    }

                    this.runningAssociatedProcesses.TryRemove(pid, out _);
                    this.ClearProcessTracking(pid);
                }

                if (associated)
                {
                    await this.ReevaluatePowerPlanAsync(this.automationLifetime.Token);
                }
            }
            catch (Exception ex)
            {
                this.SetStatus(this.isRunning, "Error handling process stop", $"Error: {ex.Message}", ex);
            }
        }

        private void ClearProcessTracking(int processId)
        {
            this.persistentRuleAutoApplyService.MarkProcessExited(processId);
            this.coreMaskService.UnregisterMaskApplication(processId);
            this.processService.UntrackProcess(processId);
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

            await this.ReevaluatePowerPlanAsync(this.automationLifetime.Token);
        }

        private async Task ReevaluatePowerPlanAsync(CancellationToken lifetime, bool forceDefault = false)
        {
            await this.powerPlanChangeSemaphore.WaitAsync();
            try
            {
                var configuration = this.configuration;
                if (lifetime.IsCancellationRequested || configuration == null)
                {
                    return;
                }

                var currentPowerPlan = await this.powerPlanService.GetActivePowerPlan();
                (ProcessModel? Process, ProcessPowerPlanAssociation? Association) selected = default;
                if (!forceDefault)
                {
                    var candidates = this.RunningAssociatedProcesses
                        .Select(process => (Process: process, Association: configuration.FindMatchingAssociation(process)))
                        .Where(match => match.Association != null)
                        .OrderByDescending(match => match.Association!.Priority);
                    foreach (var candidate in candidates)
                    {
                        if (await this.processService.IsProcessStillRunning(candidate.Process)
                            && this.runningAssociatedProcesses.TryGetValue(candidate.Process.ProcessId, out var tracked)
                            && tracked.Process.IsSameInstance(candidate.Process))
                        {
                            selected = candidate;
                            break;
                        }
                    }
                }

                var process = selected.Process;
                var association = selected.Association;
                var planGuid = association?.PowerPlanGuid ?? configuration.DefaultPowerPlanGuid;
                if (string.IsNullOrEmpty(planGuid))
                {
                    return;
                }

                var decision = this.powerPlanTransitionGate.ShouldApply(planGuid, currentPowerPlan?.Guid);
                if (!decision.ShouldApply || lifetime.IsCancellationRequested)
                {
                    this.logger.LogDebug(
                        "Power plan change suppressed for {PowerPlanGuid}: {Reason}", planGuid, decision.SuppressionReason);
                    return;
                }

                this.powerPlanTransitionGate.RecordAttempt(planGuid);
                if (!await this.powerPlanService.SetActivePowerPlanByGuidAsync(planGuid, configuration.PreventDuplicatePowerPlanChanges))
                {
                    this.logger.LogWarning("Failed to change power plan to {PowerPlanGuid}", planGuid);
                    return;
                }

                var newPowerPlan = await this.powerPlanService.GetPowerPlanByGuidAsync(planGuid);
                this.ProcessPowerPlanChanged?.Invoke(this, new ProcessPowerPlanChangeEventArgs(
                    process ?? new ProcessModel { Name = "System", ProcessId = -1 },
                    association ?? new ProcessPowerPlanAssociation("System", planGuid, configuration.DefaultPowerPlanName),
                    currentPowerPlan,
                    newPowerPlan,
                    association == null ? "DefaultRestored" : "ProcessStarted"));

                await this.notificationService.ShowPowerPlanChangeNotificationAsync(
                    currentPowerPlan?.Name ?? "Unknown",
                    newPowerPlan?.Name ?? association?.PowerPlanName ?? configuration.DefaultPowerPlanName,
                    process?.Name ?? string.Empty);
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

        private async Task ApplyPersistentRulesForDiscoveredProcessesAsync(
            IEnumerable<ProcessModel> processes, bool applyAssociationSettings, CancellationToken lifetime)
        {
            try
            {
                var results = await this.persistentRuleAutoApplyService.ApplyForDiscoveredProcessesAsync(
                    processes, process => this.GetPersistentRuleOptions(process, applyAssociationSettings), lifetime);
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

        private async Task ApplyPersistentRulesForProcessStartAsync(ProcessModel process, CancellationToken lifetime)
        {
            try
            {
                var results = await this.persistentRuleAutoApplyService.ApplyForProcessStartAsync(
                    process, target => this.GetPersistentRuleOptions(target, applyAssociationSettings: true), lifetime);
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

        private PersistentRuleApplyOptions GetPersistentRuleOptions(ProcessModel process, bool applyAssociationSettings)
        {
            var options = PersistentRuleApplyOptions.None;
            lock (this.lockObject)
            {
                if (this.runningAssociatedProcesses.TryGetValue(process.ProcessId, out var tracked)
                    && tracked.Process.IsSameInstance(process))
                {
                    options = tracked.PreservedSettings;
                }
            }

            if (applyAssociationSettings
                && this.configuration?.GetEnabledAssociations().Any(association =>
                    Enum.TryParse<ProcessPriorityClass>(association.ProcessPriority, out var priority)
                    && Enum.IsDefined(priority) && !ProcessPriorityGuardrails.IsBlocked(priority)
                    && association.MatchesProcess(process)) == true)
            {
                // A delayed retry from the saved rule would undo the association's priority.
                options |= PersistentRuleApplyOptions.SkipDelayedPriorityVerification;
            }

            return options;
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

        private async Task<PersistentRuleApplyOptions> ApplyCoreMaskAndPriorityAsync(
            ProcessModel process, ProcessPowerPlanAssociation association)
        {
            var applied = PersistentRuleApplyOptions.None;
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
                                    applied |= PersistentRuleApplyOptions.PreserveAffinity;
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
                            applied |= PersistentRuleApplyOptions.PreservePriority;

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

            return applied;
        }

        private sealed class AssociatedProcess(ProcessModel process)
        {
            public ProcessModel Process { get; set; } = process;
            public bool EffectsAttempted { get; set; }
            public PersistentRuleApplyOptions PreservedSettings { get; set; }
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
