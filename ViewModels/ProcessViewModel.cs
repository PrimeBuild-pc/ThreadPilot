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
        private readonly ProcessFilterService processFilterService;
        private readonly IVirtualizedProcessService virtualizedProcessService;
        private readonly ICpuTopologyService cpuTopologyService;
        private readonly IPowerPlanService powerPlanService;
        private readonly INotificationService notificationService;
        private readonly ISystemTrayService systemTrayService;
        private readonly ICoreMaskService coreMaskService;
        private readonly IProcessPowerPlanAssociationService associationService;
        private readonly IGameModeService gameModeService;
        private System.Timers.Timer? refreshTimer;
        private readonly ThrottledRefreshCoordinator searchRefreshCoordinator;
        private readonly ThrottledRefreshCoordinator filterRefreshCoordinator;
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
            ProcessFilterService processFilterService,
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
            this.processFilterService = processFilterService ?? throw new ArgumentNullException(nameof(processFilterService));
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

            this.searchRefreshCoordinator = new ThrottledRefreshCoordinator(
                TimeSpan.FromMilliseconds(300),
                this.ApplyFiltersOnUiAsync,
                ex => this.Logger.LogWarning(ex, "Search filter refresh failed"));

            this.filterRefreshCoordinator = new ThrottledRefreshCoordinator(
                TimeSpan.FromMilliseconds(100),
                this.ApplyFiltersOnUiAsync,
                ex => this.Logger.LogWarning(ex, "Filter refresh failed"));

            this.SetupRefreshTimer();
            this.SetupVirtualizedProcessService();
            // Note: InitializeAsync() will be called explicitly by MainWindow loading overlay
        }
    }
}

