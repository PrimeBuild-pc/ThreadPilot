namespace ThreadPilot.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows.Input;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
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
        private readonly IAffinityApplyService affinityApplyService;
        private readonly IProcessAffinityApplyCoordinator processAffinityApplyCoordinator;
        private readonly IProcessMemoryPriorityService? memoryPriorityService;
        private readonly IProcessRuleCreationService? processRuleCreationService;
        private readonly Action<string> clipboardSetter;
        private readonly Action<string> executableLocationOpener;
        private System.Timers.Timer? refreshTimer;
        private bool isUiRefreshPaused;
        private bool isProcessViewActive = true;
        private bool isVirtualizedPreloadAllowedByPolicy = true;
        private int isRefreshProcessesInProgress;
        private readonly ThrottledRefreshCoordinator searchRefreshCoordinator;
        private readonly ThrottledRefreshCoordinator filterRefreshCoordinator;
        private bool isApplyingFilter;
        private bool filterRefreshPending;
        private bool suppressCoreSelectionEvents;

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
        private bool hasPendingAffinityEdits;

        [ObservableProperty]
        private string currentAffinityText = "Current OS affinity: no process selected";

        [ObservableProperty]
        private string pendingAffinityText = "Pending core mask: none";

        [ObservableProperty]
        private string affinityEditStateText = "Select a process to view its current Windows affinity.";

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

        [ObservableProperty]
        private bool isProcessListLocked = false;

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
            IAffinityApplyService? affinityApplyService = null,
            IProcessAffinityApplyCoordinator? processAffinityApplyCoordinator = null,
            ICpuTopologyProvider? cpuTopologyProvider = null,
            IEnhancedLoggingService? enhancedLoggingService = null,
            IActivityAuditService? activityAuditService = null,
            IProcessMemoryPriorityService? memoryPriorityService = null,
            IPersistentProcessRuleStore? persistentRuleStore = null,
            IPersistentProcessRuleMatcher? persistentRuleMatcher = null,
            IProcessRuleCreationService? processRuleCreationService = null,
            Action<string>? clipboardSetter = null,
            Action<string>? executableLocationOpener = null,
            ILocalizationService? localizationService = null)
            : base(logger, enhancedLoggingService, activityAuditService)
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
            this.affinityApplyService = affinityApplyService ?? new AffinityApplyService(
                this.processService,
                this.cpuTopologyService,
                NullLogger<AffinityApplyService>.Instance);
            this.processAffinityApplyCoordinator = processAffinityApplyCoordinator ?? new ProcessAffinityApplyCoordinator(
                this.affinityApplyService,
                cpuTopologyProvider,
                new CpuSelectionMigrationService(),
                NullLogger<ProcessAffinityApplyCoordinator>.Instance);
            this.memoryPriorityService = memoryPriorityService;
            this.processRuleCreationService = processRuleCreationService ?? (persistentRuleStore == null
                ? null
                : new ProcessRuleCreationService(
                    persistentRuleStore,
                    cpuTopologyProvider,
                    new CpuSelectionMigrationService(),
                    NullLogger<ProcessRuleCreationService>.Instance));
            this.clipboardSetter = clipboardSetter ?? System.Windows.Clipboard.SetText;
            this.executableLocationOpener = executableLocationOpener ?? OpenExecutableLocationInExplorer;
            this.SelectedProcessSummary = new SelectedProcessSummaryViewModel(
                memoryPriorityService,
                persistentRuleStore,
                persistentRuleMatcher,
                localizationService);

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

        public IReadOnlyList<ProcessPriorityClass> ContextMenuCpuPriorityActions { get; } =
        [
            ProcessPriorityClass.BelowNormal,
            ProcessPriorityClass.Normal,
            ProcessPriorityClass.AboveNormal,
            ProcessPriorityClass.High,
        ];

        public SelectedProcessSummaryViewModel SelectedProcessSummary { get; }

        private static void OpenExecutableLocationInExplorer(string executablePath)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{executablePath}\"",
                UseShellExecute = true,
            };

            Process.Start(startInfo);
        }
    }
}
