namespace ThreadPilot
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Timers;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media.Animation;
    using System.Windows.Media.Effects;
    using System.Windows.Media.Imaging;
    using Microsoft.Extensions.DependencyInjection;
    using ThreadPilot.Helpers;
    using ThreadPilot.Services;
    using ThreadPilot.ViewModels;
    using ThreadPilot.Views;

    public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
    {
        private const int SystemTrayUpdateBaseIntervalMs = 10000;
        private const int SystemTrayUpdateMaxIntervalMs = 60000;

        private readonly ProcessViewModel processViewModel;
        private readonly PowerPlanViewModel powerPlanViewModel;
        private readonly Lazy<PerformanceViewModel> performanceViewModelFactory;
        private readonly ProcessPowerPlanAssociationViewModel associationViewModel;
        private readonly LogViewerViewModel logViewerViewModel;
        private readonly ISystemTrayService systemTrayService;
        private readonly ISystemTrayStatusUpdater systemTrayStatusUpdater;
        private readonly IApplicationSettingsService settingsService;
        private readonly INotificationService notificationService;
        private readonly IProcessMonitorService processMonitorService;
        private readonly IProcessMonitorManagerService processMonitorManagerService;
        private readonly IProcessPowerPlanAssociationService processPowerPlanAssociationService;
        private readonly SettingsViewModel settingsViewModel;
        private readonly MainWindowViewModel mainWindowViewModel;
        private readonly SystemTweaksViewModel systemTweaksViewModel;
        private readonly ISelfResourceManagementService selfResourceManagementService;
        private readonly IKeyboardShortcutService keyboardShortcutService;
        private readonly IServiceProvider serviceProvider;
        private readonly IThemeService themeService;
        private System.Timers.Timer? systemTrayUpdateTimer;
        private PerformanceViewModel? performanceViewModel;
        private bool isSystemTrayUpdatesSuspended;
        private int isSystemTrayUpdateInProgress;
        private int systemTrayUpdateFailureStreak;
        private int startupUpdateCheckStarted;
        private AppActivityState? lastAppliedRefreshState;
        private readonly IElevationService elevationService;
        private readonly ISecurityService securityService;

        // Loading overlay management
        private bool isInitializationComplete = false;
        private readonly List<string> initializationTasks = new();
        private readonly object initializationLock = new();
        private System.Timers.Timer? initializationTimeoutTimer;
        private readonly string debugLogPath = Path.Combine(Path.GetTempPath(), "ThreadPilot_Debug.log");

        // Flag to skip process monitoring during startup if it causes issues
        private readonly bool skipProcessMonitoringDuringStartup = false;
        private bool isPerformingShutdown = false;
        private readonly NavigationBehavior navigationBehavior = new();
        private bool isPerformanceIntroVisible = false;
        private double previousAppContentOpacity = 1;
        private TaskCompletionSource<MessageBoxResult>? unsavedSettingsDialogCompletionSource;
        private bool isSilentStartupMode;
        private bool showStartupMinimizedSuggestionOnReady;

        public MainWindow(
            ProcessViewModel processViewModel,
            PowerPlanViewModel powerPlanViewModel,
            Lazy<PerformanceViewModel> performanceViewModelFactory,
            ProcessPowerPlanAssociationViewModel associationViewModel,
            LogViewerViewModel logViewerViewModel,
            ISystemTrayService systemTrayService,
            ISystemTrayStatusUpdater systemTrayStatusUpdater,
            IApplicationSettingsService settingsService,
            INotificationService notificationService,
            IProcessMonitorService processMonitorService,
            IProcessMonitorManagerService processMonitorManagerService,
            IProcessPowerPlanAssociationService processPowerPlanAssociationService,
            SettingsViewModel settingsViewModel,
            MainWindowViewModel mainWindowViewModel,
            SystemTweaksViewModel systemTweaksViewModel,
            ISelfResourceManagementService selfResourceManagementService,
            IKeyboardShortcutService keyboardShortcutService,
            IThemeService themeService,
            IServiceProvider serviceProvider,
            IElevationService elevationService,
            ISecurityService securityService)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("MainWindow constructor starting...");

                this.InitializeComponent();
                System.Diagnostics.Debug.WriteLine("InitializeComponent completed");
                this.ConfigureDiagnosticsNavigation();

                // Initialize loading overlay
                this.InitializeLoadingOverlay();
                this.LogDebug("Loading overlay initialized");
                this.LogDebug($"Debug log file: {this.debugLogPath}");

                this.processViewModel = processViewModel;
                this.powerPlanViewModel = powerPlanViewModel;
                this.performanceViewModelFactory = performanceViewModelFactory;
                this.associationViewModel = associationViewModel;
                this.logViewerViewModel = logViewerViewModel;
                this.systemTrayService = systemTrayService;
                this.systemTrayStatusUpdater = systemTrayStatusUpdater;
                this.settingsService = settingsService;
                this.notificationService = notificationService;
                this.processMonitorService = processMonitorService;
                this.processMonitorManagerService = processMonitorManagerService;
                this.processPowerPlanAssociationService = processPowerPlanAssociationService;
                this.settingsViewModel = settingsViewModel;
                this.mainWindowViewModel = mainWindowViewModel;
                this.systemTweaksViewModel = systemTweaksViewModel;
                this.selfResourceManagementService = selfResourceManagementService;
                this.keyboardShortcutService = keyboardShortcutService;
                this.themeService = themeService;
                this.serviceProvider = serviceProvider;
                this.elevationService = elevationService;
                this.securityService = securityService;

                this.processViewModel.OpenRulesRequested += this.OnOpenRulesRequested;

                System.Diagnostics.Debug.WriteLine("Dependencies assigned");

                this.SetDataContexts();
                System.Diagnostics.Debug.WriteLine("DataContexts set");

                this.UpdateLoadingStatus("Starting ThreadPilot...", "Preparing startup sequence.");

                // Start async initialization - marshal to UI thread to prevent cross-thread access exceptions
                _ = this.Dispatcher.InvokeAsync(async () => await this.InitializeApplicationAsync());
                System.Diagnostics.Debug.WriteLine("Async initialization started");
                System.Diagnostics.Debug.WriteLine("MainWindow constructor completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in MainWindow constructor: {ex}");
                System.Windows.MessageBox.Show(
                    $"Error initializing MainWindow:\n\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}",
                    "MainWindow Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        public void ConfigureStartupMode(bool isSilentStartupMode, bool showStartupMinimizedSuggestionOnReady)
        {
            this.isSilentStartupMode = isSilentStartupMode;
            this.showStartupMinimizedSuggestionOnReady = showStartupMinimizedSuggestionOnReady;

            if (!isSilentStartupMode)
            {
                return;
            }

            this.showStartupMinimizedSuggestionOnReady = false;
            this.LoadingOverlay.Visibility = Visibility.Collapsed;
            this.ClearUIContentBlur();

            if (this.FindResource("SpinnerAnimation") is Storyboard spinnerAnimation)
            {
                spinnerAnimation.Stop();
            }

            this.isSystemTrayUpdatesSuspended = true;
            this.lastAppliedRefreshState = AppActivityState.TrayHidden;
            this.processViewModel.SetProcessViewActive(false);
            this.processViewModel.ApplyRefreshDecision(AppRefreshPolicy.Evaluate(AppActivityState.TrayHidden));
            this.powerPlanViewModel.PauseAutoRefresh();
        }

        private void ConfigureDiagnosticsNavigation()
        {
            this.NavPerf.Visibility = AppNavigationOptions.ShowAdvancedDiagnostics
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private PerformanceViewModel GetPerformanceViewModel()
        {
            if (this.performanceViewModel != null)
            {
                return this.performanceViewModel;
            }

            this.performanceViewModel = this.performanceViewModelFactory.Value;
            this.PerformanceViewControl.DataContext = this.performanceViewModel;
            return this.performanceViewModel;
        }
    }
}
