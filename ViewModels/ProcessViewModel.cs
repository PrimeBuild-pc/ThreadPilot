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
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows.Input;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using Microsoft.Extensions.Logging;
    using ThreadPilot.Models;
    using ThreadPilot.Services;

    public partial class ProcessViewModel : BaseViewModel
    {
        public event EventHandler? OpenRulesRequested;

        private readonly IProcessService processService;
        private readonly IVirtualizedProcessService virtualizedProcessService;
        private readonly ICpuTopologyService cpuTopologyService;
        private readonly IPowerPlanService powerPlanService;
        private readonly INotificationService notificationService;
        private readonly ISystemTrayService systemTrayService;
        private readonly ICoreMaskService coreMaskService;
        private readonly IProcessPowerPlanAssociationService associationService;
        private readonly IGameModeService gameModeService;
        private System.Timers.Timer? refreshTimer;
        private System.Timers.Timer? searchDebounceTimer;
        private bool isApplyingFilter;
        private bool filterRefreshPending;
        private bool suppressCoreSelectionEvents;
        private bool hasPendingAffinityEdits;

        [ObservableProperty]
        private ObservableCollection<ProcessModel> processes = new();

        [ObservableProperty]
        private ProcessModel? selectedProcess;

        [ObservableProperty]
        private string searchText = string.Empty;

        [ObservableProperty]
        private ObservableCollection<ProcessModel> filteredProcesses = new();

        [ObservableProperty]
        private string profileName = string.Empty;

        // CPU Topology and Affinity
        [ObservableProperty]
        private CpuTopologyModel? cpuTopology;

        [ObservableProperty]
        private ObservableCollection<CpuCoreModel> cpuCores = new();

        [ObservableProperty]
        private ObservableCollection<CpuAffinityPreset> affinityPresets = new();

        // Core Masks - Available masks from the service
        [ObservableProperty]
        private ObservableCollection<CoreMask> availableCoreMasks = new();

        [ObservableProperty]
        private CoreMask? selectedCoreMask;

        [ObservableProperty]
        private bool isTopologyDetectionSuccessful = false;

        [ObservableProperty]
        private string topologyStatus = "Detecting CPU topology...";

        [ObservableProperty]
        private bool areAdvancedFeaturesAvailable = false;

        [ObservableProperty]
        private PowerPlanModel? selectedPowerPlan;

        [ObservableProperty]
        private ObservableCollection<PowerPlanModel> powerPlans = new();

        // Note: EnableHyperThreading property removed - now using read-only status indicator

        [ObservableProperty]
        private bool showActiveApplicationsOnly = false;

        [ObservableProperty]
        private bool hideSystemProcesses = false;

        [ObservableProperty]
        private bool hideIdleProcesses = false;

        [ObservableProperty]
        private string sortMode = "CpuUsage";

        // Hyperthreading/SMT Status Properties
        [ObservableProperty]
        private string hyperThreadingStatusText = "Multi-Threading: Unknown";

        [ObservableProperty]
        private bool isHyperThreadingActive = false;

        // New feature properties
        [ObservableProperty]
        private bool isIdleServerDisabled = false;

        [ObservableProperty]
        private bool isRegistryPriorityEnabled = false;

        // PERFORMANCE IMPROVEMENT: Progressive loading support
        [ObservableProperty]
        private double loadingProgress = 0.0;

        [ObservableProperty]
        private string loadingStatusText = string.Empty;

        // VIRTUALIZATION ENHANCEMENT: Batch loading support
        [ObservableProperty]
        private int currentBatchIndex = 0;

        [ObservableProperty]
        private int totalBatches = 0;

        [ObservableProperty]
        private int totalProcessCount = 0;

        [ObservableProperty]
        private bool hasMoreBatches = false;

        [ObservableProperty]
        private bool isVirtualizationEnabled = true;

        public ProcessViewModel(
            ILogger<ProcessViewModel> logger,
            IProcessService processService,
            IVirtualizedProcessService virtualizedProcessService,
            ICpuTopologyService cpuTopologyService,
            IPowerPlanService powerPlanService,
            INotificationService notificationService,
            ISystemTrayService systemTrayService,
            ICoreMaskService coreMaskService,
            IProcessPowerPlanAssociationService associationService,
            IGameModeService gameModeService,
            IEnhancedLoggingService? enhancedLoggingService = null)
            : base(logger, enhancedLoggingService)
        {
            this.processService = processService ?? throw new ArgumentNullException(nameof(processService));
            this.virtualizedProcessService = virtualizedProcessService ?? throw new ArgumentNullException(nameof(virtualizedProcessService));
            this.cpuTopologyService = cpuTopologyService ?? throw new ArgumentNullException(nameof(cpuTopologyService));
            this.powerPlanService = powerPlanService ?? throw new ArgumentNullException(nameof(powerPlanService));
            this.notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            this.systemTrayService = systemTrayService ?? throw new ArgumentNullException(nameof(systemTrayService));
            this.coreMaskService = coreMaskService ?? throw new ArgumentNullException(nameof(coreMaskService));
            this.associationService = associationService ?? throw new ArgumentNullException(nameof(associationService));
            this.gameModeService = gameModeService ?? throw new ArgumentNullException(nameof(gameModeService));

            // Subscribe to topology detection events
            this.cpuTopologyService.TopologyDetected += this.OnTopologyDetected;

            // Subscribe to system tray events
            this.systemTrayService.QuickApplyRequested += this.OnTrayQuickApplyRequested;

            this.SetupRefreshTimer();
            this.SetupVirtualizedProcessService();
            // Note: InitializeAsync() will be called explicitly by MainWindow loading overlay
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
            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                this.LoadingProgress = e.ProgressPercentage;
                this.LoadingStatusText = e.StatusMessage;
            });
        }

        private void OnBackgroundBatchLoaded(object? sender, ProcessBatchResult e)
        {
            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // Background batch loaded - could update UI if needed
                this.Logger.LogDebug(
                    "Background batch {BatchIndex} loaded with {ProcessCount} processes",
                    e.BatchIndex, e.Processes.Count);
            });
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
            if (value != null && CpuTopology != null)
            {
                hasPendingAffinityEdits = false;
                // Immediately fetch and display real-time process information
                TaskSafety.FireAndForget(HandleSelectedProcessChangedAsync(value), ex =>
                {
                    Logger.LogWarning(ex, "Failed while handling selected process change for {ProcessName}", value.Name);
                });
            }
            else if (value == null)
            {
                // Clear selection
                ClearProcessSelection();
            }

            // Update system tray context menu
            systemTrayService.UpdateContextMenu(value?.Name, value != null);
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
                    // Update CPU affinity display
                    // If a Core Mask is selected, show that instead of the process's current affinity
                    // This ensures visual coherence between selected mask and Advanced CPU Affinity
                    if (this.SelectedCoreMask != null)
                    {
                        this.UpdateCoreSelectionsFromMask(this.SelectedCoreMask);
                        // Auto-apply the selected mask to the newly selected process
                        _ = this.ApplyCoreMaskToProcessAsync(this.SelectedCoreMask);
                    }
                    else
                    {
                        // No mask selected - show process's current affinity
                        this.UpdateCoreSelections(value.ProcessorAffinity);

                        // Force ProcessorAffinity notification for DataGrid column
                        // Ensures Affinity column displays the correct current value immediately
                        value.ForceNotifyProcessorAffinityChanged();
                    }

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
                        $"Settings applied to {this.SelectedProcess?.Name ?? "selected process"}", 2000);
                });
            }
            catch (Exception ex)
            {
                // Marshal UI operations to the UI thread to prevent cross-thread access exceptions
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.systemTrayService.ShowBalloonTip(
                        "ThreadPilot Error",
                        $"Failed to apply settings: {ex.Message}", 3000);
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

        private async Task AutoApplyAffinityAsync()
        {
            if (this.SelectedProcess == null || !this.hasPendingAffinityEdits)
            {
                return;
            }

            try
            {
                var affinityMask = this.CalculateAffinityMask();
                if (affinityMask == 0)
                {
                    this.Logger.LogDebug("Affinity mask is zero, skipping auto-apply");
                    return;
                }

                this.Logger.LogInformation(
                    "Auto-applying affinity 0x{AffinityMask:X} to process {ProcessName} (PID: {ProcessId})",
                    affinityMask, this.SelectedProcess.Name, this.SelectedProcess.ProcessId);

                // Apply the affinity change
                await this.processService.SetProcessorAffinity(this.SelectedProcess, affinityMask);

                // Immediately refresh the process to get the actual system state
                await this.processService.RefreshProcessInfo(this.SelectedProcess);

                // Update UI to reflect the actual system affinity
                this.UpdateCoreSelections(this.SelectedProcess.ProcessorAffinity, true);

                // Notify UI of all changes
                this.OnPropertyChanged(nameof(this.SelectedProcess));

                // Clear pending edits flag
                this.hasPendingAffinityEdits = false;

                this.Logger.LogInformation("Auto-applied affinity successfully to {ProcessName}", this.SelectedProcess.Name);
            }
            catch (Exception ex)
            {
                this.Logger.LogWarning(ex, "Failed to auto-apply affinity to {ProcessName}", this.SelectedProcess.Name);

                // Try to refresh process info even if setting failed, to show current state
                try
                {
                    await this.processService.RefreshProcessInfo(this.SelectedProcess);
                    this.UpdateCoreSelections(this.SelectedProcess.ProcessorAffinity, true);
                    this.OnPropertyChanged(nameof(this.SelectedProcess));
                }
                catch
                {
                    // Process may have terminated
                }
            }
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

            if (this.hasPendingAffinityEdits && !forceSync)
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
                this.hasPendingAffinityEdits = false;
            }
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
                // Store the currently selected process ID to preserve selection
                var selectedProcessId = this.SelectedProcess?.ProcessId;

                var currentProcesses = this.ShowActiveApplicationsOnly
                    ? await this.processService.GetActiveApplicationsAsync()
                    : await this.processService.GetProcessesAsync();

                // Update UI on the UI thread
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    // Update existing processes or add new ones
                    foreach (var process in currentProcesses)
                    {
                        var existingProcess = this.Processes.FirstOrDefault(p => p.ProcessId == process.ProcessId);
                        if (existingProcess != null)
                        {
                            // Update existing process data by copying properties
                            existingProcess.MemoryUsage = process.MemoryUsage;
                            existingProcess.Priority = process.Priority;
                            existingProcess.ProcessorAffinity = process.ProcessorAffinity;
                            existingProcess.MainWindowHandle = process.MainWindowHandle;
                            existingProcess.MainWindowTitle = process.MainWindowTitle;
                            existingProcess.HasVisibleWindow = process.HasVisibleWindow;
                            existingProcess.CpuUsage = process.CpuUsage;
                        }
                        else
                        {
                            this.Processes.Add(process);
                        }
                    }

                    // Remove terminated processes
                    var terminatedProcesses = this.Processes
                        .Where(p => !currentProcesses.Any(cp => cp.ProcessId == p.ProcessId))
                        .ToList();

                    // Check if selected process was terminated
                    bool selectedProcessTerminated = false;
                    foreach (var terminated in terminatedProcesses)
                    {
                        if (terminated.ProcessId == selectedProcessId)
                        {
                            selectedProcessTerminated = true;
                        }
                        this.Processes.Remove(terminated);
                    }

                    this.FilterProcesses();

                    // Restore selection if the process still exists
                    if (selectedProcessId.HasValue && !selectedProcessTerminated)
                    {
                        var processToSelect = this.FilteredProcesses.FirstOrDefault(p => p.ProcessId == selectedProcessId.Value);
                        if (processToSelect != null)
                        {
                            this.SelectedProcess = processToSelect;
                        }
                    }
                    else if (selectedProcessTerminated)
                    {
                        // Clear selection and reset UI if selected process was terminated
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
            if (this.SelectedProcess == null)
            {
                return;
            }

            try
            {
                var affinityMask = this.CalculateAffinityMask();
                if (affinityMask == 0)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        this.SetStatus("Please select at least one CPU core", false);
                    });
                    return;
                }

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.SetStatus($"Setting affinity for {this.SelectedProcess.Name}...");
                });

                // Apply the affinity change
                await this.processService.SetProcessorAffinity(this.SelectedProcess, affinityMask);

                // Immediately refresh the process to get the actual system state
                await this.processService.RefreshProcessInfo(this.SelectedProcess);

                // Update UI to reflect the actual system affinity (not our calculated one)
                // This ensures we show what the OS actually set, which may differ from our request
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.UpdateCoreSelections(this.SelectedProcess.ProcessorAffinity, true);

                    // Notify UI of all changes
                    this.OnPropertyChanged(nameof(this.SelectedProcess));

                    // Verify the affinity was set correctly
                    if (this.SelectedProcess.ProcessorAffinity == affinityMask)
                    {
                        this.SetStatus($"Affinity applied successfully to {this.SelectedProcess.Name} (0x{affinityMask:X}).", false);
                        _ = this.notificationService.ShowNotificationAsync("Affinity applied", $"{this.SelectedProcess.Name}: 0x{affinityMask:X}", NotificationType.Success);
                    }
                    else
                    {
                        this.SetStatus($"Affinity adjusted by system for {this.SelectedProcess.Name} to 0x{this.SelectedProcess.ProcessorAffinity:X}.", false);
                        _ = this.notificationService.ShowNotificationAsync("Affinity adjusted", $"{this.SelectedProcess.Name}: 0x{this.SelectedProcess.ProcessorAffinity:X}", NotificationType.Warning);
                    }
                });
            }
            catch (Exception ex)
            {
                var friendly = ex.Message;
                if (friendly.Contains("access denied", StringComparison.OrdinalIgnoreCase) ||
                    friendly.Contains("anti-cheat", StringComparison.OrdinalIgnoreCase))
                {
                    friendly = "Affinity change blocked (anti-cheat or insufficient privileges).";
                    _ = this.notificationService.ShowNotificationAsync("Affinity blocked", friendly, NotificationType.Warning);
                }
                else if (friendly.Contains("invalid affinity", StringComparison.OrdinalIgnoreCase))
                {
                    _ = this.notificationService.ShowNotificationAsync("Affinity invalid", friendly, NotificationType.Error);
                }
                else
                {
                    _ = this.notificationService.ShowNotificationAsync("Affinity error", friendly, NotificationType.Error);
                }

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.SetStatus($"Error setting affinity: {friendly}", false);
                });

                // Try to refresh process info even if setting failed, to show current state
                try
                {
                    await this.processService.RefreshProcessInfo(this.SelectedProcess);
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        this.UpdateCoreSelections(this.SelectedProcess.ProcessorAffinity, true);
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

                // Trigger auto-apply with the preset mask
                this.hasPendingAffinityEdits = true;

                // Apply immediately for presets (no debounce needed)
                _ = this.AutoApplyAffinityAsync();
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
        /// Called when a CoreMask is selected from the ComboBox - applies it immediately to the selected process
        /// Based on CPUSetSetter's OnMaskChanged pattern
        /// </summary>
        partial void OnSelectedCoreMaskChanged(CoreMask? oldValue, CoreMask? newValue)
        {
            if (newValue == null)
                return;

            // ALWAYS update Advanced CPU Affinity visual representation to match the selected mask
            // This ensures UI coherence even when no process is selected
            UpdateCoreSelectionsFromMask(newValue);

            // If a process is selected, apply the mask to it
            if (SelectedProcess != null)
            {
                _ = ApplyCoreMaskToProcessAsync(newValue);
            }
        }

        private async Task ApplyCoreMaskToProcessAsync(CoreMask mask)
        {
            if (this.SelectedProcess == null || mask == null)
            {
                return;
            }

            this.IsBusy = true;
            try
            {
                this.Logger.LogInformation(
                    "Applying mask '{MaskName}' to process {ProcessName} (PID: {ProcessId})",
                    mask.Name, this.SelectedProcess.Name, this.SelectedProcess.ProcessId);

                // Convert mask to affinity
                long affinity = mask.ToProcessorAffinity();

                if (affinity == 0)
                {
                    this.Logger.LogWarning("Mask '{MaskName}' produces zero affinity, skipping", mask.Name);
                    this.SetStatus("Invalid mask: no cores selected");
                    return;
                }

                // Disable Windows Game Mode for better CPU affinity control
                // Game Mode can interfere with CPU Sets, particularly on AMD systems
                await this.gameModeService.DisableGameModeForAffinityAsync();

                // Apply affinity using ProcessService (which uses CPU Sets with fallback)
                await this.processService.SetProcessorAffinity(this.SelectedProcess, affinity);

                // Refresh process info to get actual system state
                await this.processService.RefreshProcessInfo(this.SelectedProcess);

                // CRITICAL: Force UI updates on UI thread
                // RefreshProcessInfo runs on background thread, DataGrid won't receive PropertyChanged from non-UI threads
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    // Force PropertyChanged notification for ProcessorAffinity property
                    // This ensures the DataGrid Affinity column binding updates immediately
                    this.SelectedProcess.ForceNotifyProcessorAffinityChanged();

                    // Update Advanced CPU Affinity checkboxes to reflect the mask
                    this.UpdateCoreSelectionsFromMask(mask);

                    // Force complete refresh of SelectedProcess bindings in DataGrid
                    this.OnPropertyChanged(nameof(this.SelectedProcess));
                });

                this.SetStatus($"Applied mask '{mask.Name}' to {this.SelectedProcess.Name}");
                this.Logger.LogInformation("Successfully applied mask '{MaskName}' to {ProcessName}", mask.Name, this.SelectedProcess.Name);
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "Failed to apply mask '{MaskName}' to process {ProcessName}",
                    mask.Name, this.SelectedProcess.Name);
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
            }
            finally
            {
                this.suppressCoreSelectionEvents = false;
            }
        }


        [RelayCommand]
        private async Task QuickApplyAffinityAndPowerPlan()
        {
            if (this.SelectedProcess == null)
            {
                return;
            }

            try
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.SetStatus($"Applying settings to {this.SelectedProcess.Name}...");
                });

                // Apply CPU affinity
                var affinityMask = this.CalculateAffinityMask();
                if (affinityMask > 0)
                {
                    await this.processService.SetProcessorAffinity(this.SelectedProcess, affinityMask);
                }

                // Apply power plan if selected
                if (this.SelectedPowerPlan != null)
                {
                    await this.powerPlanService.SetActivePowerPlan(this.SelectedPowerPlan);
                }

                await this.processService.RefreshProcessInfo(this.SelectedProcess);

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.UpdateCoreSelections(this.SelectedProcess.ProcessorAffinity, true);
                    this.OnPropertyChanged(nameof(this.SelectedProcess));
                });

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.SetStatus($"Quick apply completed for {this.SelectedProcess.Name}.", false);
                });
            }
            catch (Exception ex)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.SetStatus($"Error applying settings: {ex.Message}", false);
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
                        this.SetStatus($"Priority applied successfully to {this.SelectedProcess.Name}: {priority}.", false);
                        _ = this.notificationService.ShowNotificationAsync("Priority applied", $"{this.SelectedProcess.Name}: {priority}", NotificationType.Success);
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
                if (message.Contains("Access denied", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("anti-cheat", StringComparison.OrdinalIgnoreCase))
                {
                    message = "Priority change blocked (anti-cheat or insufficient privileges).";
                    _ = this.notificationService.ShowNotificationAsync("Priority blocked", message, NotificationType.Warning);
                }
                else
                {
                    _ = this.notificationService.ShowNotificationAsync("Priority error", message, NotificationType.Error);
                }

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.SetStatus($"Error setting priority: {message}", false);
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
                await this.processService.LoadProcessProfile(this.ProfileName, this.SelectedProcess);
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.ClearStatus();
                });
            }
            catch (Exception ex)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.SetStatus($"Error loading profile: {ex.Message}", false);
                });
            }
        }

        private void SetupRefreshTimer()
        {
            this.refreshTimer = new System.Timers.Timer(5000); // PERFORMANCE OPTIMIZATION: Increased to 5 second refresh for better performance
            this.refreshTimer.Elapsed += async (s, e) =>
            {
                try
                {
                    // Marshal timer callback to UI thread to prevent cross-thread access exceptions
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
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
            this.refreshTimer?.Stop();
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // BUG FIX: Don't set busy state when pausing refresh
                this.SetStatus("Process monitoring paused (minimized)", false);
            });
        }

        public void ResumeRefresh()
        {
            this.refreshTimer?.Start();
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // BUG FIX: Clear busy state when resuming refresh to prevent stuck loading state
                this.ClearStatus();
                this.SetStatus("Process monitoring resumed", false);

                // Clear the status after a short delay to avoid persistent status message
                _ = Task.Delay(2000).ContinueWith(_ =>
                {
                    System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (this.StatusMessage == "Process monitoring resumed")
                        {
                            this.ClearStatus();
                        }
                    });
                });
            });
        }

        partial void OnSearchTextChanged(string value)
        {
            // PERFORMANCE OPTIMIZATION: Debounce search to prevent excessive filtering
            searchDebounceTimer?.Stop();
            searchDebounceTimer?.Dispose();

            searchDebounceTimer = new System.Timers.Timer(300); // 300ms debounce
            searchDebounceTimer.Elapsed += (s, e) =>
            {
                searchDebounceTimer?.Stop();
                searchDebounceTimer?.Dispose();
                searchDebounceTimer = null;

                // Marshal UI updates to the UI thread to prevent cross-thread access exceptions
                System.Windows.Application.Current.Dispatcher.InvokeAsync(() => FilterProcesses());
            };
            searchDebounceTimer.Start();
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
            // PERFORMANCE OPTIMIZATION: Debounce filter operations
            DebounceFilterOperation();
        }

        partial void OnHideIdleProcessesChanged(bool value)
        {
            // PERFORMANCE OPTIMIZATION: Debounce filter operations
            DebounceFilterOperation();
        }

        partial void OnSortModeChanged(string value)
        {
            // PERFORMANCE OPTIMIZATION: Debounce filter operations
            DebounceFilterOperation();
        }

        private System.Timers.Timer? filterDebounceTimer;

        private void DebounceFilterOperation()
        {
            this.filterDebounceTimer?.Stop();
            this.filterDebounceTimer?.Dispose();

            this.filterDebounceTimer = new System.Timers.Timer(100); // 100ms debounce for filter operations
            this.filterDebounceTimer.Elapsed += (s, e) =>
            {
                this.filterDebounceTimer?.Stop();
                this.filterDebounceTimer?.Dispose();
                this.filterDebounceTimer = null;

                // Marshal UI updates to the UI thread to prevent cross-thread access exceptions
                System.Windows.Application.Current.Dispatcher.InvokeAsync(() => this.FilterProcesses());
            };
            this.filterDebounceTimer.Start();
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
            var filtered = this.Processes.AsEnumerable();

            try
            {
                do
                {
                    this.filterRefreshPending = false;

                    filtered = this.Processes.AsEnumerable();

                    if (!string.IsNullOrWhiteSpace(this.SearchText))
                    {
                        filtered = filtered.Where(p => p.Name.Contains(this.SearchText, StringComparison.OrdinalIgnoreCase));
                    }

                    if (this.HideSystemProcesses)
                    {
                        filtered = filtered.Where(p => !IsSystemProcess(p));
                    }

                    if (this.HideIdleProcesses)
                    {
                        filtered = filtered.Where(p => p.CpuUsage > 0.1);
                    }

                    filtered = this.SortMode switch
                    {
                        "CpuUsage" => filtered.OrderByDescending(p => p.CpuUsage),
                        "MemoryUsage" => filtered.OrderByDescending(p => p.MemoryUsage),
                        "Name" => filtered.OrderBy(p => p.Name),
                        "ProcessId" => filtered.OrderBy(p => p.ProcessId),
                        _ => filtered.OrderByDescending(p => p.CpuUsage),
                    };

                    this.FilteredProcesses = new ObservableCollection<ProcessModel>(filtered);
                }
                while (this.filterRefreshPending);
            }
            finally
            {
                this.isApplyingFilter = false;
            }
        }

        private static bool IsSystemProcess(ProcessModel process)
        {
            var systemProcesses = new[]
            {
                "System", "Registry", "smss.exe", "csrss.exe", "wininit.exe", "winlogon.exe",
                "services.exe", "lsass.exe", "svchost.exe", "spoolsv.exe", "explorer.exe",
                "dwm.exe", "audiodg.exe", "conhost.exe", "dllhost.exe", "rundll32.exe",
                "taskhostw.exe", "SearchIndexer.exe", "WmiPrvSE.exe", "MsMpEng.exe",
                "SecurityHealthService.exe", "SecurityHealthSystray.exe",
            };

            return systemProcesses.Any(sp => process.Name.Equals(sp, StringComparison.OrdinalIgnoreCase)) ||
                   process.Name.StartsWith("System", StringComparison.OrdinalIgnoreCase);
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
    }
}
