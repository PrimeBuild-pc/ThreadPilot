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

namespace ThreadPilot.ViewModels
{
    public partial class ProcessViewModel : BaseViewModel
    {
        private readonly IProcessService _processService;
        private readonly IVirtualizedProcessService _virtualizedProcessService;
        private readonly ICpuTopologyService _cpuTopologyService;
        private readonly IPowerPlanService _powerPlanService;
        private readonly INotificationService _notificationService;
        private readonly ISystemTrayService _systemTrayService;
        private readonly ICoreMaskService _coreMaskService;
        private readonly IProcessPowerPlanAssociationService _associationService;
        private readonly IGameModeService _gameModeService;
        private System.Timers.Timer? _refreshTimer;
        private System.Timers.Timer? _searchDebounceTimer;
        private bool _suppressCoreSelectionEvents;
        private bool _hasPendingAffinityEdits;

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

        [ObservableProperty]
        private bool isBusy = false;

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
            _processService = processService ?? throw new ArgumentNullException(nameof(processService));
            _virtualizedProcessService = virtualizedProcessService ?? throw new ArgumentNullException(nameof(virtualizedProcessService));
            _cpuTopologyService = cpuTopologyService ?? throw new ArgumentNullException(nameof(cpuTopologyService));
            _powerPlanService = powerPlanService ?? throw new ArgumentNullException(nameof(powerPlanService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _systemTrayService = systemTrayService ?? throw new ArgumentNullException(nameof(systemTrayService));
            _coreMaskService = coreMaskService ?? throw new ArgumentNullException(nameof(coreMaskService));
            _associationService = associationService ?? throw new ArgumentNullException(nameof(associationService));
            _gameModeService = gameModeService ?? throw new ArgumentNullException(nameof(gameModeService));

            // Subscribe to topology detection events
            _cpuTopologyService.TopologyDetected += OnTopologyDetected;

            // Subscribe to system tray events
            _systemTrayService.QuickApplyRequested += OnTrayQuickApplyRequested;

            SetupRefreshTimer();
            SetupVirtualizedProcessService();
            // Note: InitializeAsync() will be called explicitly by MainWindow loading overlay
        }

        private void SetupVirtualizedProcessService()
        {
            // Configure virtualization settings
            _virtualizedProcessService.Configuration.BatchSize = 50;
            _virtualizedProcessService.Configuration.EnableBackgroundLoading = true;

            // Subscribe to events
            _virtualizedProcessService.BatchLoadProgress += OnBatchLoadProgress;
            _virtualizedProcessService.BackgroundBatchLoaded += OnBackgroundBatchLoaded;
        }

        private async void OnBatchLoadProgress(object? sender, BatchLoadProgressEventArgs e)
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                LoadingProgress = e.ProgressPercentage;
                LoadingStatusText = e.StatusMessage;
            });
        }

        private async void OnBackgroundBatchLoaded(object? sender, ProcessBatchResult e)
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // Background batch loaded - could update UI if needed
                Logger.LogDebug("Background batch {BatchIndex} loaded with {ProcessCount} processes",
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
                    SetStatus("Initializing CPU topology and power plans...");
                });
                System.Diagnostics.Debug.WriteLine("ProcessViewModel.InitializeAsync: Starting initialization");

                // Initialize CPU topology
                System.Diagnostics.Debug.WriteLine("ProcessViewModel.InitializeAsync: About to detect CPU topology");
                await _cpuTopologyService.DetectTopologyAsync();
                System.Diagnostics.Debug.WriteLine("ProcessViewModel.InitializeAsync: CPU topology detection completed");

                // Initialize core masks service
                System.Diagnostics.Debug.WriteLine("ProcessViewModel.InitializeAsync: About to initialize core masks");
                await _coreMaskService.InitializeAsync();
                AvailableCoreMasks = _coreMaskService.AvailableMasks;
                SelectedCoreMask = _coreMaskService.DefaultMask;
                System.Diagnostics.Debug.WriteLine("ProcessViewModel.InitializeAsync: Core masks initialized");

                // Load power plans
                System.Diagnostics.Debug.WriteLine("ProcessViewModel.InitializeAsync: About to load power plans");
                await RefreshPowerPlansAsync();
                System.Diagnostics.Debug.WriteLine("ProcessViewModel.InitializeAsync: Power plans loaded");

                // Load processes automatically on startup (Bug #8 fix)
                System.Diagnostics.Debug.WriteLine("ProcessViewModel.InitializeAsync: About to load processes automatically");
                await LoadProcessesCommand.ExecuteAsync(null);

                // Access process count on UI thread to avoid threading issues
                int processCount = 0;
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    processCount = Processes?.Count ?? 0;
                });
                System.Diagnostics.Debug.WriteLine($"ProcessViewModel.InitializeAsync: Processes loaded automatically, count: {processCount}");

                // Start refresh timer for real-time updates
                System.Diagnostics.Debug.WriteLine("ProcessViewModel.InitializeAsync: Starting refresh timer");
                _refreshTimer?.Start();
                System.Diagnostics.Debug.WriteLine("ProcessViewModel.InitializeAsync: Initialization completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ProcessViewModel.InitializeAsync: Exception occurred: {ex.Message}");
                // Update status on UI thread
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    SetStatus($"Failed to initialize: {ex.Message}", false);
                });
            }
        }

        private async Task RefreshPowerPlansAsync()
        {
            try
            {
                var plans = await _powerPlanService.GetPowerPlansAsync();
                var activePlan = await _powerPlanService.GetActivePowerPlan();

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    PowerPlans.Clear();
                    foreach (var plan in plans)
                    {
                        plan.IsActive = plan.Guid == activePlan?.Guid;
                        PowerPlans.Add(plan);
                    }

                    SelectedPowerPlan = PowerPlans.FirstOrDefault(p => p.Guid == activePlan?.Guid);
                });
            }
            catch (Exception ex)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    SetStatus($"Failed to load power plans: {ex.Message}", false);
                });
            }
        }

        partial void OnSelectedProcessChanged(ProcessModel? value)
        {
            if (value != null && CpuTopology != null)
            {
                _hasPendingAffinityEdits = false;
                // Immediately fetch and display real-time process information
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // First check if the process is still running
                        bool isStillRunning = await _processService.IsProcessStillRunning(value);
                        if (!isStillRunning)
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                SetStatus($"Process {value.Name} (PID: {value.ProcessId}) has terminated", false);
                                SelectedProcess = null;
                                ClearProcessSelection();
                            });
                            return;
                        }

                        // Refresh process info to get current state from OS
                        await _processService.RefreshProcessInfo(value);

                        // Update UI on main thread with fresh data
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            // Update CPU affinity display
                            // If a Core Mask is selected, show that instead of the process's current affinity
                            // This ensures visual coherence between selected mask and Advanced CPU Affinity
                            if (SelectedCoreMask != null)
                            {
                                UpdateCoreSelectionsFromMask(SelectedCoreMask);
                                // Auto-apply the selected mask to the newly selected process
                                _ = ApplyCoreMaskToProcessAsync(SelectedCoreMask);
                            }
                            else
                            {
                                // No mask selected - show process's current affinity
                                UpdateCoreSelections(value.ProcessorAffinity);

                                // Force ProcessorAffinity notification for DataGrid column
                                // Ensures Affinity column displays the correct current value immediately
                                value.ForceNotifyProcessorAffinityChanged();
                            }

                            // Update priority display - trigger property change to refresh ComboBox
                            OnPropertyChanged(nameof(SelectedProcess));

                            // Update feature states from the selected process
                            IsIdleServerDisabled = value.IsIdleServerDisabled;
                            IsRegistryPriorityEnabled = value.IsRegistryPriorityEnabled;

                            // BUG FIX: Update status without setting busy state for process selection
                            SetStatus($"Selected process: {value.Name} (PID: {value.ProcessId}) - " +
                                    $"Priority: {value.Priority}, Affinity: 0x{value.ProcessorAffinity:X}", false);
                        });

                        // Load current power plan association if available
                        await LoadProcessPowerPlanAssociation(value);
                    }
                    catch (InvalidOperationException ex) when (ex.Message.Contains("terminated") || ex.Message.Contains("exited") || ex.Message.Contains("no longer exists"))
                    {
                        // Process has terminated
                        Logger.LogInformation("Process {ProcessName} (PID: {ProcessId}) has terminated", value.Name, value.ProcessId);
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            SetStatus($"Process {value.Name} (PID: {value.ProcessId}) has terminated", false);
                            SelectedProcess = null;
                            ClearProcessSelection();
                        });
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "Failed to refresh process info for {ProcessName}", value.Name);
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            SetStatus($"Warning: Could not access process {value.Name} - it may have terminated or require elevated privileges", false);
                        });
                    }
                });
            }
            else if (value == null)
            {
                // Clear selection
                ClearProcessSelection();
            }

            // Update system tray context menu
            _systemTrayService.UpdateContextMenu(value?.Name, value != null);
        }

        private async void OnTrayQuickApplyRequested(object? sender, EventArgs e)
        {
            try
            {
                // Marshal UI operations to the UI thread to prevent cross-thread access exceptions
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    await QuickApplyAffinityAndPowerPlanCommand.ExecuteAsync(null);
                    _systemTrayService.ShowBalloonTip("ThreadPilot",
                        $"Settings applied to {SelectedProcess?.Name ?? "selected process"}", 2000);
                });
            }
            catch (Exception ex)
            {
                // Marshal UI operations to the UI thread to prevent cross-thread access exceptions
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _systemTrayService.ShowBalloonTip("ThreadPilot Error",
                        $"Failed to apply settings: {ex.Message}", 3000);
                });
            }
        }

        private void OnTopologyDetected(object? sender, CpuTopologyDetectedEventArgs e)
        {
            // Ensure all UI updates happen on the dispatcher thread
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                CpuTopology = e.Topology;
                IsTopologyDetectionSuccessful = e.DetectionSuccessful;

                if (e.DetectionSuccessful)
                {
                    TopologyStatus = $"Detected: {e.Topology.TotalLogicalCores} logical cores, " +
                                   $"{e.Topology.TotalPhysicalCores} physical cores";
                    AreAdvancedFeaturesAvailable = e.Topology.HasIntelHybrid || e.Topology.HasAmdCcd || e.Topology.HasHyperThreading;
                }
                else
                {
                    TopologyStatus = $"Detection failed: {e.ErrorMessage ?? "Unknown error"}";
                    AreAdvancedFeaturesAvailable = false;
                }

                UpdateCpuCores();
                UpdateAffinityPresets();
                UpdateHyperThreadingStatus();
            });
        }

        private void UpdateCpuCores()
        {
            if (CpuTopology == null) return;

            CpuCores.Clear();
            foreach (var core in CpuTopology.LogicalCores)
            {
                core.PropertyChanged -= OnCorePropertyChanged;
                core.PropertyChanged += OnCorePropertyChanged;
                CpuCores.Add(core);
            }
        }

        private void OnCorePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Note: Advanced CPU Affinity cores are now read-only (ProcessView.xaml has IsHitTestVisible="False")
            // This event handler is kept for compatibility but should not be triggered
            // Core modifications are done exclusively through the Core Mask tab
            if (_suppressCoreSelectionEvents) return;

            Logger.LogDebug("Core property changed but cores are read-only - no action taken");
        }

        private async Task AutoApplyAffinityAsync()
        {
            if (SelectedProcess == null || !_hasPendingAffinityEdits) return;

            try
            {
                var affinityMask = CalculateAffinityMask();
                if (affinityMask == 0)
                {
                    Logger.LogDebug("Affinity mask is zero, skipping auto-apply");
                    return;
                }

                Logger.LogInformation("Auto-applying affinity 0x{AffinityMask:X} to process {ProcessName} (PID: {ProcessId})",
                    affinityMask, SelectedProcess.Name, SelectedProcess.ProcessId);

                // Apply the affinity change
                await _processService.SetProcessorAffinity(SelectedProcess, affinityMask);

                // Immediately refresh the process to get the actual system state
                await _processService.RefreshProcessInfo(SelectedProcess);

                // Update UI to reflect the actual system affinity
                UpdateCoreSelections(SelectedProcess.ProcessorAffinity, true);

                // Notify UI of all changes
                OnPropertyChanged(nameof(SelectedProcess));

                // Clear pending edits flag
                _hasPendingAffinityEdits = false;

                Logger.LogInformation("Auto-applied affinity successfully to {ProcessName}", SelectedProcess.Name);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to auto-apply affinity to {ProcessName}", SelectedProcess.Name);

                // Try to refresh process info even if setting failed, to show current state
                try
                {
                    await _processService.RefreshProcessInfo(SelectedProcess);
                    UpdateCoreSelections(SelectedProcess.ProcessorAffinity, true);
                    OnPropertyChanged(nameof(SelectedProcess));
                }
                catch
                {
                    // Process may have terminated
                }
            }
        }

        private void UpdateHyperThreadingStatus()
        {
            if (CpuTopology == null)
            {
                HyperThreadingStatusText = "Multi-Threading: Unknown";
                IsHyperThreadingActive = false;
                return;
            }

            // Determine if hyperthreading/SMT is present and active
            bool hasMultiThreading = CpuTopology.HasHyperThreading;
            IsHyperThreadingActive = hasMultiThreading;

            // Determine the appropriate technology name based on CPU vendor
            string technologyName = "Multi-Threading";
            if (CpuTopology.CpuBrand.Contains("Intel", StringComparison.OrdinalIgnoreCase))
            {
                technologyName = "Hyper-Threading";
            }
            else if (CpuTopology.CpuBrand.Contains("AMD", StringComparison.OrdinalIgnoreCase))
            {
                technologyName = "SMT";
            }

            // Set the status text
            string status = hasMultiThreading ? "Active" : "Not Available";
            HyperThreadingStatusText = $"{technologyName}: {status}";

            Logger.LogInformation("Updated hyperthreading status: {StatusText} (Active: {IsActive})",
                HyperThreadingStatusText, IsHyperThreadingActive);
        }

        private void UpdateAffinityPresets()
        {
            AffinityPresets.Clear();
            var presets = _cpuTopologyService.GetAffinityPresets();
            foreach (var preset in presets)
            {
                AffinityPresets.Add(preset);
            }
        }

        private void UpdateCoreSelections(long affinityMask, bool forceSync = false)
        {
            if (CpuTopology == null || CpuCores.Count == 0)
            {
                Logger.LogWarning("Cannot update core selections: CpuTopology={CpuTopology}, CpuCores.Count={CpuCoresCount}",
                    CpuTopology != null, CpuCores.Count);
                return;
            }

            if (_hasPendingAffinityEdits && !forceSync)
            {
                Logger.LogDebug("Skipping affinity sync because user edits are pending");
                return;
            }

            Logger.LogDebug("Updating core selections for affinity mask 0x{AffinityMask:X} ({AffinityMaskBinary})",
                affinityMask, Convert.ToString(affinityMask, 2).PadLeft(Environment.ProcessorCount, '0'));

            // Update each core's selection state based on the actual OS affinity mask
            var updatedCores = new List<(int CoreId, bool WasSelected, bool IsSelected)>();

            try
            {
                _suppressCoreSelectionEvents = true;

                foreach (var core in CpuCores)
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
                _suppressCoreSelectionEvents = false;
            }

            // The UI will automatically update since CpuCoreModel now implements INotifyPropertyChanged
            // No need to force collection refresh as individual property changes will be notified

            // Log the affinity update for debugging
            var selectedCoreIds = CpuCores.Where(c => c.IsSelected).Select(c => c.LogicalCoreId).OrderBy(id => id).ToList();
            var totalCores = CpuCores.Count;
            var selectedCount = selectedCoreIds.Count;

            Logger.LogInformation("Updated core selections for affinity mask 0x{AffinityMask:X}: " +
                                "Selected {SelectedCount}/{TotalCores} cores: [{CoreIds}]",
                affinityMask, selectedCount, totalCores, string.Join(", ", selectedCoreIds));

            if (updatedCores.Count > 0)
            {
                Logger.LogDebug("Core selection changes: {Changes}",
                    string.Join("; ", updatedCores.Select(c => $"Core {c.CoreId}: {c.WasSelected} -> {c.IsSelected}")));
            }
            else
            {
                Logger.LogDebug("No core selection changes needed - UI already matches affinity mask");
            }

            if (forceSync)
            {
                _hasPendingAffinityEdits = false;
            }
        }

        private long CalculateAffinityMask()
        {
            if (CpuTopology == null) return 0;

            var selectedCores = CpuCores.Where(core => core.IsSelected);

            // Note: Removed hyperthreading filtering - user can manually select desired cores
            // All selected cores (including HT siblings) are now included in the affinity mask

            return selectedCores.Aggregate(0L, (mask, core) => mask | core.AffinityMask);
        }

        [RelayCommand]
        public async Task LoadMoreProcesses()
        {
            if (!IsVirtualizationEnabled || !HasMoreBatches || IsBusy)
                return;

            try
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    SetStatus($"Loading more processes (batch {CurrentBatchIndex + 2})...");
                });

                var nextBatchIndex = CurrentBatchIndex + 1;
                var batch = await _virtualizedProcessService.LoadProcessBatchAsync(nextBatchIndex, ShowActiveApplicationsOnly);

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    // Add new processes to existing collection
                    foreach (var process in batch.Processes)
                    {
                        Processes.Add(process);
                    }

                    CurrentBatchIndex = batch.BatchIndex;
                    TotalBatches = batch.TotalBatches;
                    HasMoreBatches = batch.HasMoreBatches;
                    TotalProcessCount = batch.TotalProcessCount;

                    FilterProcesses();

                    // BUG FIX: Ensure loading state is properly cleared
                    ClearStatus();
                    LoadingProgress = 0.0;
                    LoadingStatusText = string.Empty;
                });

                // Preload next batch in background
                await _virtualizedProcessService.PreloadNextBatchAsync(CurrentBatchIndex, ShowActiveApplicationsOnly);
            }
            catch (Exception ex)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    // BUG FIX: Ensure loading state is cleared even on error
                    LoadingProgress = 0.0;
                    LoadingStatusText = string.Empty;
                    SetStatus($"Error loading more processes: {ex.Message}", false);
                });
            }
        }

        [RelayCommand]
        public async Task LoadProcesses()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"LoadProcesses: Starting, ShowActiveApplicationsOnly={ShowActiveApplicationsOnly}");

                // PERFORMANCE IMPROVEMENT: Progressive loading with status updates
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    LoadingProgress = 0.0;
                    LoadingStatusText = ShowActiveApplicationsOnly ? "Loading active applications..." : "Loading processes...";
                    SetStatus(LoadingStatusText);
                });

                ObservableCollection<ProcessModel> newProcesses;

                // VIRTUALIZATION ENHANCEMENT: Use virtualized loading for large process lists
                if (IsVirtualizationEnabled)
                {
                    System.Diagnostics.Debug.WriteLine("LoadProcesses: Using virtualized loading");
                    await _virtualizedProcessService.InitializeAsync();

                    var totalCount = await _virtualizedProcessService.GetTotalProcessCountAsync(ShowActiveApplicationsOnly);
                    if (totalCount > _virtualizedProcessService.Configuration.BatchSize)
                    {
                        // Load first batch only
                        var batch = await _virtualizedProcessService.LoadProcessBatchAsync(0, ShowActiveApplicationsOnly);
                        newProcesses = new ObservableCollection<ProcessModel>(batch.Processes);

                        // Update virtualization state
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            CurrentBatchIndex = batch.BatchIndex;
                            TotalBatches = batch.TotalBatches;
                            HasMoreBatches = batch.HasMoreBatches;
                            TotalProcessCount = batch.TotalProcessCount;
                        });

                        // Preload next batch in background
                        await _virtualizedProcessService.PreloadNextBatchAsync(0, ShowActiveApplicationsOnly);
                    }
                    else
                    {
                        // Small list, load all processes normally
                        newProcesses = ShowActiveApplicationsOnly
                            ? await _processService.GetActiveApplicationsAsync()
                            : await _processService.GetProcessesAsync();
                    }
                }
                else
                {
                    // Traditional loading
                    if (ShowActiveApplicationsOnly)
                    {
                        System.Diagnostics.Debug.WriteLine("LoadProcesses: Getting active applications");
                        newProcesses = await _processService.GetActiveApplicationsAsync();
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("LoadProcesses: Getting all processes");
                        newProcesses = await _processService.GetProcessesAsync();
                    }
                }

                // Update UI on the UI thread
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Processes = newProcesses;
                    System.Diagnostics.Debug.WriteLine($"LoadProcesses: Retrieved {Processes?.Count ?? 0} processes");
                    FilterProcesses();
                    System.Diagnostics.Debug.WriteLine($"LoadProcesses: After filtering, {FilteredProcesses?.Count ?? 0} processes visible");

                    // BUG FIX: Ensure loading state is properly cleared
                    ClearStatus();
                    LoadingProgress = 0.0;
                    LoadingStatusText = string.Empty;
                });

                System.Diagnostics.Debug.WriteLine("LoadProcesses: Completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadProcesses: Exception occurred: {ex.Message}");
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    // BUG FIX: Ensure loading state is cleared even on error
                    LoadingProgress = 0.0;
                    LoadingStatusText = string.Empty;
                    SetStatus($"Error loading processes: {ex.Message}", false);
                });
            }
        }

        [RelayCommand]
        private async Task RefreshProcesses()
        {
            if (IsBusy) return;

            try
            {
                // Store the currently selected process ID to preserve selection
                var selectedProcessId = SelectedProcess?.ProcessId;

                var currentProcesses = ShowActiveApplicationsOnly
                    ? await _processService.GetActiveApplicationsAsync()
                    : await _processService.GetProcessesAsync();

                // Update UI on the UI thread
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    // Update existing processes or add new ones
                    foreach (var process in currentProcesses)
                    {
                        var existingProcess = Processes.FirstOrDefault(p => p.ProcessId == process.ProcessId);
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
                            Processes.Add(process);
                        }
                    }

                    // Remove terminated processes
                    var terminatedProcesses = Processes
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
                        Processes.Remove(terminated);
                    }

                    FilterProcesses();

                    // Restore selection if the process still exists
                    if (selectedProcessId.HasValue && !selectedProcessTerminated)
                    {
                        var processToSelect = FilteredProcesses.FirstOrDefault(p => p.ProcessId == selectedProcessId.Value);
                        if (processToSelect != null)
                        {
                            SelectedProcess = processToSelect;
                        }
                    }
                    else if (selectedProcessTerminated)
                    {
                        // Clear selection and reset UI if selected process was terminated
                        SelectedProcess = null;
                        ClearProcessSelection();
                    }
                });
            }
            catch (Exception ex)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    SetStatus($"Error refreshing processes: {ex.Message}", false);
                });
            }
        }

        [RelayCommand]
        private async Task SetAffinity()
        {
            if (SelectedProcess == null) return;

            try
            {
                var affinityMask = CalculateAffinityMask();
                if (affinityMask == 0)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        SetStatus("Please select at least one CPU core", false);
                    });
                    return;
                }

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    SetStatus($"Setting affinity for {SelectedProcess.Name}...");
                });

                // Apply the affinity change
                await _processService.SetProcessorAffinity(SelectedProcess, affinityMask);

                // Immediately refresh the process to get the actual system state
                await _processService.RefreshProcessInfo(SelectedProcess);

                // Update UI to reflect the actual system affinity (not our calculated one)
                // This ensures we show what the OS actually set, which may differ from our request
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    UpdateCoreSelections(SelectedProcess.ProcessorAffinity, true);

                    // Notify UI of all changes
                    OnPropertyChanged(nameof(SelectedProcess));

                    // Verify the affinity was set correctly
                    if (SelectedProcess.ProcessorAffinity == affinityMask)
                    {
                        SetStatus($"Affinity set successfully for {SelectedProcess.Name} (0x{affinityMask:X})", false);
                        _ = _notificationService.ShowNotificationAsync("Affinity applied", $"{SelectedProcess.Name}: 0x{affinityMask:X}", NotificationType.Success);
                    }
                    else
                    {
                        SetStatus($"Affinity partially set for {SelectedProcess.Name} - OS adjusted to 0x{SelectedProcess.ProcessorAffinity:X}", false);
                        _ = _notificationService.ShowNotificationAsync("Affinity adjusted", $"{SelectedProcess.Name}: 0x{SelectedProcess.ProcessorAffinity:X}", NotificationType.Warning);
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
                    _ = _notificationService.ShowNotificationAsync("Affinity blocked", friendly, NotificationType.Warning);
                }
                else if (friendly.Contains("invalid affinity", StringComparison.OrdinalIgnoreCase))
                {
                    _ = _notificationService.ShowNotificationAsync("Affinity invalid", friendly, NotificationType.Error);
                }
                else
                {
                    _ = _notificationService.ShowNotificationAsync("Affinity error", friendly, NotificationType.Error);
                }

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    SetStatus($"Error setting affinity: {friendly}", false);
                });

                // Try to refresh process info even if setting failed, to show current state
                try
                {
                    await _processService.RefreshProcessInfo(SelectedProcess);
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        UpdateCoreSelections(SelectedProcess.ProcessorAffinity, true);
                        OnPropertyChanged(nameof(SelectedProcess));
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
                    ClearStatus();
                });
            }
        }

        [RelayCommand]
        private async Task ApplyAffinityPreset(CpuAffinityPreset preset)
        {
            if (preset == null || !preset.IsAvailable || CpuTopology == null) return;

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    _suppressCoreSelectionEvents = true;

                    // Clear all selections first
                    foreach (var core in CpuCores)
                    {
                        core.IsSelected = false;
                    }

                    // Apply preset mask
                    foreach (var core in CpuCores)
                    {
                        core.IsSelected = (preset.AffinityMask & core.AffinityMask) != 0;
                    }

                    // Notify UI of changes
                    OnPropertyChanged(nameof(CpuCores));
                    SetStatus($"Applied preset: {preset.Name}");
                }
                finally
                {
                    _suppressCoreSelectionEvents = false;
                }

                // Trigger auto-apply with the preset mask
                _hasPendingAffinityEdits = true;

                // Apply immediately for presets (no debounce needed)
                _ = AutoApplyAffinityAsync();
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

        private static T? FindVisualChild<T>(System.Windows.DependencyObject obj) where T : System.Windows.DependencyObject
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(obj, i);
                if (child is T typedChild)
                    return typedChild;

                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                    return childOfChild;
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
            if (SelectedProcess == null || mask == null)
                return;

            IsBusy = true;
            try
            {
                Logger.LogInformation("Applying mask '{MaskName}' to process {ProcessName} (PID: {ProcessId})",
                    mask.Name, SelectedProcess.Name, SelectedProcess.ProcessId);

                // Convert mask to affinity
                long affinity = mask.ToProcessorAffinity();

                if (affinity == 0)
                {
                    Logger.LogWarning("Mask '{MaskName}' produces zero affinity, skipping", mask.Name);
                    SetStatus("Invalid mask: no cores selected");
                    return;
                }

                // Disable Windows Game Mode for better CPU affinity control
                // Game Mode can interfere with CPU Sets, particularly on AMD systems
                await _gameModeService.DisableGameModeForAffinityAsync();

                // Apply affinity using ProcessService (which uses CPU Sets with fallback)
                await _processService.SetProcessorAffinity(SelectedProcess, affinity);

                // Refresh process info to get actual system state
                await _processService.RefreshProcessInfo(SelectedProcess);

                // CRITICAL: Force UI updates on UI thread
                // RefreshProcessInfo runs on background thread, DataGrid won't receive PropertyChanged from non-UI threads
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    // Force PropertyChanged notification for ProcessorAffinity property
                    // This ensures the DataGrid Affinity column binding updates immediately
                    SelectedProcess.ForceNotifyProcessorAffinityChanged();

                    // Update Advanced CPU Affinity checkboxes to reflect the mask
                    UpdateCoreSelectionsFromMask(mask);

                    // Force complete refresh of SelectedProcess bindings in DataGrid
                    OnPropertyChanged(nameof(SelectedProcess));
                });

                SetStatus($"Applied mask '{mask.Name}' to {SelectedProcess.Name}");
                Logger.LogInformation("Successfully applied mask '{MaskName}' to {ProcessName}", mask.Name, SelectedProcess.Name);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to apply mask '{MaskName}' to process {ProcessName}",
                    mask.Name, SelectedProcess.Name);
                SetStatus($"Error applying mask: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void UpdateCoreSelectionsFromMask(CoreMask mask)
        {
            if (mask == null || CpuCores.Count == 0)
                return;

            try
            {
                _suppressCoreSelectionEvents = true;

                for (int i = 0; i < CpuCores.Count && i < mask.BoolMask.Count; i++)
                {
                    CpuCores[i].IsSelected = mask.BoolMask[i];
                }

                OnPropertyChanged(nameof(CpuCores));
            }
            finally
            {
                _suppressCoreSelectionEvents = false;
            }
        }


        [RelayCommand]
        private async Task QuickApplyAffinityAndPowerPlan()
        {
            if (SelectedProcess == null) return;

            try
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    SetStatus($"Applying settings to {SelectedProcess.Name}...");
                });

                // Apply CPU affinity
                var affinityMask = CalculateAffinityMask();
                if (affinityMask > 0)
                {
                    await _processService.SetProcessorAffinity(SelectedProcess, affinityMask);
                }

                // Apply power plan if selected
                if (SelectedPowerPlan != null)
                {
                    await _powerPlanService.SetActivePowerPlan(SelectedPowerPlan);
                }

                await _processService.RefreshProcessInfo(SelectedProcess);

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    UpdateCoreSelections(SelectedProcess.ProcessorAffinity, true);
                    OnPropertyChanged(nameof(SelectedProcess));
                });

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    SetStatus($"Settings applied successfully to {SelectedProcess.Name}", false);
                });
            }
            catch (Exception ex)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    SetStatus($"Error applying settings: {ex.Message}", false);
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
                    SetStatus("Refreshing CPU topology...");
                });
                await _cpuTopologyService.RefreshTopologyAsync();
            }
            catch (Exception ex)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    SetStatus($"Error refreshing topology: {ex.Message}", false);
                });
            }
        }

        [RelayCommand]
        private async Task SetPowerPlan()
        {
            if (SelectedPowerPlan == null) return;

            try
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    SetStatus($"Setting power plan to {SelectedPowerPlan.Name}...");
                });

                var success = await _powerPlanService.SetActivePowerPlan(SelectedPowerPlan);

                await RefreshPowerPlansAsync();

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var activePlan = PowerPlans.FirstOrDefault(p => p.IsActive);
                    if (success && activePlan?.Guid == SelectedPowerPlan.Guid)
                    {
                        SetStatus($"Power plan set successfully to {SelectedPowerPlan.Name}", false);
                    }
                    else
                    {
                        SetStatus($"Power plan change attempted - current plan: {activePlan?.Name ?? "Unknown"}", false);
                    }
                });
            }
            catch (Exception ex)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    SetStatus($"Error setting power plan: {ex.Message}", false);
                });

                try
                {
                    await RefreshPowerPlansAsync();
                }
                catch
                {
                    // ignored
                }
            }
            finally
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(ClearStatus);
            }
        }

        [RelayCommand]
        private async Task SetPriority(ProcessPriorityClass priority)
        {
            if (SelectedProcess == null) return;

            try
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    SetStatus($"Setting priority for {SelectedProcess.Name} to {priority}...");
                });

                // Apply the priority change
                await _processService.SetProcessPriority(SelectedProcess, priority);

                // Immediately refresh the process to get the actual system state
                await _processService.RefreshProcessInfo(SelectedProcess);

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    // Notify UI that the process properties have changed
                    OnPropertyChanged(nameof(SelectedProcess));

                    // Verify the priority was set correctly
                    if (SelectedProcess.Priority == priority)
                    {
                        SetStatus($"Priority set successfully for {SelectedProcess.Name} to {priority}", false);
                        _ = _notificationService.ShowNotificationAsync("Priority applied", $"{SelectedProcess.Name}: {priority}", NotificationType.Success);
                    }
                    else
                    {
                        SetStatus($"Priority set for {SelectedProcess.Name} - OS adjusted to {SelectedProcess.Priority}", false);
                        _ = _notificationService.ShowNotificationAsync("Priority adjusted", $"{SelectedProcess.Name}: {SelectedProcess.Priority}", NotificationType.Warning);
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
                    _ = _notificationService.ShowNotificationAsync("Priority blocked", message, NotificationType.Warning);
                }
                else
                {
                    _ = _notificationService.ShowNotificationAsync("Priority error", message, NotificationType.Error);
                }

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    SetStatus($"Error setting priority: {message}", false);
                });

                // Try to refresh process info even if setting failed, to show current state
                try
                {
                    await _processService.RefreshProcessInfo(SelectedProcess);
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        OnPropertyChanged(nameof(SelectedProcess));
                    });
                }
                catch
                {
                    // Process may have terminated
                }
            }
            finally
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(ClearStatus);
            }
        }

        [RelayCommand]
        private async Task SaveProfile()
        {
            if (SelectedProcess == null || string.IsNullOrWhiteSpace(ProfileName)) return;

            try
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    SetStatus($"Saving profile {ProfileName}...");
                });
                await _processService.SaveProcessProfile(ProfileName, SelectedProcess);
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ClearStatus();
                });
            }
            catch (Exception ex)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    SetStatus($"Error saving profile: {ex.Message}", false);
                });
            }
        }

        [RelayCommand]
        private async Task LoadProfile()
        {
            if (SelectedProcess == null || string.IsNullOrWhiteSpace(ProfileName)) return;

            try
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    SetStatus($"Loading profile {ProfileName}...");
                });
                await _processService.LoadProcessProfile(ProfileName, SelectedProcess);
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ClearStatus();
                });
            }
            catch (Exception ex)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    SetStatus($"Error loading profile: {ex.Message}", false);
                });
            }
        }

        private void SetupRefreshTimer()
        {
            _refreshTimer = new System.Timers.Timer(5000); // PERFORMANCE OPTIMIZATION: Increased to 5 second refresh for better performance
            _refreshTimer.Elapsed += async (s, e) =>
            {
                try
                {
                    // Marshal timer callback to UI thread to prevent cross-thread access exceptions
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        await RefreshProcessesCommand.ExecuteAsync(null);
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
            _refreshTimer?.Stop();
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // BUG FIX: Don't set busy state when pausing refresh
                SetStatus("Process monitoring paused (minimized)", false);
            });
        }

        public void ResumeRefresh()
        {
            _refreshTimer?.Start();
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // BUG FIX: Clear busy state when resuming refresh to prevent stuck loading state
                ClearStatus();
                SetStatus("Process monitoring resumed", false);

                // Clear the status after a short delay to avoid persistent status message
                _ = Task.Delay(2000).ContinueWith(_ =>
                {
                    System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (StatusMessage == "Process monitoring resumed")
                        {
                            ClearStatus();
                        }
                    });
                });
            });
        }

        partial void OnSearchTextChanged(string value)
        {
            // PERFORMANCE OPTIMIZATION: Debounce search to prevent excessive filtering
            _searchDebounceTimer?.Stop();
            _searchDebounceTimer?.Dispose();

            _searchDebounceTimer = new System.Timers.Timer(300); // 300ms debounce
            _searchDebounceTimer.Elapsed += (s, e) =>
            {
                _searchDebounceTimer?.Stop();
                _searchDebounceTimer?.Dispose();
                _searchDebounceTimer = null;

                // Marshal UI updates to the UI thread to prevent cross-thread access exceptions
                System.Windows.Application.Current.Dispatcher.InvokeAsync(() => FilterProcesses());
            };
            _searchDebounceTimer.Start();
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

        private System.Timers.Timer? _filterDebounceTimer;

        private void DebounceFilterOperation()
        {
            _filterDebounceTimer?.Stop();
            _filterDebounceTimer?.Dispose();

            _filterDebounceTimer = new System.Timers.Timer(100); // 100ms debounce for filter operations
            _filterDebounceTimer.Elapsed += (s, e) =>
            {
                _filterDebounceTimer?.Stop();
                _filterDebounceTimer?.Dispose();
                _filterDebounceTimer = null;

                // Marshal UI updates to the UI thread to prevent cross-thread access exceptions
                System.Windows.Application.Current.Dispatcher.InvokeAsync(() => FilterProcesses());
            };
            _filterDebounceTimer.Start();
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
            var filtered = Processes.AsEnumerable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                filtered = filtered.Where(p => p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            }

            // Apply system process filter
            if (HideSystemProcesses)
            {
                filtered = filtered.Where(p => !IsSystemProcess(p));
            }

            // Apply idle process filter
            if (HideIdleProcesses)
            {
                filtered = filtered.Where(p => p.CpuUsage > 0.1);
            }

            // Apply sorting
            filtered = SortMode switch
            {
                "CpuUsage" => filtered.OrderByDescending(p => p.CpuUsage),
                "MemoryUsage" => filtered.OrderByDescending(p => p.MemoryUsage),
                "Name" => filtered.OrderBy(p => p.Name),
                "ProcessId" => filtered.OrderBy(p => p.ProcessId),
                _ => filtered.OrderByDescending(p => p.CpuUsage)
            };

            // PERFORMANCE OPTIMIZATION: Update existing collection instead of creating new one
            var filteredList = filtered.ToList();

            // Remove items that are no longer in the filtered list
            for (int i = FilteredProcesses.Count - 1; i >= 0; i--)
            {
                if (!filteredList.Contains(FilteredProcesses[i]))
                {
                    FilteredProcesses.RemoveAt(i);
                }
            }

            // Add new items that aren't already in the collection
            foreach (var item in filteredList)
            {
                if (!FilteredProcesses.Contains(item))
                {
                    FilteredProcesses.Add(item);
                }
            }

            // Reorder existing items if necessary (only if sort order changed)
            if (FilteredProcesses.Count > 1)
            {
                var currentOrder = FilteredProcesses.ToList();
                bool needsReordering = false;

                for (int i = 0; i < Math.Min(currentOrder.Count, filteredList.Count); i++)
                {
                    if (currentOrder[i] != filteredList[i])
                    {
                        needsReordering = true;
                        break;
                    }
                }

                if (needsReordering)
                {
                    FilteredProcesses.Clear();
                    foreach (var item in filteredList)
                    {
                        FilteredProcesses.Add(item);
                    }
                }
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
                "SecurityHealthService.exe", "SecurityHealthSystray.exe"
            };

            return systemProcesses.Any(sp => process.Name.Equals(sp, StringComparison.OrdinalIgnoreCase)) ||
                   process.Name.StartsWith("System", StringComparison.OrdinalIgnoreCase);
        }

        private async Task LoadProcessPowerPlanAssociation(ProcessModel process)
        {
            try
            {
                await RefreshPowerPlansAsync();
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to load power plan association for process {ProcessName}", process.Name);
            }
        }

        private void ClearProcessSelection()
        {
            // Clear CPU core selections
            foreach (var core in CpuCores)
            {
                core.IsSelected = false;
            }

            // Reset power plan to current system default
            _ = Task.Run(async () =>
            {
                try
                {
                    await RefreshPowerPlansAsync();
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to reset power plan selection");
                }
            });

            // Notify UI of changes
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // Reset feature states
                IsIdleServerDisabled = false;
                IsRegistryPriorityEnabled = false;

                OnPropertyChanged(nameof(CpuCores));

                // BUG FIX: Clear status without setting busy state and auto-clear after delay
                SetStatus("Process selection cleared", false);

                // Clear the status after a short delay
                _ = Task.Delay(2000).ContinueWith(_ =>
                {
                    System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (StatusMessage == "Process selection cleared")
                        {
                            ClearStatus();
                        }
                    });
                });
            });
        }

        /// <summary>
        /// Toggles the idle server functionality for the selected process
        /// </summary>
        private async Task ToggleIdleServerAsync(bool disable)
        {
            if (SelectedProcess == null) return;

            try
            {
                SetStatus($"{(disable ? "Disabling" : "Enabling")} idle server for {SelectedProcess.Name}...");

                // Implementation for disabling/enabling idle server
                // This typically involves setting process execution state or power management settings
                var success = await _processService.SetIdleServerStateAsync(SelectedProcess, !disable);

                if (success)
                {
                    SelectedProcess.IsIdleServerDisabled = disable;
                    SetStatus($"Idle server {(disable ? "disabled" : "enabled")} for {SelectedProcess.Name}");

                    await LogUserActionAsync("IdleServer",
                        $"Idle server {(disable ? "disabled" : "enabled")} for process {SelectedProcess.Name}",
                        $"PID: {SelectedProcess.ProcessId}");
                }
                else
                {
                    SetStatus($"Failed to {(disable ? "disable" : "enable")} idle server for {SelectedProcess.Name}", false);
                    // Revert the UI state
                    IsIdleServerDisabled = !disable;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error toggling idle server for process {ProcessName}", SelectedProcess.Name);
                SetStatus($"Error: {ex.Message}", false);
                // Revert the UI state
                IsIdleServerDisabled = !disable;
            }
        }

        /// <summary>
        /// Toggles registry-based priority enforcement for the selected process
        /// </summary>
        private async Task ToggleRegistryPriorityAsync(bool enable)
        {
            if (SelectedProcess == null) return;

            try
            {
                SetStatus($"{(enable ? "Enabling" : "Disabling")} registry priority enforcement for {SelectedProcess.Name}...");

                // Implementation for registry-based priority setting
                var success = await _processService.SetRegistryPriorityAsync(SelectedProcess, enable, SelectedProcess.Priority);

                if (success)
                {
                    SelectedProcess.IsRegistryPriorityEnabled = enable;

                    if (enable)
                    {
                        SetStatus($"Registry priority enforcement enabled for {SelectedProcess.Name}. Process restart required for changes to take effect.");

                        // Show notification about restart requirement
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            System.Windows.MessageBox.Show(
                                $"Registry priority has been set for {SelectedProcess.Name}.\n\n" +
                                "The process must be restarted for the registry changes to take effect.\n\n" +
                                "This setting will persist across system reboots and will automatically apply the selected priority when the process starts.",
                                "Registry Priority Set - Restart Required",
                                System.Windows.MessageBoxButton.OK,
                                System.Windows.MessageBoxImage.Information);
                        });
                    }
                    else
                    {
                        SetStatus($"Registry priority enforcement disabled for {SelectedProcess.Name}");
                    }

                    await LogUserActionAsync("RegistryPriority",
                        $"Registry priority enforcement {(enable ? "enabled" : "disabled")} for process {SelectedProcess.Name}",
                        $"PID: {SelectedProcess.ProcessId}, Priority: {SelectedProcess.Priority}");
                }
                else
                {
                    SetStatus($"Failed to {(enable ? "enable" : "disable")} registry priority enforcement for {SelectedProcess.Name}", false);
                    // Revert the UI state
                    IsRegistryPriorityEnabled = !enable;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error toggling registry priority for process {ProcessName}", SelectedProcess.Name);
                SetStatus($"Error: {ex.Message}", false);
                // Revert the UI state
                IsRegistryPriorityEnabled = !enable;
            }
        }

        /// <summary>
        /// Saves the current process settings (affinity mask, priority, power plan) as an association
        /// Based on CPUSetSetter's SetMask pattern
        /// </summary>
        [RelayCommand]
        private async Task SaveCurrentAsAssociation()
        {
            if (SelectedProcess == null)
            {
                await _notificationService.ShowNotificationAsync("No Process Selected",
                    "Please select a process to save as an association", NotificationType.Warning);
                return;
            }

            try
            {
                SetStatus($"Saving association for {SelectedProcess.Name}...");

                // Get current power plan
                var currentPowerPlan = await _powerPlanService.GetActivePowerPlan();

                // Create new association
                var association = new ProcessPowerPlanAssociation
                {
                    ExecutableName = SelectedProcess.Name,
                    ExecutablePath = SelectedProcess.ExecutablePath ?? string.Empty,
                    PowerPlanGuid = currentPowerPlan?.Guid ?? string.Empty,
                    PowerPlanName = currentPowerPlan?.Name ?? "Unknown",
                    CoreMaskId = SelectedCoreMask?.Id,
                    CoreMaskName = SelectedCoreMask?.Name,
                    ProcessPriority = SelectedProcess.Priority.ToString(),
                    MatchByPath = !string.IsNullOrEmpty(SelectedProcess.ExecutablePath),
                    Priority = 0,
                    Description = $"Saved from Process Management on {DateTime.Now:g}",
                    IsEnabled = true
                };

                // Try to add the association
                var success = await _associationService.AddAssociationAsync(association);

                if (success)
                {
                    SetStatus($"Association saved for {SelectedProcess.Name}", false);
                    await _notificationService.ShowNotificationAsync("Association Saved",
                        $"Settings for {SelectedProcess.Name} saved successfully", NotificationType.Success);

                    await LogUserActionAsync("SaveAssociation",
                        $"Saved association for process {SelectedProcess.Name}",
                        $"PID: {SelectedProcess.ProcessId}, PowerPlan: {currentPowerPlan?.Name}, " +
                        $"CoreMask: {SelectedCoreMask?.Name ?? "None"}, Priority: {SelectedProcess.Priority}");
                }
                else
                {
                    SetStatus($"Association already exists for {SelectedProcess.Name}", false);
                    await _notificationService.ShowNotificationAsync("Association Exists",
                        $"An association for {SelectedProcess.Name} already exists", NotificationType.Warning);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error saving association for process {ProcessName}", SelectedProcess.Name);
                SetStatus($"Error saving association: {ex.Message}", false);
                await _notificationService.ShowNotificationAsync("Error",
                    $"Failed to save association: {ex.Message}", NotificationType.Error);
            }
        }
    }
}