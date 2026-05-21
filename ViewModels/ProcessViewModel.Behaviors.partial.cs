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
namespace ThreadPilot.ViewModels
{
    using System;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Windows.Input;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using Microsoft.Extensions.Logging;
    using ThreadPilot.Models;
    using ThreadPilot.Services;

    public partial class ProcessViewModel : BaseViewModel
    {
        private async Task ApplyFiltersOnUiAsync()
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(this.FilterProcesses);
        }

        private void SetupVirtualizedProcessService()
        {
            // Configure virtualization settings
            this.virtualizedProcessService.Configuration.BatchSize = 50;
            this.virtualizedProcessService.Configuration.EnableBackgroundLoading = true;

            // Subscribe to events
            this.virtualizedProcessService.BatchLoadProgress += this.OnBatchLoadProgress;
            this.virtualizedProcessService.BackgroundBatchLoaded += this.OnBackgroundBatchLoaded;
        }

        private void OnBatchLoadProgress(object? sender, BatchLoadProgressEventArgs e)
        {
            if (this.isUiRefreshPaused || !this.isProcessViewActive)
            {
                return;
            }

            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                this.LoadingProgress = e.ProgressPercentage;
                this.LoadingStatusText = e.StatusMessage;
            });
        }

        private void OnBackgroundBatchLoaded(object? sender, ProcessBatchResult e)
        {
            this.Logger.LogDebug(
                "Background batch {BatchIndex} loaded with {ProcessCount} processes",
                e.BatchIndex, e.Processes.Count);
        }

        public override async Task InitializeAsync()
        {
            try
            {
                // Update status on UI thread
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.SetStatus("Initializing CPU topology and power plans...");
                });
                System.Diagnostics.Debug.WriteLine("ProcessViewModel.InitializeAsync: Starting initialization");

                // Initialize CPU topology
                System.Diagnostics.Debug.WriteLine("ProcessViewModel.InitializeAsync: About to detect CPU topology");
                await this.cpuTopologyService.DetectTopologyAsync();
                System.Diagnostics.Debug.WriteLine("ProcessViewModel.InitializeAsync: CPU topology detection completed");

                // Initialize core masks service
                System.Diagnostics.Debug.WriteLine("ProcessViewModel.InitializeAsync: About to initialize core masks");
                await this.coreMaskService.InitializeAsync();
                this.AvailableCoreMasks = this.coreMaskService.AvailableMasks;
                this.SelectedCoreMask = this.coreMaskService.DefaultMask;
                System.Diagnostics.Debug.WriteLine("ProcessViewModel.InitializeAsync: Core masks initialized");

                // Load power plans
                System.Diagnostics.Debug.WriteLine("ProcessViewModel.InitializeAsync: About to load power plans");
                await this.RefreshPowerPlansAsync();
                System.Diagnostics.Debug.WriteLine("ProcessViewModel.InitializeAsync: Power plans loaded");

                // Load processes automatically on startup (Bug #8 fix)
                System.Diagnostics.Debug.WriteLine("ProcessViewModel.InitializeAsync: About to load processes automatically");
                await this.LoadProcessesCommand.ExecuteAsync(null);

                // Access process count on UI thread to avoid threading issues
                int processCount = 0;
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    processCount = this.Processes?.Count ?? 0;
                });
                System.Diagnostics.Debug.WriteLine($"ProcessViewModel.InitializeAsync: Processes loaded automatically, count: {processCount}");

                // Start refresh timer for real-time updates
                System.Diagnostics.Debug.WriteLine("ProcessViewModel.InitializeAsync: Starting refresh timer");
                this.refreshTimer?.Start();
                System.Diagnostics.Debug.WriteLine("ProcessViewModel.InitializeAsync: Initialization completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ProcessViewModel.InitializeAsync: Exception occurred: {ex.Message}");
                // Update status on UI thread
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.SetStatus($"Failed to initialize: {ex.Message}", false);
                });
            }
        }

        private async Task RefreshPowerPlansAsync()
        {
            try
            {
                var plans = await this.powerPlanService.GetPowerPlansAsync();
                var activePlan = await this.powerPlanService.GetActivePowerPlan();

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.PowerPlans.Clear();
                    foreach (var plan in plans)
                    {
                        plan.IsActive = plan.Guid == activePlan?.Guid;
                        this.PowerPlans.Add(plan);
                    }

                    this.SelectedPowerPlan = this.PowerPlans.FirstOrDefault(p => p.Guid == activePlan?.Guid);
                });
            }
            catch (Exception ex)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.SetStatus($"Failed to load power plans: {ex.Message}", false);
                });
            }
        }

        partial void OnSelectedProcessChanged(ProcessModel? value)
        {
            this.UpdateSelectedProcessSummary(value);

            if (value != null && CpuTopology != null)
            {
                this.HasPendingAffinityEdits = false;
                this.UpdateAffinityDisplayState();
                // Immediately fetch and display real-time process information
                TaskSafety.FireAndForget(HandleSelectedProcessChangedAsync(value), ex =>
                {
                    this.Logger.LogWarning(ex, "Failed while handling selected process change for {ProcessName}", value.Name);
                });
            }
            else if (value == null)
            {
                // Clear selection
                this.ClearProcessSelection();
            }

            // Update system tray context menu
            this.systemTrayService.UpdateContextMenu(value?.Name, value != null);
        }

        private void UpdateSelectedProcessSummary(ProcessModel? process)
        {
            TaskSafety.FireAndForget(
                this.UpdateSelectedProcessSummaryAsync(process),
                ex => this.Logger.LogWarning(ex, "Failed to update selected process summary"));
        }

        private Task UpdateSelectedProcessSummaryAsync(ProcessModel? process)
        {
            return this.SelectedProcessSummary.UpdateAsync(process, this.StatusMessage, this.HasError);
        }

        private async Task HandleSelectedProcessChangedAsync(ProcessModel value)
        {
            try
            {
                // First check if the process is still running
                bool isStillRunning = await this.processService.IsProcessStillRunning(value);
                if (!isStillRunning)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        this.SetStatus($"Process {value.Name} (PID: {value.ProcessId}) has terminated", false);
                        this.SelectedProcess = null;
                        this.ClearProcessSelection();
                    });
                    return;
                }

                // Refresh process info to get current state from OS
                await this.processService.RefreshProcessInfo(value);

                // Update UI on main thread with fresh data
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    this.UpdateCoreSelections(value.ProcessorAffinity);
                    this.UpdateAffinityDisplayState();
                    value.ForceNotifyProcessorAffinityChanged();

                    // Update priority display - trigger property change to refresh ComboBox
                    this.OnPropertyChanged(nameof(this.SelectedProcess));

                    // Update feature states from the selected process
                    this.IsIdleServerDisabled = value.IsIdleServerDisabled;
                    this.IsRegistryPriorityEnabled = value.IsRegistryPriorityEnabled;

                    // BUG FIX: Update status without setting busy state for process selection
                    this.SetStatus(
                        $"Selected process: {value.Name} (PID: {value.ProcessId}) - " +
                            $"Priority: {value.Priority}, Affinity: 0x{value.ProcessorAffinity:X}", false);
                });
                if (ReferenceEquals(this.SelectedProcess, value))
                {
                    // Keep this second update for refreshed process fields and the latest operation message.
                    this.UpdateSelectedProcessSummary(value);
                }

                // Load current power plan association if available
                await this.LoadProcessPowerPlanAssociation(value);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("terminated") || ex.Message.Contains("exited") || ex.Message.Contains("no longer exists"))
            {
                // Process has terminated
                this.Logger.LogInformation("Process {ProcessName} (PID: {ProcessId}) has terminated", value.Name, value.ProcessId);
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    this.SetStatus($"Process {value.Name} (PID: {value.ProcessId}) has terminated", false);
                    this.SelectedProcess = null;
                    this.ClearProcessSelection();
                });
            }
            catch (Exception ex)
            {
                this.Logger.LogWarning(ex, "Failed to refresh process info for {ProcessName}", value.Name);
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    this.SetStatus($"Warning: Could not access process {value.Name} - it may have terminated or require elevated privileges", false);
                });
                if (ReferenceEquals(this.SelectedProcess, value))
                {
                    this.UpdateSelectedProcessSummary(value);
                }
            }
        }

        private void OnTrayQuickApplyRequested(object? sender, EventArgs e)
        {
            TaskSafety.FireAndForget(this.OnTrayQuickApplyRequestedAsync(), ex =>
            {
                this.Logger.LogWarning(ex, "Quick apply request failed");
            });
        }

        private async Task OnTrayQuickApplyRequestedAsync()
        {
            try
            {
                // Marshal UI operations to the UI thread to prevent cross-thread access exceptions
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    await this.QuickApplyAffinityAndPowerPlanCommand.ExecuteAsync(null);
                    this.systemTrayService.ShowBalloonTip(
                        "ThreadPilot",
                        $"Pending settings applied to {this.SelectedProcess?.Name ?? "selected process"}", 2000);
                });
            }
            catch (Exception ex)
            {
                // Marshal UI operations to the UI thread to prevent cross-thread access exceptions
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.systemTrayService.ShowBalloonTip(
                        "ThreadPilot Error",
                        $"Failed to apply pending settings: {ex.Message}", 3000);
                });
            }
        }

        private void OnTopologyDetected(object? sender, CpuTopologyDetectedEventArgs e)
        {
            // Ensure all UI updates happen on the dispatcher thread
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                this.CpuTopology = e.Topology;
                this.IsTopologyDetectionSuccessful = e.DetectionSuccessful;

                if (e.DetectionSuccessful)
                {
                    this.TopologyStatus = $"Detected: {e.Topology.TotalLogicalCores} logical CPUs, " +
                                   $"{e.Topology.TotalPhysicalCores} physical CPUs";
                    this.AreAdvancedFeaturesAvailable = e.Topology.HasIntelHybrid || e.Topology.HasAmdCcd || e.Topology.HasHyperThreading;
                }
                else
                {
                    this.TopologyStatus = $"Detection failed: {e.ErrorMessage ?? "Unknown error"}";
                    this.AreAdvancedFeaturesAvailable = false;
                }

                this.UpdateCpuCores();
                this.UpdateAffinityPresets();
                this.UpdateHyperThreadingStatus();
            });
        }

        private void UpdateCpuCores()
        {
            if (this.CpuTopology == null)
            {
                return;
            }

            this.CpuCores.Clear();
            foreach (var core in this.CpuTopology.LogicalCores)
            {
                core.PropertyChanged -= this.OnCorePropertyChanged;
                core.PropertyChanged += this.OnCorePropertyChanged;
                this.CpuCores.Add(core);
            }
        }

        private void OnCorePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Note: Advanced CPU Affinity cores are now read-only (ProcessView.xaml has IsHitTestVisible="False")
            // This event handler is kept for compatibility but should not be triggered
            // Core modifications are done exclusively through the Core Mask tab
            if (this.suppressCoreSelectionEvents)
            {
                return;
            }

            this.Logger.LogDebug("Core property changed but cores are read-only - no action taken");
        }

        private void UpdateHyperThreadingStatus()
        {
            if (this.CpuTopology == null)
            {
                this.HyperThreadingStatusText = "Multi-Threading: Unknown";
                this.IsHyperThreadingActive = false;
                return;
            }

            // Determine if hyperthreading/SMT is present and active
            bool hasMultiThreading = this.CpuTopology.HasHyperThreading;
            this.IsHyperThreadingActive = hasMultiThreading;

            // Determine the appropriate technology name based on CPU vendor
            string technologyName = "Multi-Threading";
            if (this.CpuTopology.CpuBrand.Contains("Intel", StringComparison.OrdinalIgnoreCase))
            {
                technologyName = "Hyper-Threading";
            }
            else if (this.CpuTopology.CpuBrand.Contains("AMD", StringComparison.OrdinalIgnoreCase))
            {
                technologyName = "SMT";
            }

            // Set the status text
            string status = hasMultiThreading ? "Active" : "Not Available";
            this.HyperThreadingStatusText = $"{technologyName}: {status}";

            this.Logger.LogInformation(
                "Updated hyperthreading status: {StatusText} (Active: {IsActive})",
                this.HyperThreadingStatusText, this.IsHyperThreadingActive);
        }

        private void UpdateAffinityPresets()
        {
            this.AffinityPresets.Clear();
            var presets = this.cpuTopologyService.GetAffinityPresets();
            foreach (var preset in presets)
            {
                this.AffinityPresets.Add(preset);
            }
        }

        private void UpdateCoreSelections(long affinityMask, bool forceSync = false)
        {
            if (this.CpuTopology == null || this.CpuCores.Count == 0)
            {
                this.Logger.LogWarning(
                    "Cannot update core selections: CpuTopology={CpuTopology}, CpuCores.Count={CpuCoresCount}",
                    this.CpuTopology != null, this.CpuCores.Count);
                return;
            }

            if (this.HasPendingAffinityEdits && !forceSync)
            {
                this.Logger.LogDebug("Skipping affinity sync because user edits are pending");
                return;
            }

            this.Logger.LogDebug(
                "Updating core selections for affinity mask 0x{AffinityMask:X} ({AffinityMaskBinary})",
                affinityMask, Convert.ToString(affinityMask, 2).PadLeft(Environment.ProcessorCount, '0'));

            // Update each core's selection state based on the actual OS affinity mask
            var updatedCores = new List<(int CoreId, bool WasSelected, bool IsSelected)>();

            try
            {
                this.suppressCoreSelectionEvents = true;

                foreach (var core in this.CpuCores)
                {
                    bool wasSelected = core.IsSelected;
                    bool shouldBeSelected = (affinityMask & core.AffinityMask) != 0;

                    if (wasSelected != shouldBeSelected)
                    {
                        core.IsSelected = shouldBeSelected;
                        updatedCores.Add((core.LogicalCoreId, wasSelected, shouldBeSelected));
                    }
                }
            }
            finally
            {
                this.suppressCoreSelectionEvents = false;
            }

            // The UI will automatically update since CpuCoreModel now implements INotifyPropertyChanged
            // No need to force collection refresh as individual property changes will be notified

            // Log the affinity update for debugging
            var selectedCoreIds = this.CpuCores.Where(c => c.IsSelected).Select(c => c.LogicalCoreId).OrderBy(id => id).ToList();
            var totalCores = this.CpuCores.Count;
            var selectedCount = selectedCoreIds.Count;

            this.Logger.LogInformation(
                "Updated core selections for affinity mask 0x{AffinityMask:X}: " +
                                "Selected {SelectedCount}/{TotalCores} cores: [{CoreIds}]",
                affinityMask, selectedCount, totalCores, string.Join(", ", selectedCoreIds));

            if (updatedCores.Count > 0)
            {
                this.Logger.LogDebug(
                    "Core selection changes: {Changes}",
                    string.Join("; ", updatedCores.Select(c => $"Core {c.CoreId}: {c.WasSelected} -> {c.IsSelected}")));
            }
            else
            {
                this.Logger.LogDebug("No core selection changes needed - UI already matches affinity mask");
            }

            if (forceSync)
            {
                this.HasPendingAffinityEdits = false;
            }

            this.UpdateAffinityDisplayState();
        }

        private long CalculateAffinityMask()
        {
            if (this.CpuTopology == null)
            {
                return 0;
            }

            var selectedCores = this.CpuCores.Where(core => core.IsSelected);

            // Note: Removed hyperthreading filtering - user can manually select desired cores
            // All selected cores (including HT siblings) are now included in the affinity mask

            return selectedCores.Aggregate(0L, (mask, core) => mask | core.AffinityMask);
        }

        private List<bool> GetPendingCoreSelectionMask()
        {
            return this.CpuCores
                .OrderBy(core => core.LogicalCoreId)
                .Select(core => core.IsSelected)
                .ToList();
        }

        private void UpdateAffinityDisplayState()
        {
            var currentMask = this.SelectedProcess?.ProcessorAffinity;
            this.CurrentAffinityText = currentMask.HasValue
                ? $"Current OS affinity: 0x{currentMask.Value:X}"
                : "Current OS affinity: no process selected";

            if (this.SelectedProcess == null)
            {
                this.PendingAffinityText = "Pending core mask: none";
                this.AffinityEditStateText = "Select a process to view its current Windows affinity.";
                return;
            }

            if (!this.HasPendingAffinityEdits)
            {
                this.PendingAffinityText = "Pending core mask: none";
                this.AffinityEditStateText = "Current OS affinity is displayed. Select a core mask to stage a change.";
                return;
            }

            var pendingMask = this.CalculateAffinityMask();
            this.PendingAffinityText = pendingMask > 0
                ? $"Pending core mask: 0x{pendingMask:X}"
                : "Pending core mask: no cores selected";
            this.AffinityEditStateText = "Core mask staged. Use Apply Affinity to change Windows affinity.";
        }

        [RelayCommand]
        public async Task LoadMoreProcesses()
        {
            if (!this.IsVirtualizationEnabled || !this.HasMoreBatches || this.IsBusy)
            {
                return;
            }

            try
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.SetStatus($"Loading more processes (batch {this.CurrentBatchIndex + 2})...");
                });

                var nextBatchIndex = this.CurrentBatchIndex + 1;
                var batch = await this.virtualizedProcessService.LoadProcessBatchAsync(nextBatchIndex, this.ShowActiveApplicationsOnly);

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    // Add new processes to existing collection
                    foreach (var process in batch.Processes)
                    {
                        this.Processes.Add(process);
                    }

                    this.CurrentBatchIndex = batch.BatchIndex;
                    this.TotalBatches = batch.TotalBatches;
                    this.HasMoreBatches = batch.HasMoreBatches;
                    this.TotalProcessCount = batch.TotalProcessCount;

                    this.FilterProcesses();

                    // BUG FIX: Ensure loading state is properly cleared
                    this.ClearStatus();
                    this.LoadingProgress = 0.0;
                    this.LoadingStatusText = string.Empty;
                });

                // Preload next batch in background
                await this.virtualizedProcessService.PreloadNextBatchAsync(this.CurrentBatchIndex, this.ShowActiveApplicationsOnly);
            }
            catch (Exception ex)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    // BUG FIX: Ensure loading state is cleared even on error
                    this.LoadingProgress = 0.0;
                    this.LoadingStatusText = string.Empty;
                    this.SetStatus($"Error loading more processes: {ex.Message}", false);
                });
            }
        }

        [RelayCommand]
        public async Task LoadProcesses()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"LoadProcesses: Starting, ShowActiveApplicationsOnly={this.ShowActiveApplicationsOnly}");

                // PERFORMANCE IMPROVEMENT: Progressive loading with status updates
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.LoadingProgress = 0.0;
                    this.LoadingStatusText = this.ShowActiveApplicationsOnly ? "Loading active applications..." : "Loading processes...";
                    this.SetStatus(this.LoadingStatusText);
                });

                ObservableCollection<ProcessModel> newProcesses;

                // VIRTUALIZATION ENHANCEMENT: Use virtualized loading for large process lists
                if (this.IsVirtualizationEnabled)
                {
                    System.Diagnostics.Debug.WriteLine("LoadProcesses: Using virtualized loading");
                    await this.virtualizedProcessService.InitializeAsync();

                    var totalCount = await this.virtualizedProcessService.GetTotalProcessCountAsync(this.ShowActiveApplicationsOnly);
                    if (totalCount > this.virtualizedProcessService.Configuration.BatchSize)
                    {
                        // Load first batch only
                        var batch = await this.virtualizedProcessService.LoadProcessBatchAsync(0, this.ShowActiveApplicationsOnly);
                        newProcesses = new ObservableCollection<ProcessModel>(batch.Processes);

                        // Update virtualization state
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            this.CurrentBatchIndex = batch.BatchIndex;
                            this.TotalBatches = batch.TotalBatches;
                            this.HasMoreBatches = batch.HasMoreBatches;
                            this.TotalProcessCount = batch.TotalProcessCount;
                        });

                        // Preload next batch in background
                        await this.virtualizedProcessService.PreloadNextBatchAsync(0, this.ShowActiveApplicationsOnly);
                    }
                    else
                    {
                        // Small list, load all processes normally
                        newProcesses = this.ShowActiveApplicationsOnly
                            ? await this.processService.GetActiveApplicationsAsync()
                            : await this.processService.GetProcessesAsync();
                    }
                }
                else
                {
                    // Traditional loading
                    if (this.ShowActiveApplicationsOnly)
                    {
                        System.Diagnostics.Debug.WriteLine("LoadProcesses: Getting active applications");
                        newProcesses = await this.processService.GetActiveApplicationsAsync();
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("LoadProcesses: Getting all processes");
                        newProcesses = await this.processService.GetProcessesAsync();
                    }
                }

                // Update UI on the UI thread
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.Processes = newProcesses;
                    System.Diagnostics.Debug.WriteLine($"LoadProcesses: Retrieved {this.Processes?.Count ?? 0} processes");
                    this.FilterProcesses();
                    System.Diagnostics.Debug.WriteLine($"LoadProcesses: After filtering, {this.FilteredProcesses?.Count ?? 0} processes visible");

                    // BUG FIX: Ensure loading state is properly cleared
                    this.ClearStatus();
                    this.LoadingProgress = 0.0;
                    this.LoadingStatusText = string.Empty;
                });

                System.Diagnostics.Debug.WriteLine("LoadProcesses: Completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadProcesses: Exception occurred: {ex.Message}");
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    // BUG FIX: Ensure loading state is cleared even on error
                    this.LoadingProgress = 0.0;
                    this.LoadingStatusText = string.Empty;
                    this.SetStatus($"Error loading processes: {ex.Message}", false);
                });
            }
        }

        [RelayCommand]
        private async Task RefreshProcesses()
        {
            if (this.IsBusy)
            {
                return;
            }

            try
            {
                var selectedProcessId = this.SelectedProcess?.ProcessId;

                var currentProcesses = this.ShowActiveApplicationsOnly
                    ? await this.processService.GetActiveApplicationsAsync()
                    : await this.processService.GetProcessesAsync();

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var deltaResult = ProcessListDeltaUpdater.ApplyDelta(
                        this.Processes,
                        currentProcesses,
                        selectedProcessId);

                    this.FilterProcesses();

                    if (deltaResult.SelectedProcess != null)
                    {
                        var processToSelect = this.FilteredProcesses.FirstOrDefault(
                            p => p.ProcessId == deltaResult.SelectedProcess.ProcessId);
                        if (processToSelect != null)
                        {
                            this.SelectedProcess = processToSelect;
                        }
                    }
                    else if (deltaResult.SelectedProcessTerminated)
                    {
                        this.SelectedProcess = null;
                        this.ClearProcessSelection();
                    }
                });
            }
            catch (Exception ex)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.SetStatus($"Error refreshing processes: {ex.Message}", false);
                });
            }
        }

        [RelayCommand]
        private async Task SetAffinity()
        {
            var selectedProcess = this.SelectedProcess;
            if (selectedProcess == null)
            {
                return;
            }

            try
            {
                var pendingSelection = this.GetPendingCoreSelectionMask();

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.SetStatus($"Setting affinity for {selectedProcess.Name}...");
                });

                var result = await this.processAffinityApplyCoordinator.ApplyCoreSelectionAsync(
                    selectedProcess,
                    pendingSelection,
                    "Manual Process tab CPU selection");

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (!result.UsedCpuSets)
                    {
                        this.UpdateCoreSelections(selectedProcess.ProcessorAffinity, true);
                    }

                    selectedProcess.ForceNotifyProcessorAffinityChanged();
                    this.OnPropertyChanged(nameof(this.SelectedProcess));

                    if (result.Success)
                    {
                        this.HasPendingAffinityEdits = false;
                        this.UpdateAffinityDisplayState();
                        this.SetStatus($"Affinity applied successfully to {selectedProcess.Name} (0x{result.VerifiedMask:X}).", false);
                        _ = this.notificationService.ShowNotificationAsync("Affinity applied", $"{selectedProcess.Name}: 0x{result.VerifiedMask:X}", NotificationType.Success);
                    }
                    else if (result.FailureReason == AffinityApplyFailureReason.VerificationMismatch)
                    {
                        this.HasPendingAffinityEdits = false;
                        this.UpdateAffinityDisplayState();
                        this.SetStatus(result.Message, false);
                        _ = this.notificationService.ShowNotificationAsync("Affinity adjusted", result.Message, NotificationType.Warning);
                    }
                    else if (result.FailureReason == AffinityApplyFailureReason.ProcessTerminated)
                    {
                        this.SelectedProcess = null;
                        this.ClearProcessSelection();
                        this.SetCriticalStatus(result.Message);
                        _ = this.notificationService.ShowNotificationAsync("Affinity failed", result.Message, NotificationType.Warning);
                    }
                    else if (result.FailureReason == AffinityApplyFailureReason.AccessDenied)
                    {
                        this.SetCriticalStatus(result.Message);
                        _ = this.notificationService.ShowNotificationAsync("Affinity blocked", result.Message, NotificationType.Warning);
                    }
                    else if (result.IsInvalidTopology || result.IsLegacyFallbackBlocked)
                    {
                        this.SetCriticalStatus(result.Message);
                        _ = this.notificationService.ShowNotificationAsync("Affinity blocked", result.Message, NotificationType.Warning);
                    }
                    else
                    {
                        this.SetStatus(result.Message, false);
                        _ = this.notificationService.ShowNotificationAsync("Affinity error", result.Message, NotificationType.Error);
                    }
                });
            }
            catch (Exception ex)
            {
                var friendly = ex.Message;
                _ = this.notificationService.ShowNotificationAsync("Affinity error", friendly, NotificationType.Error);

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.SetCriticalStatus($"Error setting affinity: {friendly}");
                });

                // Try to refresh process info even if setting failed, to show current state
                try
                {
                    if (this.SelectedProcess != null)
                    {
                        await this.processService.RefreshProcessInfo(this.SelectedProcess);
                    }

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (this.SelectedProcess != null)
                        {
                            this.UpdateCoreSelections(this.SelectedProcess.ProcessorAffinity, true);
                            this.OnPropertyChanged(nameof(this.SelectedProcess));
                        }
                    });
                }
                catch
                {
                    // Process may have terminated
                }
            }
            finally
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.ClearStatus();
                });
            }
        }

        [RelayCommand]
        private async Task ApplyAffinityPreset(CpuAffinityPreset preset)
        {
            if (preset == null || !preset.IsAvailable || this.CpuTopology == null)
            {
                return;
            }

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    this.suppressCoreSelectionEvents = true;

                    // Clear all selections first
                    foreach (var core in this.CpuCores)
                    {
                        core.IsSelected = false;
                    }

                    // Apply preset mask
                    foreach (var core in this.CpuCores)
                    {
                        core.IsSelected = (preset.AffinityMask & core.AffinityMask) != 0;
                    }

                    // Notify UI of changes
                    this.OnPropertyChanged(nameof(this.CpuCores));
                    this.SetStatus($"Applied preset: {preset.Name}");
                }
                finally
                {
                    this.suppressCoreSelectionEvents = false;
                }

                // Keep the preset as a pending selection; affinity changes require an explicit apply command.
                this.HasPendingAffinityEdits = true;
                this.UpdateAffinityDisplayState();
            });
        }


        [RelayCommand]
        private void CreateCustomMask()
        {
            // Request to switch to Core Masks tab
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var mainWindow = System.Windows.Application.Current.MainWindow;
                if (mainWindow != null)
                {
                    // Find the TabControl in MainWindow
                    var tabControl = FindVisualChild<System.Windows.Controls.TabControl>(mainWindow);
                    if (tabControl != null)
                    {
                        // Switch to Core Masks tab (index 1)
                        tabControl.SelectedIndex = 1;
                    }
                }
            });
        }

        private static T? FindVisualChild<T>(System.Windows.DependencyObject obj)
            where T : System.Windows.DependencyObject
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(obj, i);
                if (child is T typedChild)
                {
                    return typedChild;
                }

                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
            return null;
        }

        /// <summary>
        /// Called when a CoreMask is selected from the ComboBox.
        /// </summary>
        partial void OnSelectedCoreMaskChanged(CoreMask? oldValue, CoreMask? newValue)
        {
            if (newValue == null)
                return;

            UpdateCoreSelectionsFromMask(newValue);
        }

        private async Task ApplyCoreMaskToProcessAsync(CoreMask mask)
        {
            var selectedProcess = this.SelectedProcess;
            if (selectedProcess == null || mask == null)
            {
                return;
            }

            this.IsBusy = true;
            try
            {
                this.Logger.LogInformation(
                    "Applying mask '{MaskName}' to process {ProcessName} (PID: {ProcessId})",
                    mask.Name, selectedProcess.Name, selectedProcess.ProcessId);

                // Disable Windows Game Mode for better CPU affinity control
                // Game Mode can interfere with CPU Sets, particularly on AMD systems
                await this.gameModeService.DisableGameModeForAffinityAsync();

                var result = await this.processAffinityApplyCoordinator.ApplyCoreMaskAsync(selectedProcess, mask);

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    selectedProcess.ForceNotifyProcessorAffinityChanged();
                    if (!result.UsedCpuSets)
                    {
                        this.UpdateCoreSelections(selectedProcess.ProcessorAffinity, true);
                    }

                    this.OnPropertyChanged(nameof(this.SelectedProcess));
                });

                if (!result.Success)
                {
                    this.SetStatus(result.Message);
                    this.Logger.LogWarning(
                        "Failed to apply mask '{MaskName}' to process {ProcessName}: {Message}",
                        mask.Name,
                        selectedProcess.Name,
                        result.Message);
                    return;
                }

                this.HasPendingAffinityEdits = false;
                this.UpdateAffinityDisplayState();
                this.SetStatus($"Applied mask '{mask.Name}' to {selectedProcess.Name}");
                this.Logger.LogInformation("Successfully applied mask '{MaskName}' to {ProcessName}", mask.Name, selectedProcess.Name);
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "Failed to apply mask '{MaskName}' to process {ProcessName}",
                    mask.Name, selectedProcess.Name);
                this.SetStatus($"Error applying mask: {ex.Message}");
            }
            finally
            {
                this.IsBusy = false;
            }
        }

        private void UpdateCoreSelectionsFromMask(CoreMask mask)
        {
            if (mask == null || this.CpuCores.Count == 0)
            {
                return;
            }

            try
            {
                this.suppressCoreSelectionEvents = true;

                for (int i = 0; i < this.CpuCores.Count && i < mask.BoolMask.Count; i++)
                {
                    this.CpuCores[i].IsSelected = mask.BoolMask[i];
                }

                this.OnPropertyChanged(nameof(this.CpuCores));
                this.HasPendingAffinityEdits = this.SelectedProcess != null;
                this.UpdateAffinityDisplayState();
            }
            finally
            {
                this.suppressCoreSelectionEvents = false;
            }
        }


        [RelayCommand]
        private async Task QuickApplyAffinityAndPowerPlan()
        {
            var selectedProcess = this.SelectedProcess;
            if (selectedProcess == null)
            {
                return;
            }

            try
            {
                var affinityAppliedWithCpuSets = false;

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.SetStatus($"Applying pending settings to {selectedProcess.Name}...");
                });

                // Apply CPU affinity
                var pendingSelection = this.GetPendingCoreSelectionMask();
                if (pendingSelection.Any(selected => selected))
                {
                    var result = await this.processAffinityApplyCoordinator.ApplyCoreSelectionAsync(
                        selectedProcess,
                        pendingSelection,
                        "Manual Process tab quick apply CPU selection");
                    if (!result.Success)
                    {
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            if (!result.UsedCpuSets)
                            {
                                this.UpdateCoreSelections(selectedProcess.ProcessorAffinity, true);
                            }

                            selectedProcess.ForceNotifyProcessorAffinityChanged();
                            this.OnPropertyChanged(nameof(this.SelectedProcess));
                            this.SetStatus(result.Message, false);
                        });
                        return;
                    }

                    this.HasPendingAffinityEdits = false;
                    this.UpdateAffinityDisplayState();
                    affinityAppliedWithCpuSets = result.UsedCpuSets;
                }

                // Apply power plan if selected
                if (this.SelectedPowerPlan != null)
                {
                    await this.powerPlanService.SetActivePowerPlan(this.SelectedPowerPlan);
                }

                await this.processService.RefreshProcessInfo(selectedProcess);

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (!affinityAppliedWithCpuSets)
                    {
                        this.UpdateCoreSelections(selectedProcess.ProcessorAffinity, true);
                    }
                    else
                    {
                        this.UpdateAffinityDisplayState();
                    }

                    selectedProcess.ForceNotifyProcessorAffinityChanged();
                    this.OnPropertyChanged(nameof(this.SelectedProcess));
                });

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.SetStatus($"Pending settings applied to {selectedProcess.Name}.", false);
                });
            }
            catch (Exception ex)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.SetStatus($"Error applying pending settings: {ex.Message}", false);
                });
            }
        }

        [RelayCommand]
        private async Task RefreshTopology()
        {
            try
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.SetStatus("Refreshing CPU topology...");
                });
                await this.cpuTopologyService.RefreshTopologyAsync();
            }
            catch (Exception ex)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.SetStatus($"Error refreshing topology: {ex.Message}", false);
                });
            }
        }

        [RelayCommand]
        private async Task SetPowerPlan()
        {
            if (this.SelectedPowerPlan == null)
            {
                return;
            }

            try
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.SetStatus($"Setting power plan to {this.SelectedPowerPlan.Name}...");
                });

                var success = await this.powerPlanService.SetActivePowerPlan(this.SelectedPowerPlan);

                await this.RefreshPowerPlansAsync();

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var activePlan = this.PowerPlans.FirstOrDefault(p => p.IsActive);
                    if (success && activePlan?.Guid == this.SelectedPowerPlan.Guid)
                    {
                        this.SetStatus($"Power plan set successfully to {this.SelectedPowerPlan.Name}", false);
                    }
                    else
                    {
                        this.SetStatus($"Power plan change attempted - current plan: {activePlan?.Name ?? "Unknown"}", false);
                    }
                });
            }
            catch (Exception ex)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.SetStatus($"Error setting power plan: {ex.Message}", false);
                });

                try
                {
                    await this.RefreshPowerPlansAsync();
                }
                catch
                {
                    // ignored
                }
            }
            finally
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(this.ClearStatus);
            }
        }

        [RelayCommand]
        private async Task SetPriority(ProcessPriorityClass priority)
        {
            if (this.SelectedProcess == null)
            {
                return;
            }

            try
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.SetStatus($"Setting priority for {this.SelectedProcess.Name} to {priority}...");
                });

                // Apply the priority change
                await this.processService.SetProcessPriority(this.SelectedProcess, priority);

                // Immediately refresh the process to get the actual system state
                await this.processService.RefreshProcessInfo(this.SelectedProcess);

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    // Notify UI that the process properties have changed
                    this.OnPropertyChanged(nameof(this.SelectedProcess));

                    // Verify the priority was set correctly
                    if (this.SelectedProcess.Priority == priority)
                    {
                        var warning = ProcessPriorityGuardrails.GetWarning(priority);
                        if (!string.IsNullOrWhiteSpace(warning))
                        {
                            this.SetCriticalStatus(warning);
                            _ = this.notificationService.ShowNotificationAsync("Priority warning", warning, NotificationType.Warning);
                        }
                        else
                        {
                            this.SetStatus($"Priority applied successfully to {this.SelectedProcess.Name}: {priority}.", false);
                            _ = this.notificationService.ShowNotificationAsync("Priority applied", $"{this.SelectedProcess.Name}: {priority}", NotificationType.Success);
                        }
                    }
                    else
                    {
                        this.SetStatus($"Priority adjusted by system for {this.SelectedProcess.Name} to {this.SelectedProcess.Priority}.", false);
                        _ = this.notificationService.ShowNotificationAsync("Priority adjusted", $"{this.SelectedProcess.Name}: {this.SelectedProcess.Priority}", NotificationType.Warning);
                    }
                });
            }
            catch (Exception ex)
            {
                var message = ex.Message;
                if (message.Contains("Realtime priority is blocked", StringComparison.OrdinalIgnoreCase))
                {
                    message = ProcessOperationUserMessages.RealtimePriorityBlocked;
                    _ = this.notificationService.ShowNotificationAsync("Priority blocked", message, NotificationType.Warning);
                }
                else if (message.Contains("Access denied", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("anti-cheat", StringComparison.OrdinalIgnoreCase))
                {
                    message = ProcessOperationUserMessages.AccessDenied;
                    _ = this.notificationService.ShowNotificationAsync("Priority blocked", message, NotificationType.Warning);
                }
                else
                {
                    _ = this.notificationService.ShowNotificationAsync("Priority error", message, NotificationType.Error);
                }

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.SetCriticalStatus($"Error setting priority: {message}");
                });

                // Try to refresh process info even if setting failed, to show current state
                try
                {
                    await this.processService.RefreshProcessInfo(this.SelectedProcess);
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        this.OnPropertyChanged(nameof(this.SelectedProcess));
                    });
                }
                catch
                {
                    // Process may have terminated
                }
            }
            finally
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(this.ClearStatus);
            }
        }

        [RelayCommand]
        private Task SetContextBelowNormalPriority(ProcessModel? process) =>
            this.SetContextCpuPriorityAsync(process, ProcessPriorityClass.BelowNormal);

        [RelayCommand]
        private Task SetContextNormalPriority(ProcessModel? process) =>
            this.SetContextCpuPriorityAsync(process, ProcessPriorityClass.Normal);

        [RelayCommand]
        private Task SetContextAboveNormalPriority(ProcessModel? process) =>
            this.SetContextCpuPriorityAsync(process, ProcessPriorityClass.AboveNormal);

        [RelayCommand]
        private Task SetContextHighPriority(ProcessModel? process) =>
            this.SetContextCpuPriorityAsync(process, ProcessPriorityClass.High);

        [RelayCommand]
        private Task SetContextMemoryPriorityVeryLow(ProcessModel? process) =>
            this.SetContextMemoryPriorityAsync(process, ProcessMemoryPriority.VeryLow);

        [RelayCommand]
        private Task SetContextMemoryPriorityLow(ProcessModel? process) =>
            this.SetContextMemoryPriorityAsync(process, ProcessMemoryPriority.Low);

        [RelayCommand]
        private Task SetContextMemoryPriorityMedium(ProcessModel? process) =>
            this.SetContextMemoryPriorityAsync(process, ProcessMemoryPriority.Medium);

        [RelayCommand]
        private Task SetContextMemoryPriorityBelowNormal(ProcessModel? process) =>
            this.SetContextMemoryPriorityAsync(process, ProcessMemoryPriority.BelowNormal);

        [RelayCommand]
        private Task SetContextMemoryPriorityNormal(ProcessModel? process) =>
            this.SetContextMemoryPriorityAsync(process, ProcessMemoryPriority.Normal);

        [RelayCommand]
        private async Task ClearContextCpuSets(ProcessModel? process)
        {
            if (process == null)
            {
                return;
            }

            try
            {
                var success = await this.processService.ClearProcessCpuSetAsync(process);
                if (!success)
                {
                    this.SetContextError(ProcessOperationUserMessages.AccessDenied);
                    await this.UpdateSelectedProcessSummaryAsync(process);
                    return;
                }

                await this.processService.RefreshProcessInfo(process);
                this.SetStatus($"CPU Sets cleared for {process.Name}.", false);
                await this.UpdateSelectedProcessSummaryAsync(process);
            }
            catch (Exception ex)
            {
                this.SetContextError(MapProcessOperationException(ex));
                await this.TryRefreshContextProcessSummaryAsync(process);
            }
        }

        [RelayCommand]
        private async Task RefreshContextProcessInfo(ProcessModel? process)
        {
            if (process == null)
            {
                return;
            }

            try
            {
                await this.processService.RefreshProcessInfo(process);
                this.SetStatus($"Process info refreshed for {process.Name}.", false);
                await this.UpdateSelectedProcessSummaryAsync(process);
            }
            catch (Exception ex)
            {
                this.SetContextError(MapProcessOperationException(ex));
                await this.TryRefreshContextProcessSummaryAsync(process);
            }
        }

        [RelayCommand]
        private async Task OpenContextExecutableLocation(ProcessModel? process)
        {
            if (process == null)
            {
                return;
            }

            var path = process.ExecutablePath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                this.SetContextError($"Executable path is unavailable for {process.Name}.");
                await this.UpdateSelectedProcessSummaryAsync(process);
                return;
            }

            try
            {
                this.executableLocationOpener(path);
                this.SetStatus($"Opened executable location for {process.Name}.", false);
                await this.UpdateSelectedProcessSummaryAsync(process);
            }
            catch (Exception ex)
            {
                this.SetContextError($"Could not open executable location: {ex.Message}");
                await this.UpdateSelectedProcessSummaryAsync(process);
            }
        }

        [RelayCommand]
        private async Task CopyContextProcessInfo(ProcessModel? process)
        {
            if (process == null)
            {
                return;
            }

            await this.UpdateSelectedProcessSummaryAsync(process);

            var path = string.IsNullOrWhiteSpace(process.ExecutablePath)
                ? "unavailable"
                : process.ExecutablePath;
            var builder = new StringBuilder()
                .AppendLine($"Name: {process.Name}")
                .AppendLine($"PID: {process.ProcessId}")
                .AppendLine($"Path: {path}")
                .AppendLine($"CPU priority: {process.Priority}")
                .AppendLine($"Memory priority: {this.SelectedProcessSummary.MemoryPriority?.ToString() ?? "unavailable"}")
                .AppendLine($"Affinity: 0x{process.ProcessorAffinity:X}")
                .AppendLine($"Rule status: {this.SelectedProcessSummary.RuleStatusText}");

            try
            {
                this.clipboardSetter(builder.ToString().TrimEnd());
                this.SetStatus($"Copied process info for {process.Name}.", false);
                await this.UpdateSelectedProcessSummaryAsync(process);
            }
            catch (Exception ex)
            {
                this.SetContextError($"Could not copy process info: {ex.Message}");
                await this.UpdateSelectedProcessSummaryAsync(process);
            }
        }

        private async Task SetContextCpuPriorityAsync(ProcessModel? process, ProcessPriorityClass priority)
        {
            if (process == null)
            {
                return;
            }

            if (ProcessPriorityGuardrails.IsBlocked(priority))
            {
                this.SetContextError(ProcessOperationUserMessages.RealtimePriorityBlocked);
                await this.UpdateSelectedProcessSummaryAsync(process);
                return;
            }

            try
            {
                await this.processService.SetProcessPriority(process, priority);
                await this.processService.RefreshProcessInfo(process);

                var warning = ProcessPriorityGuardrails.GetWarning(priority);
                if (!string.IsNullOrWhiteSpace(warning))
                {
                    this.SetCriticalStatus(warning);
                    _ = this.notificationService.ShowNotificationAsync("Priority warning", warning, NotificationType.Warning);
                }
                else
                {
                    this.SetStatus($"Priority applied successfully to {process.Name}: {priority}.", false);
                    _ = this.notificationService.ShowNotificationAsync("Priority applied", $"{process.Name}: {priority}", NotificationType.Success);
                }

                await this.UpdateSelectedProcessSummaryAsync(process);
            }
            catch (Exception ex)
            {
                var message = MapProcessOperationException(ex);
                this.SetContextError(message);
                _ = this.notificationService.ShowNotificationAsync("Priority blocked", message, NotificationType.Warning);
                await this.TryRefreshContextProcessSummaryAsync(process);
            }
        }

        private async Task SetContextMemoryPriorityAsync(ProcessModel? process, ProcessMemoryPriority priority)
        {
            if (process == null)
            {
                return;
            }

            if (this.memoryPriorityService == null)
            {
                this.SetContextError("Memory priority is unavailable on this system.");
                await this.UpdateSelectedProcessSummaryAsync(process);
                return;
            }

            try
            {
                var result = await this.memoryPriorityService.SetMemoryPriorityAsync(process, priority);
                if (!result.Success)
                {
                    this.SetContextError(string.IsNullOrWhiteSpace(result.UserMessage)
                        ? ProcessOperationUserMessages.AccessDenied
                        : result.UserMessage);
                    await this.UpdateSelectedProcessSummaryAsync(process);
                    return;
                }

                this.SetStatus($"Memory priority applied successfully to {process.Name}: {priority}.", false);
                await this.UpdateSelectedProcessSummaryAsync(process);
            }
            catch (Exception ex)
            {
                this.SetContextError(MapProcessOperationException(ex));
                await this.UpdateSelectedProcessSummaryAsync(process);
            }
        }

        private async Task TryRefreshContextProcessSummaryAsync(ProcessModel process)
        {
            try
            {
                await this.processService.RefreshProcessInfo(process);
            }
            catch
            {
                // The selected process may have exited or become inaccessible; keep the safe user message.
            }

            await this.UpdateSelectedProcessSummaryAsync(process);
        }

        private void SetContextError(string message)
        {
            this.SetStatus(message, false);
            this.SetError(message);
        }

        private static string MapProcessOperationException(Exception exception)
        {
            var message = exception.Message ?? string.Empty;
            if (message.Contains("Realtime priority", StringComparison.OrdinalIgnoreCase))
            {
                return ProcessOperationUserMessages.RealtimePriorityBlocked;
            }

            if (message.Contains("anti-cheat", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("protected", StringComparison.OrdinalIgnoreCase))
            {
                return ProcessOperationUserMessages.AntiCheatProtectedLikely;
            }

            if (message.Contains("exited", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("terminated", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("no longer exists", StringComparison.OrdinalIgnoreCase))
            {
                return ProcessOperationUserMessages.ProcessExited;
            }

            if (exception is UnauthorizedAccessException ||
                message.Contains("access denied", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("denied", StringComparison.OrdinalIgnoreCase))
            {
                return ProcessOperationUserMessages.AccessDenied;
            }

            return string.IsNullOrWhiteSpace(message) ? ProcessOperationUserMessages.AccessDenied : message;
        }

        [RelayCommand]
        private async Task SaveProfile()
        {
            if (this.SelectedProcess == null || string.IsNullOrWhiteSpace(this.ProfileName))
            {
                return;
            }

            try
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.SetStatus($"Saving profile {this.ProfileName}...");
                });
                await this.processService.SaveProcessProfile(this.ProfileName, this.SelectedProcess);
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.ClearStatus();
                });
            }
            catch (Exception ex)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.SetStatus($"Error saving profile: {ex.Message}", false);
                });
            }
        }

        [RelayCommand]
        private async Task LoadProfile()
        {
            if (this.SelectedProcess == null || string.IsNullOrWhiteSpace(this.ProfileName))
            {
                return;
            }

            try
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.SetStatus($"Loading profile {this.ProfileName}...");
                });
                var success = await this.processService.LoadProcessProfile(this.ProfileName, this.SelectedProcess);
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (success)
                    {
                        this.ClearStatus();
                    }
                    else
                    {
                        this.SetCriticalStatus($"Profile {this.ProfileName} could not be fully applied.");
                    }
                });
            }
            catch (Exception ex)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.SetCriticalStatus($"Error loading profile: {ex.Message}");
                });
            }
        }

        private void SetupRefreshTimer()
        {
            this.refreshTimer = new System.Timers.Timer(5000); // PERFORMANCE OPTIMIZATION: Increased to 5 second refresh for better performance
            this.refreshTimer.Elapsed += async (s, e) =>
            {
                if (this.isUiRefreshPaused || !this.isProcessViewActive)
                {
                    return;
                }

                try
                {
                    // Marshal timer callback to UI thread to prevent cross-thread access exceptions
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        if (this.isUiRefreshPaused || !this.isProcessViewActive)
                        {
                            return;
                        }

                        await this.RefreshProcessesCommand.ExecuteAsync(null);
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Timer refresh error: {ex.Message}");
                }
            };
            // Don't start automatically - only start when needed
        }

        public void PauseRefresh()
        {
            this.SetUiRefreshEnabled(false, refreshImmediately: false);
        }

        public void ResumeRefresh()
        {
            this.SetUiRefreshEnabled(true, refreshImmediately: true);
        }

        public void ApplyRefreshDecision(AppRefreshDecision decision)
        {
            ArgumentNullException.ThrowIfNull(decision);

            this.virtualizedProcessService.Configuration.EnableBackgroundLoading = decision.VirtualizedPreloadEnabled;
            this.SetUiRefreshEnabled(decision.ProcessUiRefreshEnabled, decision.ImmediateProcessRefresh);
        }

        public void SetProcessViewActive(bool isActive)
        {
            if (this.isProcessViewActive == isActive)
            {
                this.virtualizedProcessService.Configuration.EnableBackgroundLoading = isActive && !this.isUiRefreshPaused;
                return;
            }

            this.isProcessViewActive = isActive;
            this.virtualizedProcessService.Configuration.EnableBackgroundLoading = isActive && !this.isUiRefreshPaused;

            if (!isActive)
            {
                this.refreshTimer?.Stop();
                return;
            }

            if (!this.isUiRefreshPaused)
            {
                this.refreshTimer?.Start();
                _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    try
                    {
                        await this.RefreshProcessesCommand.ExecuteAsync(null);
                    }
                    catch (Exception ex)
                    {
                        this.Logger.LogDebug(ex, "Immediate process refresh after returning to process view failed");
                    }
                });
            }
        }

        public void SetUiRefreshEnabled(bool enabled, bool refreshImmediately = true)
        {
            this.isUiRefreshPaused = !enabled;
            this.virtualizedProcessService.Configuration.EnableBackgroundLoading = enabled && this.isProcessViewActive;

            if (!enabled)
            {
                this.refreshTimer?.Stop();
                return;
            }

            if (this.isProcessViewActive)
            {
                this.refreshTimer?.Start();
            }

            if (!refreshImmediately || !this.isProcessViewActive)
            {
                return;
            }

            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                if (this.isUiRefreshPaused)
                {
                    return;
                }

                try
                {
                    this.ClearStatus();
                    await this.RefreshProcessesCommand.ExecuteAsync(null);
                }
                catch (Exception ex)
                {
                    this.Logger.LogDebug(ex, "Immediate process refresh after resume failed");
                }
            });
        }

        partial void OnSearchTextChanged(string value)
        {
            this.searchRefreshCoordinator.Schedule();
        }

        partial void OnShowActiveApplicationsOnlyChanged(bool value)
        {
            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                await LoadProcessesCommand.ExecuteAsync(null);
            });
        }

        partial void OnHideSystemProcessesChanged(bool value)
        {
            this.filterRefreshCoordinator.Schedule();
        }

        partial void OnHideIdleProcessesChanged(bool value)
        {
            this.filterRefreshCoordinator.Schedule();
        }

        partial void OnSortModeChanged(string value)
        {
            this.filterRefreshCoordinator.Schedule();
        }

        partial void OnIsIdleServerDisabledChanged(bool value)
        {
            if (SelectedProcess != null)
            {
                _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    await ToggleIdleServerAsync(value);
                });
            }
        }

        partial void OnIsRegistryPriorityEnabledChanged(bool value)
        {
            if (SelectedProcess != null)
            {
                _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    await ToggleRegistryPriorityAsync(value);
                });
            }
        }

        private void FilterProcesses()
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(this.FilterProcesses);
                return;
            }

            if (this.isApplyingFilter)
            {
                this.filterRefreshPending = true;
                return;
            }

            this.isApplyingFilter = true;

            try
            {
                do
                {
                    this.filterRefreshPending = false;

                    var criteria = new ProcessFilterCriteria
                    {
                        SearchText = this.SearchText,
                        HideSystemProcesses = this.HideSystemProcesses,
                        HideIdleProcesses = this.HideIdleProcesses,
                        SortMode = this.SortMode,
                    };

                    var filteredResults = this.processFilterService.FilterAndSort(this.Processes, criteria);
                    this.FilteredProcesses = new ObservableCollection<ProcessModel>(filteredResults);
                }
                while (this.filterRefreshPending);
            }
            finally
            {
                this.isApplyingFilter = false;
            }
        }

        private async Task LoadProcessPowerPlanAssociation(ProcessModel process)
        {
            try
            {
                await this.RefreshPowerPlansAsync();
            }
            catch (Exception ex)
            {
                this.Logger.LogWarning(ex, "Failed to load power plan association for process {ProcessName}", process.Name);
            }
        }

        private void ClearProcessSelection()
        {
            // Clear CPU core selections
            foreach (var core in this.CpuCores)
            {
                core.IsSelected = false;
            }

            this.HasPendingAffinityEdits = false;
            this.UpdateAffinityDisplayState();

            // Reset power plan to current system default
            _ = Task.Run(async () =>
            {
                try
                {
                    await this.RefreshPowerPlansAsync();
                }
                catch (Exception ex)
                {
                    this.Logger.LogWarning(ex, "Failed to reset power plan selection");
                }
            });

            // Notify UI of changes
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // Reset feature states
                this.IsIdleServerDisabled = false;
                this.IsRegistryPriorityEnabled = false;

                this.OnPropertyChanged(nameof(this.CpuCores));

                // BUG FIX: Clear status without setting busy state and auto-clear after delay
                this.SetStatus("Process selection cleared", false);

                // Clear the status after a short delay
                _ = Task.Delay(2000).ContinueWith(_ =>
                {
                    System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (this.StatusMessage == "Process selection cleared")
                        {
                            this.ClearStatus();
                        }
                    });
                });
            });
        }

        /// <summary>
        /// Toggles the idle server functionality for the selected process.
        /// </summary>
        private async Task ToggleIdleServerAsync(bool disable)
        {
            if (this.SelectedProcess == null)
            {
                return;
            }

            try
            {
                this.SetStatus($"{(disable ? "Disabling" : "Enabling")} idle server for {this.SelectedProcess.Name}...");

                // Implementation for disabling/enabling idle server
                // This typically involves setting process execution state or power management settings
                var success = await this.processService.SetIdleServerStateAsync(this.SelectedProcess, !disable);

                if (success)
                {
                    this.SelectedProcess.IsIdleServerDisabled = disable;
                    this.SetStatus($"Idle server {(disable ? "disabled" : "enabled")} for {this.SelectedProcess.Name}");

                    await this.LogUserActionAsync(
                        "IdleServer",
                        $"Idle server {(disable ? "disabled" : "enabled")} for process {this.SelectedProcess.Name}",
                        $"PID: {this.SelectedProcess.ProcessId}");
                }
                else
                {
                    this.SetStatus($"Failed to {(disable ? "disable" : "enable")} idle server for {this.SelectedProcess.Name}", false);
                    // Revert the UI state
                    this.IsIdleServerDisabled = !disable;
                }
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "Error toggling idle server for process {ProcessName}", this.SelectedProcess.Name);
                this.SetStatus($"Error: {ex.Message}", false);
                // Revert the UI state
                this.IsIdleServerDisabled = !disable;
            }
        }

        /// <summary>
        /// Toggles registry-based priority enforcement for the selected process.
        /// </summary>
        private async Task ToggleRegistryPriorityAsync(bool enable)
        {
            if (this.SelectedProcess == null)
            {
                return;
            }

            try
            {
                this.SetStatus($"{(enable ? "Enabling" : "Disabling")} registry priority enforcement for {this.SelectedProcess.Name}...");

                // Implementation for registry-based priority setting
                var success = await this.processService.SetRegistryPriorityAsync(this.SelectedProcess, enable, this.SelectedProcess.Priority);

                if (success)
                {
                    this.SelectedProcess.IsRegistryPriorityEnabled = enable;

                    if (enable)
                    {
                        this.SetStatus($"Registry priority enforcement enabled for {this.SelectedProcess.Name}. Process restart required for changes to take effect.");

                        // Show notification about restart requirement
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            System.Windows.MessageBox.Show(
                                $"Registry priority has been set for {this.SelectedProcess.Name}.\n\n" +
                                "The process must be restarted for the registry changes to take effect.\n\n" +
                                $"{ProcessOperationUserMessages.PersistentLaunchTimePriorityNotice}\n\n" +
                                "This setting will persist across system reboots and will automatically apply the selected priority when the process starts.",
                                "Registry Priority Set - Restart Required",
                                System.Windows.MessageBoxButton.OK,
                                System.Windows.MessageBoxImage.Information);
                        });
                    }
                    else
                    {
                        this.SetStatus($"Registry priority enforcement disabled for {this.SelectedProcess.Name}");
                    }

                    await this.LogUserActionAsync(
                        "RegistryPriority",
                        $"Registry priority enforcement {(enable ? "enabled" : "disabled")} for process {this.SelectedProcess.Name}",
                        $"PID: {this.SelectedProcess.ProcessId}, Priority: {this.SelectedProcess.Priority}");
                }
                else
                {
                    this.SetStatus($"Failed to {(enable ? "enable" : "disable")} registry priority enforcement for {this.SelectedProcess.Name}", false);
                    // Revert the UI state
                    this.IsRegistryPriorityEnabled = !enable;
                }
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "Error toggling registry priority for process {ProcessName}", this.SelectedProcess.Name);
                this.SetStatus($"Error: {ex.Message}", false);
                // Revert the UI state
                this.IsRegistryPriorityEnabled = !enable;
            }
        }

        /// <summary>
        /// Saves the current process settings (affinity mask, priority, power plan) as an association
        /// Based on CPUSetSetter's SetMask pattern.
        /// </summary>
        [RelayCommand]
        private void OpenRulesTab()
        {
            this.OpenRulesRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private async Task SaveCurrentAsAssociation()
        {
            if (this.SelectedProcess == null)
            {
                await this.notificationService.ShowNotificationAsync(
                    "No Process Selected",
                    "Please select a process to save as an association", NotificationType.Warning);
                return;
            }

            try
            {
                this.SetStatus($"Saving rule for {this.SelectedProcess.Name}...");

                // Get current power plan
                var currentPowerPlan = await this.powerPlanService.GetActivePowerPlan();

                // Create new association
                var association = new ProcessPowerPlanAssociation
                {
                    ExecutableName = this.SelectedProcess.Name,
                    ExecutablePath = this.SelectedProcess.ExecutablePath ?? string.Empty,
                    PowerPlanGuid = currentPowerPlan?.Guid ?? string.Empty,
                    PowerPlanName = currentPowerPlan?.Name ?? "Unknown",
                    CoreMaskId = this.SelectedCoreMask?.Id,
                    CoreMaskName = this.SelectedCoreMask?.Name,
                    ProcessPriority = this.SelectedProcess.Priority.ToString(),
                    MatchByPath = !string.IsNullOrEmpty(this.SelectedProcess.ExecutablePath),
                    Priority = 0,
                    Description = $"Saved from Process Management on {DateTime.Now:g}",
                    IsEnabled = true,
                };

                // Try to add the association
                var success = await this.associationService.AddAssociationAsync(association);

                if (success)
                {
                    this.SetStatus($"Rule created for {this.SelectedProcess.Name} and ready for auto-apply.", false);
                    await this.notificationService.ShowNotificationAsync(
                        "Rule Saved",
                        $"Settings for {this.SelectedProcess.Name} saved successfully", NotificationType.Success);

                    await this.LogUserActionAsync(
                        "SaveAssociation",
                        $"Saved association for process {this.SelectedProcess.Name}",
                        $"PID: {this.SelectedProcess.ProcessId}, PowerPlan: {currentPowerPlan?.Name}, " +
                        $"CoreMask: {this.SelectedCoreMask?.Name ?? "None"}, Priority: {this.SelectedProcess.Priority}");
                }
                else
                {
                    var existingAssociation = await this.associationService.FindAssociationByExecutableAsync(this.SelectedProcess.Name);
                    if (existingAssociation != null)
                    {
                        existingAssociation.ExecutablePath = association.ExecutablePath;
                        existingAssociation.PowerPlanGuid = association.PowerPlanGuid;
                        existingAssociation.PowerPlanName = association.PowerPlanName;
                        existingAssociation.CoreMaskId = association.CoreMaskId;
                        existingAssociation.CoreMaskName = association.CoreMaskName;
                        existingAssociation.ProcessPriority = association.ProcessPriority;
                        existingAssociation.MatchByPath = association.MatchByPath;
                        existingAssociation.Description = association.Description;
                        existingAssociation.IsEnabled = true;
                        existingAssociation.UpdatedAt = DateTime.UtcNow;

                        var updated = await this.associationService.UpdateAssociationAsync(existingAssociation);
                        if (updated)
                        {
                            this.SetStatus($"Existing rule updated for {this.SelectedProcess.Name}.", false);
                            await this.notificationService.ShowNotificationAsync(
                                "Rule Updated",
                                $"Existing rule for {this.SelectedProcess.Name} was updated", NotificationType.Information);
                        }
                        else
                        {
                            this.SetStatus($"Failed to update existing rule for {this.SelectedProcess.Name}", false);
                            await this.notificationService.ShowNotificationAsync(
                                "Rule Update Failed",
                                $"Could not update existing rule for {this.SelectedProcess.Name}", NotificationType.Warning);
                        }
                    }
                    else
                    {
                        this.SetStatus($"Rule already exists for {this.SelectedProcess.Name}", false);
                        await this.notificationService.ShowNotificationAsync(
                            "Rule Exists",
                            $"A rule for {this.SelectedProcess.Name} already exists", NotificationType.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "Error saving association for process {ProcessName}", this.SelectedProcess.Name);
                this.SetStatus($"Error saving rule: {ex.Message}", false);
                await this.notificationService.ShowNotificationAsync(
                    "Error",
                    $"Failed to save rule: {ex.Message}", NotificationType.Error);
            }
        }

        protected override void OnDispose()
        {
            this.refreshTimer?.Stop();
            this.refreshTimer?.Dispose();
            this.refreshTimer = null;

            this.searchRefreshCoordinator.Dispose();
            this.filterRefreshCoordinator.Dispose();

            this.cpuTopologyService.TopologyDetected -= this.OnTopologyDetected;
            this.systemTrayService.QuickApplyRequested -= this.OnTrayQuickApplyRequested;

            base.OnDispose();
        }
    }
}
