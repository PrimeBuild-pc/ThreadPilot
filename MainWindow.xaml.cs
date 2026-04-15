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
        private readonly PerformanceViewModel performanceViewModel;
        private readonly ProcessPowerPlanAssociationViewModel associationViewModel;
        private readonly LogViewerViewModel logViewerViewModel;
        private readonly ISystemTrayService systemTrayService;
        private readonly IApplicationSettingsService settingsService;
        private readonly INotificationService notificationService;
        private readonly IProcessMonitorService processMonitorService;
        private readonly IProcessMonitorManagerService processMonitorManagerService;
        private readonly IProcessPowerPlanAssociationService processPowerPlanAssociationService;
        private readonly SettingsViewModel settingsViewModel;
        private readonly MainWindowViewModel mainWindowViewModel;
        private readonly SystemTweaksViewModel systemTweaksViewModel;
        private readonly IKeyboardShortcutService keyboardShortcutService;
        private readonly IServiceProvider serviceProvider;
        private readonly IThemeService themeService;
        private System.Timers.Timer? systemTrayUpdateTimer;
        private bool isSystemTrayUpdatesSuspended;
        private int isSystemTrayUpdateInProgress;
        private int systemTrayUpdateFailureStreak;
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

        public MainWindow(
            ProcessViewModel processViewModel,
            PowerPlanViewModel powerPlanViewModel,
            PerformanceViewModel performanceViewModel,
            ProcessPowerPlanAssociationViewModel associationViewModel,
            LogViewerViewModel logViewerViewModel,
            ISystemTrayService systemTrayService,
            IApplicationSettingsService settingsService,
            INotificationService notificationService,
            IProcessMonitorService processMonitorService,
            IProcessMonitorManagerService processMonitorManagerService,
            IProcessPowerPlanAssociationService processPowerPlanAssociationService,
            SettingsViewModel settingsViewModel,
            MainWindowViewModel mainWindowViewModel,
            SystemTweaksViewModel systemTweaksViewModel,
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

                // Initialize loading overlay
                this.InitializeLoadingOverlay();
                this.LogDebug("Loading overlay initialized");
                this.LogDebug($"Debug log file: {this.debugLogPath}");

                this.processViewModel = processViewModel;
                this.powerPlanViewModel = powerPlanViewModel;
                this.performanceViewModel = performanceViewModel;
                this.associationViewModel = associationViewModel;
                this.logViewerViewModel = logViewerViewModel;
                this.systemTrayService = systemTrayService;
                this.settingsService = settingsService;
                this.notificationService = notificationService;
                this.processMonitorService = processMonitorService;
                this.processMonitorManagerService = processMonitorManagerService;
                this.processPowerPlanAssociationService = processPowerPlanAssociationService;
                this.settingsViewModel = settingsViewModel;
                this.mainWindowViewModel = mainWindowViewModel;
                this.systemTweaksViewModel = systemTweaksViewModel;
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

        private void SetDataContexts()
        {
            // Set DataContext for the main window
            this.DataContext = this.mainWindowViewModel;

            // Set DataContext for the power plans view
            this.PowerPlanViewControl.DataContext = this.powerPlanViewModel;

            // Set DataContext for the association view
            this.AssociationView.DataContext = this.associationViewModel;

            // Set DataContext for the performance view
            this.PerformanceViewControl.DataContext = this.performanceViewModel;

            // Set DataContext for the log viewer view
            this.LogViewerViewControl.DataContext = this.logViewerViewModel;

            // Set DataContext for the system tweaks view
            this.SystemTweaksView.DataContext = this.systemTweaksViewModel;

            // Set DataContext for the settings view
            this.SettingsView.DataContext = this.settingsViewModel;
        }

        private void InitializeLoadingOverlay()
        {
            try
            {
                var loadingOverlay = this.FindName("LoadingOverlay") as Grid;

                // Ensure overlay is visible while initialization runs
                if (loadingOverlay != null)
                {
                    loadingOverlay.Visibility = Visibility.Visible;
                    loadingOverlay.Opacity = 1;
                }

                // Enable blur on main app content during startup loading.
                this.ApplyUIContentBlur(15);

                // Start spinner animation if available
                var spinnerAnimation = this.FindResource("SpinnerAnimation") as Storyboard;
                spinnerAnimation?.Begin();

                // Set a timeout guard for initialization
                this.initializationTimeoutTimer = new System.Timers.Timer(15000)
                {
                    AutoReset = false,
                };
                this.initializationTimeoutTimer.Elapsed += this.OnInitializationTimeout;
                this.initializationTimeoutTimer.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize loading overlay: {ex.Message}");
            }
        }

        private async Task InitializeApplicationAsync()
        {
            try
            {
                this.LogDebug("=== Starting InitializeApplicationAsync ===");

                await this.Dispatcher.InvokeAsync(() => this.UpdateLoadingStatus("Loading view models...", "Loading process, power plan and rules data."));
                this.LogDebug("About to call LoadViewModelsAsync...");
                await this.LoadViewModelsAsync();
                this.LogDebug("LoadViewModelsAsync completed successfully");
                this.CompleteInitializationTask("ViewModels");

                this.LogDebug("About to initialize MainWindowViewModel...");
                await this.mainWindowViewModel.InitializeAsync();
                this.LogDebug("MainWindowViewModel initialized successfully");
                this.CompleteInitializationTask("MainWindowViewModel");

                await this.Dispatcher.InvokeAsync(() => this.UpdateLoadingStatus("Initializing services...", "Starting monitoring, tray and notification services."));
                this.LogDebug("About to call InitializeServicesAsync...");
                await this.InitializeServicesAsync();
                this.LogDebug("InitializeServicesAsync completed successfully");
                this.CompleteInitializationTask("Services");

                await this.Dispatcher.InvokeAsync(() => this.UpdateLoadingStatus("Finalizing startup...", "Applying final UI state and startup checks."));
                this.LogDebug("Finalizing startup...");
                await Task.Delay(500); // Brief delay to show final status
                this.CompleteInitializationTask("Finalization");

                // All initialization complete
                this.LogDebug("All initialization complete, hiding overlay...");
                await this.Dispatcher.InvokeAsync(() => this.HideLoadingOverlay());
                this.LogDebug("=== InitializeApplicationAsync completed successfully ===");
            }
            catch (Exception ex)
            {
                this.LogDebug($"=== ERROR in InitializeApplicationAsync: {ex} ===");
                await this.Dispatcher.InvokeAsync(() => this.ShowInitializationError(ex));
            }
        }

        private void UpdateLoadingStatus(string stage, string details = "")
        {
            if (this.mainWindowViewModel != null)
            {
                this.mainWindowViewModel.InitializationStage = stage;
                this.mainWindowViewModel.InitializationDetails = details;
            }
        }

        private void CompleteInitializationTask(string taskName)
        {
            lock (this.initializationLock)
            {
                this.initializationTasks.Add(taskName);
                System.Diagnostics.Debug.WriteLine($"Initialization task completed: {taskName}");
            }
        }

        private void HideLoadingOverlay()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== Starting HideLoadingOverlay ===");
                this.isInitializationComplete = true;
                this.initializationTimeoutTimer?.Stop();
                this.initializationTimeoutTimer?.Dispose();

                // Stop spinner animation
                var spinnerAnimation = this.FindResource("SpinnerAnimation") as Storyboard;
                spinnerAnimation?.Stop();
                System.Diagnostics.Debug.WriteLine("Spinner animation stopped");

                // Start fade-out animation
                var fadeOutAnimation = this.FindResource("FadeOutAnimation") as Storyboard;
                if (fadeOutAnimation != null)
                {
                    System.Diagnostics.Debug.WriteLine("Starting fade-out animation");
                    fadeOutAnimation.Completed += (s, e) =>
                    {
                        System.Diagnostics.Debug.WriteLine("Fade-out animation completed, hiding overlay");
                        var loadingOverlay = this.FindName("LoadingOverlay") as Grid;
                        if (loadingOverlay != null)
                        {
                            loadingOverlay.Visibility = Visibility.Collapsed;
                            System.Diagnostics.Debug.WriteLine("Loading overlay visibility set to Collapsed");
                        }

                        // Disable app content blur and restore style-driven behavior.
                        this.ClearUIContentBlur();
                        System.Diagnostics.Debug.WriteLine("=== Loading overlay hidden successfully ===");

                        // Show elevation warning if needed
                        this.TryShowElevationWarning();
                    };
                    fadeOutAnimation.Begin();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("WARNING: FadeOutAnimation not found, hiding overlay immediately");
                    // Fallback: hide overlay immediately if animation fails
                    var loadingOverlay = this.FindName("LoadingOverlay") as Grid;
                    if (loadingOverlay != null)
                    {
                        loadingOverlay.Visibility = Visibility.Collapsed;
                    }

                    this.ClearUIContentBlur();
                    System.Diagnostics.Debug.WriteLine("=== Loading overlay hidden immediately (fallback) ===");

                    // Show elevation warning if needed
                    this.TryShowElevationWarning();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"=== ERROR hiding loading overlay: {ex} ===");
                // Emergency fallback: hide overlay without animation
                try
                {
                    var loadingOverlay = this.FindName("LoadingOverlay") as Grid;
                    if (loadingOverlay != null)
                    {
                        loadingOverlay.Visibility = Visibility.Collapsed;
                    }

                    this.ClearUIContentBlur();
                    System.Diagnostics.Debug.WriteLine("Emergency fallback: overlay hidden without animation");

                    // Show elevation warning if needed
                    this.TryShowElevationWarning();
                }
                catch (Exception fallbackEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Emergency fallback also failed: {fallbackEx}");
                }
            }
        }

        private void ApplyUIContentBlur(double radius)
        {
            if (this.UIContent.Effect is not BlurEffect blur)
            {
                blur = new BlurEffect();
                this.UIContent.Effect = blur;
            }

            blur.KernelType = KernelType.Gaussian;
            blur.Radius = radius;
        }

        private void ClearUIContentBlur()
        {
            this.UIContent.Effect = null;
        }

        private void OnInitializationTimeout(object? sender, ElapsedEventArgs e)
        {
            this.Dispatcher.InvokeAsync(() =>
            {
                if (!this.isInitializationComplete)
                {
                    this.ShowInitializationError(new TimeoutException("Application initialization timed out after 15 seconds"));
                }
            });
        }

        private void ShowInitializationError(Exception ex)
        {
            try
            {
                this.UpdateLoadingStatus("Initialization failed", ex.Message);

                var result = System.Windows.MessageBox.Show(
                    $"ThreadPilot failed to initialize properly:\n\n{ex.Message}\n\nDebug log: {this.debugLogPath}\n\nWould you like to retry initialization or close the application?",
                    "Initialization Error",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Error);

                if (result == MessageBoxResult.Yes)
                {
                    // Retry initialization - marshal to UI thread to prevent cross-thread access exceptions
                    this.isInitializationComplete = false;
                    this.initializationTasks.Clear();
                    this.UpdateLoadingStatus("Retrying initialization...", "Restarting startup sequence.");
                    this.LogDebug("=== RETRYING INITIALIZATION ===");
                    _ = this.Dispatcher.InvokeAsync(async () => await this.InitializeApplicationAsync());
                }
                else
                {
                    // Close application
                    this.LogDebug("User chose to close application");
                    System.Windows.Application.Current.Shutdown();
                }
            }
            catch (Exception overlayEx)
            {
                this.LogDebug($"Error showing initialization error: {overlayEx.Message}");
                System.Windows.Application.Current.Shutdown();
            }
        }

        private async Task LoadViewModelsAsync()
        {
            try
            {
                this.LogDebug("=== Starting LoadViewModelsAsync ===");

                this.LogDebug("About to initialize ProcessViewModel (including CPU topology)...");
                try
                {
                    // Use the full initialization method instead of just LoadProcesses
                    var processTask = this.processViewModel.InitializeAsync();
                    var processResult = await Task.WhenAny(processTask, Task.Delay(15000)); // 15 second timeout for full initialization
                    if (processResult != processTask)
                    {
                        this.LogDebug("ProcessViewModel.InitializeAsync() timed out, trying fallback...");
                        // Fallback: just load processes without full initialization
                        await this.processViewModel.LoadProcesses();
                        this.LogDebug($"ProcessViewModel fallback (LoadProcesses only) completed, process count: {this.processViewModel.Processes?.Count ?? 0}, filtered count: {this.processViewModel.FilteredProcesses?.Count ?? 0}");
                    }
                    else
                    {
                        await processTask; // Ensure we get any exceptions
                        this.LogDebug($"ProcessViewModel initialized successfully (including CPU topology), process count: {this.processViewModel.Processes?.Count ?? 0}, filtered count: {this.processViewModel.FilteredProcesses?.Count ?? 0}");
                    }
                }
                catch (Exception processEx)
                {
                    this.LogDebug($"ProcessViewModel initialization failed: {processEx.Message}, trying fallback...");
                    // Fallback: just load processes without full initialization
                    await this.processViewModel.LoadProcesses();
                    this.LogDebug($"ProcessViewModel fallback (LoadProcesses only) completed after exception, process count: {this.processViewModel.Processes?.Count ?? 0}, filtered count: {this.processViewModel.FilteredProcesses?.Count ?? 0}");
                }

                this.LogDebug("About to load PowerPlanViewModel...");
                var powerPlanTask = this.powerPlanViewModel.LoadPowerPlans();
                var powerPlanResult = await Task.WhenAny(powerPlanTask, Task.Delay(5000)); // 5 second timeout
                if (powerPlanResult != powerPlanTask)
                {
                    throw new TimeoutException("PowerPlanViewModel.LoadPowerPlans() timed out after 5 seconds");
                }
                await powerPlanTask; // Ensure we get any exceptions
                this.LogDebug("PowerPlanViewModel loaded successfully");

                this.LogDebug("About to initialize PerformanceViewModel...");
                var performanceTask = this.performanceViewModel.InitializeAsync();
                var performanceResult = await Task.WhenAny(performanceTask, Task.Delay(5000));
                if (performanceResult != performanceTask)
                {
                    throw new TimeoutException("PerformanceViewModel.InitializeAsync() timed out after 5 seconds");
                }
                await performanceTask; // Ensure we get any exceptions
                this.LogDebug("PerformanceViewModel initialized successfully");

                this.LogDebug("About to load SystemTweaksViewModel...");
                var systemTweaksTask = this.systemTweaksViewModel.LoadCommand.ExecuteAsync(null);
                var systemTweaksResult = await Task.WhenAny(systemTweaksTask, Task.Delay(5000)); // 5 second timeout
                if (systemTweaksResult != systemTweaksTask)
                {
                    throw new TimeoutException("SystemTweaksViewModel.LoadCommand.ExecuteAsync() timed out after 5 seconds");
                }
                await systemTweaksTask; // Ensure we get any exceptions
                this.LogDebug("SystemTweaksViewModel loaded successfully");

                // Initialize keyboard shortcuts after window is loaded
                this.Loaded += this.OnWindowLoaded;
                this.LogDebug("Keyboard shortcuts event handler attached");

                // The association view model loads its data automatically in its constructor
                this.LogDebug("=== LoadViewModelsAsync completed successfully ===");
            }
            catch (Exception ex)
            {
                this.LogDebug($"=== ERROR in LoadViewModelsAsync: {ex} ===");
                throw; // Re-throw to be handled by initialization error handler
            }
        }

        private async Task InitializeServicesAsync()
        {
            this.LogDebug("=== Starting InitializeServicesAsync ===");

            this.LogDebug("About to initialize settings...");
            await this.InitializeSettingsAsync();
            this.LogDebug("Settings initialized successfully");

            this.LogDebug("About to initialize system tray...");
            try
            {
                var systemTrayTask = this.InitializeSystemTrayAsync();
                var systemTrayResult = await Task.WhenAny(systemTrayTask, Task.Delay(5000)); // 5 second timeout
                if (systemTrayResult != systemTrayTask)
                {
                    this.LogDebug("System tray initialization timed out, continuing with basic tray setup...");
                    // Initialize basic system tray without context menu updates (Initialize() is idempotent)
                    await this.InitializeBasicSystemTrayAsync();
                    this.LogDebug("Basic system tray initialized (without context menu)");
                }
                else
                {
                    await systemTrayTask; // Ensure we get any exceptions
                    this.LogDebug("System tray initialized successfully");
                }
            }
            catch (Exception systemTrayEx)
            {
                this.LogDebug($"System tray initialization failed: {systemTrayEx.Message}, using basic tray...");
                // Fallback: basic system tray initialization
                try
                {
                    await this.InitializeBasicSystemTrayAsync();
                    this.LogDebug("Fallback system tray initialized");
                }
                catch (Exception fallbackEx)
                {
                    this.LogDebug($"Even fallback system tray failed: {fallbackEx.Message}");
                }
            }

            this.LogDebug("About to initialize notifications...");
            this.InitializeNotifications();
            this.LogDebug("Notifications initialized successfully");

            this.LogDebug("About to initialize monitoring...");
            await this.InitializeMonitoringAsync();
            this.LogDebug("Monitoring initialized successfully");

            if (this.skipProcessMonitoringDuringStartup)
            {
                this.LogDebug("Skipping process monitoring manager startup (temporary bypass enabled)");
            }
            else
            {
                this.LogDebug("About to start process monitoring manager...");
                try
                {
                    var monitoringTask = this.StartProcessMonitoringManagerAsync();
                    var timeoutTask = Task.Delay(8000); // 8 second timeout
                    var completedTask = await Task.WhenAny(monitoringTask, timeoutTask);

                    if (completedTask == timeoutTask)
                    {
                        this.LogDebug("Process monitoring manager startup timed out after 8 seconds, continuing without monitoring...");
                    }
                    else
                    {
                        try
                        {
                            await monitoringTask; // Ensure we get any exceptions
                            this.LogDebug("Process monitoring manager started successfully");
                        }
                        catch (Exception taskEx)
                        {
                            this.LogDebug($"Process monitoring manager task failed: {taskEx.Message}");
                        }
                    }
                }
                catch (Exception monitoringEx)
                {
                    this.LogDebug($"Process monitoring manager startup failed: {monitoringEx.Message}, continuing without monitoring...");
                }
            }

            this.LogDebug("=== InitializeServicesAsync completed successfully ===");
        }

        private async Task InitializeSettingsAsync()
        {
            try
            {
                await this.settingsService.LoadSettingsAsync();

                // Apply initial settings
                var settings = this.settingsService.Settings;
                var useDarkTheme = settings.HasUserThemePreference
                    ? settings.UseDarkTheme
                    : this.themeService.GetSystemUsesDarkTheme();

                if (!settings.HasUserThemePreference && settings.UseDarkTheme != useDarkTheme)
                {
                    settings.UseDarkTheme = useDarkTheme;
                    await this.settingsService.UpdateSettingsAsync(settings);
                }

                this.themeService.ApplyTheme(useDarkTheme);
                this.mainWindowViewModel.IsDarkTheme = useDarkTheme;
                DwmHelper.ApplyWindowCaptionTheme(this, useDarkTheme);

                if (settings.StartMinimized)
                {
                    this.WindowState = WindowState.Minimized;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
            }
        }

        private async Task InitializeSystemTrayAsync()
        {
            try
            {
                this.systemTrayService.Initialize();
                this.systemTrayService.Show();

                // Subscribe to tray events
                this.UnsubscribeSystemTrayEvents();
                this.systemTrayService.ShowMainWindowRequested += this.OnShowMainWindowRequested;
                this.systemTrayService.DashboardRequested += this.OnDashboardRequested;
                this.systemTrayService.ExitRequested += this.OnExitRequested;
                this.systemTrayService.MonitoringToggleRequested += this.OnMonitoringToggleRequested;
                this.systemTrayService.SettingsRequested += this.OnSettingsRequested;
                this.systemTrayService.PowerPlanChangeRequested += this.OnPowerPlanChangeRequested;
                this.systemTrayService.ProfileApplicationRequested += this.OnProfileApplicationRequested;
                this.systemTrayService.PerformanceDashboardRequested += this.OnPerformanceDashboardRequested;

                // Update settings and tooltip
                this.systemTrayService.UpdateSettings(this.settingsService.Settings);
                this.systemTrayService.ApplyTheme(this.themeService.IsDarkTheme);
                this.systemTrayService.UpdateTooltip("ThreadPilot - Process & Power Plan Manager");

                // Initialize system tray context menu with current data
                await this.UpdateSystemTrayContextMenuAsync();

                // Start periodic system tray updates
                this.StartSystemTrayUpdateTimer();
            }
            catch (Exception ex)
            {
                // Log error but don't fail startup
                System.Diagnostics.Debug.WriteLine($"Failed to initialize system tray: {ex.Message}");
            }
        }

        private async Task InitializeBasicSystemTrayAsync()
        {
            try
            {
                this.LogDebug("Initializing basic system tray (without full context menu)...");

                // Initialize basic tray icon (this is idempotent)
                this.systemTrayService.Initialize();
                this.systemTrayService.Show();

                // Subscribe to essential tray events only
                this.UnsubscribeSystemTrayEvents();
                this.systemTrayService.ShowMainWindowRequested += this.OnShowMainWindowRequested;
                this.systemTrayService.DashboardRequested += this.OnDashboardRequested;
                this.systemTrayService.ExitRequested += this.OnExitRequested;

                // Update basic settings and tooltip
                this.systemTrayService.UpdateSettings(this.settingsService.Settings);
                this.systemTrayService.ApplyTheme(this.themeService.IsDarkTheme);
                this.systemTrayService.UpdateTooltip("ThreadPilot - Process & Power Plan Manager (Basic Mode)");

                this.LogDebug("Basic system tray initialization completed");
            }
            catch (Exception ex)
            {
                this.LogDebug($"Failed to initialize basic system tray: {ex.Message}");
                throw;
            }
        }

        private void OnShowMainWindowRequested(object? sender, EventArgs e)
        {
            this.ShowWindowFromTray();
        }

        private void OnExitRequested(object? sender, EventArgs e)
        {
            TaskSafety.FireAndForget(this.OnExitRequestedAsync(), ex =>
            {
                this.LogDebug($"OnExitRequested failed: {ex.Message}");
            });
        }

        private async Task OnExitRequestedAsync()
        {
            await this.PerformGracefulShutdownAsync();
        }

        private void OnDashboardRequested(object? sender, EventArgs e)
        {
            this.ShowWindowFromTray("Process");
        }

        /// <summary>
        /// Performs graceful shutdown with cleanup of all applied optimizations
        /// Similar to CPU Set Setter's ExitAppGracefully.
        /// </summary>
        private async Task PerformGracefulShutdownAsync(bool validateUnsavedChanges = true)
        {
            if (this.isPerformingShutdown)
            {
                return;
            }

            if (validateUnsavedChanges && !await this.HandleUnsavedSettingsBeforeExitAsync())
            {
                return;
            }

            this.isPerformingShutdown = true;

            try
            {
                this.LogDebug("Starting graceful shutdown...");

                // 1. Stop monitoring services
                try
                {
                    this.LogDebug("Stopping process monitoring manager...");
                    await this.processMonitorManagerService.StopAsync();
                    this.LogDebug("Process monitoring manager stopped");
                }
                catch (Exception ex)
                {
                    this.LogDebug($"Error stopping process monitoring: {ex.Message}");
                }

                // 2. Cleanup applied CPU masks (like CPU Set Setter's ClearAllProcessMasksNoSave)
                if (this.settingsService.Settings.ClearMasksOnClose)
                {
                    try
                    {
                        this.LogDebug("Clearing all applied CPU masks...");
                        var processService = this.serviceProvider.GetRequiredService<IProcessService>();
                        await processService.ClearAllAppliedMasksAsync();
                        this.LogDebug("CPU masks cleared");
                    }
                    catch (Exception ex)
                    {
                        this.LogDebug($"Error clearing CPU masks: {ex.Message}");
                    }

                    // Also reset priorities
                    try
                    {
                        this.LogDebug("Resetting all process priorities...");
                        var processService = this.serviceProvider.GetRequiredService<IProcessService>();
                        await processService.ResetAllProcessPrioritiesAsync();
                        this.LogDebug("Process priorities reset");
                    }
                    catch (Exception ex)
                    {
                        this.LogDebug($"Error resetting priorities: {ex.Message}");
                    }
                }

                // 3. Restore default power plan if configured
                if (this.settingsService.Settings.RestoreDefaultPowerPlanOnExit)
                {
                    try
                    {
                        var targetDefaultPowerPlanGuid = this.settingsService.Settings.DefaultPowerPlanId;

                        try
                        {
                            await this.processPowerPlanAssociationService.LoadConfigurationAsync();
                            var (associationDefaultPowerPlanGuid, _) = await this.processPowerPlanAssociationService.GetDefaultPowerPlanAsync();
                            if (!string.IsNullOrWhiteSpace(associationDefaultPowerPlanGuid))
                            {
                                targetDefaultPowerPlanGuid = associationDefaultPowerPlanGuid;
                            }
                        }
                        catch (Exception associationEx)
                        {
                            this.LogDebug($"Could not read default power plan from association config: {associationEx.Message}");
                        }

                        if (string.IsNullOrWhiteSpace(targetDefaultPowerPlanGuid))
                        {
                            this.LogDebug("No default power plan configured for restore on exit");
                        }
                        else
                        {
                            this.LogDebug("Restoring default power plan...");
                            var powerPlanService = this.serviceProvider.GetRequiredService<IPowerPlanService>();
                            await powerPlanService.SetActivePowerPlanByGuidAsync(targetDefaultPowerPlanGuid);
                            this.LogDebug("Default power plan restored");
                        }
                    }
                    catch (Exception ex)
                    {
                        this.LogDebug($"Error restoring power plan: {ex.Message}");
                    }
                }

                // 4. Save settings
                try
                {
                    this.LogDebug("Saving settings...");
                    await this.settingsService.SaveSettingsAsync();
                    this.LogDebug("Settings saved");
                }
                catch (Exception ex)
                {
                    this.LogDebug($"Error saving settings: {ex.Message}");
                }

                // 5. Dispose tray service
                try
                {
                    this.LogDebug("Disposing system tray...");
                    this.systemTrayService.Dispose();
                    this.LogDebug("System tray disposed");
                }
                catch (Exception ex)
                {
                    this.LogDebug($"Error disposing tray: {ex.Message}");
                }

                this.LogDebug("Graceful shutdown completed");
            }
            catch (Exception ex)
            {
                this.LogDebug($"Error during graceful shutdown: {ex.Message}");
            }
            finally
            {
                // Ensure application exits
                System.Windows.Application.Current.Shutdown();
            }
        }

        private async Task<bool> HandleUnsavedSettingsBeforeExitAsync()
        {
            if (!this.settingsViewModel.HasPendingChanges)
            {
                return true;
            }

            var result = System.Windows.MessageBox.Show(
                "You have unsaved changes in Settings.\n\nChoose an action:\n- Yes: Save and exit\n- No: Discard and exit\n- Cancel: Return to app",
                "Unsaved Settings",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Cancel)
            {
                return false;
            }

            if (result == MessageBoxResult.Yes)
            {
                var saved = await this.settingsViewModel.SaveIfDirtyAsync();
                return saved;
            }

            await this.settingsViewModel.DiscardPendingChangesAsync();
            return true;
        }

        private async Task HandleWindowCloseAsync()
        {
            if (!await this.HandleUnsavedSettingsBeforeExitAsync())
            {
                return;
            }

            if (this.settingsService.Settings.CloseToTray)
            {
                this.WindowState = WindowState.Minimized;
                return;
            }

            await this.PerformGracefulShutdownAsync(validateUnsavedChanges: false);
        }

        private void OnMonitoringToggleRequested(object? sender, MonitoringToggleEventArgs e)
        {
            TaskSafety.FireAndForget(this.OnMonitoringToggleRequestedAsync(e), ex =>
            {
                this.LogDebug($"OnMonitoringToggleRequested failed: {ex.Message}");
            });
        }

        private async Task OnMonitoringToggleRequestedAsync(MonitoringToggleEventArgs e)
        {
            try
            {
                if (e.EnableMonitoring)
                {
                    await this.processMonitorManagerService.StartAsync();
                    await this.notificationService.ShowSuccessNotificationAsync(
                        "Monitoring Enabled",
                        "Process monitoring and power plan management has been enabled");
                }
                else
                {
                    await this.processMonitorManagerService.StopAsync();
                    await this.notificationService.ShowNotificationAsync(
                        "Monitoring Disabled",
                        "Process monitoring and power plan management has been disabled",
                        Models.NotificationType.Warning);
                }
            }
            catch (Exception ex)
            {
                await this.notificationService.ShowErrorNotificationAsync(
                    "Monitoring Error",
                    "Failed to toggle process monitoring",
                    ex);
            }
        }

        private void OnSettingsRequested(object? sender, EventArgs e)
        {
            try
            {
                this.ShowWindowFromTray("Settings");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to open settings: {ex.Message}");
            }
        }

        private void OnPowerPlanChangeRequested(object? sender, PowerPlanChangeRequestedEventArgs e)
        {
            TaskSafety.FireAndForget(this.OnPowerPlanChangeRequestedAsync(e), ex =>
            {
                this.LogDebug($"OnPowerPlanChangeRequested failed: {ex.Message}");
            });
        }

        private async Task OnPowerPlanChangeRequestedAsync(PowerPlanChangeRequestedEventArgs e)
        {
            try
            {
                var powerPlanService = this.serviceProvider.GetRequiredService<IPowerPlanService>();
                var success = await powerPlanService.SetActivePowerPlanByGuidAsync(e.PowerPlanGuid);

                if (success)
                {
                    this.systemTrayService.ShowBalloonTip(
                        "ThreadPilot",
                        $"Power plan changed to {e.PowerPlanName}", 2000);
                }
                else
                {
                    this.systemTrayService.ShowBalloonTip(
                        "ThreadPilot Error",
                        $"Failed to change power plan to {e.PowerPlanName}", 3000);
                }
            }
            catch (Exception ex)
            {
                this.systemTrayService.ShowBalloonTip(
                    "ThreadPilot Error",
                    $"Error changing power plan: {ex.Message}", 3000);
            }
        }

        private void OnProfileApplicationRequested(object? sender, ProfileApplicationRequestedEventArgs e)
        {
            TaskSafety.FireAndForget(this.OnProfileApplicationRequestedAsync(e), ex =>
            {
                this.LogDebug($"OnProfileApplicationRequested failed: {ex.Message}");
            });
        }

        private async Task OnProfileApplicationRequestedAsync(ProfileApplicationRequestedEventArgs e)
        {
            try
            {
                var processService = this.serviceProvider.GetRequiredService<IProcessService>();
                var selectedProcess = this.processViewModel.SelectedProcess;

                if (selectedProcess != null)
                {
                    var success = await processService.LoadProcessProfile(e.ProfileName, selectedProcess);

                    if (success)
                    {
                        this.systemTrayService.ShowBalloonTip(
                            "ThreadPilot",
                            $"Profile '{e.ProfileName}' applied to {selectedProcess.Name}", 2000);
                    }
                    else
                    {
                        this.systemTrayService.ShowBalloonTip(
                            "ThreadPilot Error",
                            $"Failed to apply profile '{e.ProfileName}'", 3000);
                    }
                }
                else
                {
                    this.systemTrayService.ShowBalloonTip(
                        "ThreadPilot",
                        "No process selected for profile application", 2000);
                }
            }
            catch (Exception ex)
            {
                this.systemTrayService.ShowBalloonTip(
                    "ThreadPilot Error",
                    $"Error applying profile: {ex.Message}", 3000);
            }
        }

        private void OnPerformanceDashboardRequested(object? sender, EventArgs e)
        {
            try
            {
                this.ShowWindowFromTray("Performance");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to open performance dashboard: {ex.Message}");
            }
        }

        private async Task InitializeKeyboardShortcutsAsync()
        {
            try
            {
                // Set window handle for global hotkey registration
                var windowInteropHelper = new System.Windows.Interop.WindowInteropHelper(this);
                var handle = windowInteropHelper.EnsureHandle();

                if (this.keyboardShortcutService is KeyboardShortcutService service)
                {
                    service.SetWindowHandle(handle);
                }

                // Subscribe to shortcut activation events
                this.keyboardShortcutService.ShortcutActivated -= this.OnShortcutActivated;
                this.keyboardShortcutService.ShortcutActivated += this.OnShortcutActivated;

                // Load shortcuts from settings - with error handling
                try
                {
                    await this.keyboardShortcutService.LoadShortcutsFromSettingsAsync();
                }
                catch (Exception settingsEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load shortcuts from settings, using defaults: {settingsEx.Message}");
                    // Continue with default shortcuts if settings loading fails
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize keyboard shortcuts: {ex.Message}");
                // Don't let keyboard shortcut initialization failure prevent the app from starting
            }
        }

        private void OnShortcutActivated(object? sender, ShortcutActivatedEventArgs e)
        {
            try
            {
                System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    await this.HandleShortcutActionAsync(e.ActionName);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling shortcut {e.ActionName}: {ex.Message}");
            }
        }

        private async Task HandleShortcutActionAsync(string actionName)
        {
            switch (actionName)
            {
                case ShortcutActions.ShowMainWindow:
                    if (this.IsVisible && this.WindowState != WindowState.Minimized)
                    {
                        this.ShowInTaskbar = false;
                        this.Hide();
                    }
                    else
                    {
                        this.ShowWindowFromTray();
                    }
                    break;

                case ShortcutActions.ToggleMonitoring:
                    // Toggle monitoring - implementation can be added later
                    await this.notificationService.ShowNotificationAsync("Keyboard Shortcut", "Toggle monitoring shortcut activated");
                    break;

                case ShortcutActions.PowerPlanHighPerformance:
                    // Switch to high performance power plan - implementation can be added later
                    await this.notificationService.ShowNotificationAsync("Keyboard Shortcut", "High Performance power plan shortcut activated");
                    break;

                case ShortcutActions.OpenTweaks:
                    this.ShowWindowFromTray("Tweaks");
                    break;

                case ShortcutActions.OpenSettings:
                    this.ShowWindowFromTray("Settings");
                    break;

                case ShortcutActions.RefreshProcessList:
                    // Refresh process list - implementation can be added later
                    await this.notificationService.ShowNotificationAsync("Keyboard Shortcut", "Refresh process list shortcut activated");
                    break;

                case ShortcutActions.ExitApplication:
                    this.Close();
                    break;
            }
        }

        private async Task UpdateSystemTrayContextMenuAsync()
        {
            try
            {
                // Update power plans in system tray
                var powerPlanService = this.serviceProvider.GetRequiredService<IPowerPlanService>();
                var powerPlans = await powerPlanService.GetPowerPlansAsync();
                var activePowerPlan = await powerPlanService.GetActivePowerPlan();
                this.systemTrayService.UpdatePowerPlans(powerPlans, activePowerPlan);

                // Update profiles in system tray
                var profilesDirectory = StoragePaths.ProfilesDirectory;
                var profileNames = new List<string>();

                if (Directory.Exists(profilesDirectory))
                {
                    profileNames = Directory.GetFiles(profilesDirectory, "*.json")
                        .Select(Path.GetFileNameWithoutExtension)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .ToList()!;
                }

                this.systemTrayService.UpdateProfiles(profileNames);

                // Update system status (with timeout to prevent hanging)
                try
                {
                    var performanceService = this.serviceProvider.GetRequiredService<IPerformanceMonitoringService>();
                    var metricsTask = performanceService.GetSystemMetricsAsync(lightweight: true);
                    var metricsResult = await Task.WhenAny(metricsTask, Task.Delay(2000)); // 2 second timeout

                    if (metricsResult == metricsTask)
                    {
                        var currentMetrics = await metricsTask;
                        this.systemTrayService.UpdateSystemStatus(
                            activePowerPlan?.Name ?? "Unknown",
                            currentMetrics?.TotalCpuUsage ?? 0.0,
                            currentMetrics?.MemoryUsagePercentage ?? 0.0);
                    }
                    else
                    {
                        // Timeout - use default values
                        this.systemTrayService.UpdateSystemStatus(
                            activePowerPlan?.Name ?? "Unknown",
                            0.0, 0.0);
                    }
                }
                catch (Exception metricsEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to get performance metrics for tray: {metricsEx.Message}");
                    // Use default values
                    this.systemTrayService.UpdateSystemStatus(
                        activePowerPlan?.Name ?? "Unknown",
                        0.0, 0.0);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update system tray context menu: {ex.Message}");
            }
        }

        private void StartSystemTrayUpdateTimer()
        {
            try
            {
                this.systemTrayUpdateTimer?.Stop();
                this.systemTrayUpdateTimer?.Dispose();

                this.systemTrayUpdateFailureStreak = 0;
                this.systemTrayUpdateTimer = new System.Timers.Timer(SystemTrayUpdateBaseIntervalMs);
                this.systemTrayUpdateTimer.Elapsed += async (s, e) =>
                {
                    if (this.isSystemTrayUpdatesSuspended)
                    {
                        return;
                    }

                    if (Interlocked.Exchange(ref this.isSystemTrayUpdateInProgress, 1) == 1)
                    {
                        return;
                    }

                    try
                    {
                        var updateSucceeded = await this.UpdateSystemTrayStatusAsync();
                        this.ApplySystemTrayUpdateBackoff(updateSucceeded);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error in system tray update timer: {ex.Message}");
                        this.ApplySystemTrayUpdateBackoff(updateSucceeded: false);
                    }
                    finally
                    {
                        Interlocked.Exchange(ref this.isSystemTrayUpdateInProgress, 0);
                    }
                };
                this.systemTrayUpdateTimer.AutoReset = true;
                this.systemTrayUpdateTimer.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to start system tray update timer: {ex.Message}");
            }
        }

        private void ApplySystemTrayUpdateBackoff(bool updateSucceeded)
        {
            if (this.systemTrayUpdateTimer == null)
            {
                return;
            }

            if (updateSucceeded)
            {
                this.systemTrayUpdateFailureStreak = 0;
                if (Math.Abs(this.systemTrayUpdateTimer.Interval - SystemTrayUpdateBaseIntervalMs) > 1)
                {
                    this.systemTrayUpdateTimer.Interval = SystemTrayUpdateBaseIntervalMs;
                }

                return;
            }

            this.systemTrayUpdateFailureStreak = Math.Min(4, this.systemTrayUpdateFailureStreak + 1);
            var exponentialDelay = SystemTrayUpdateBaseIntervalMs * Math.Pow(2, this.systemTrayUpdateFailureStreak);
            var nextIntervalMs = Math.Min(SystemTrayUpdateMaxIntervalMs, exponentialDelay);

            if (Math.Abs(this.systemTrayUpdateTimer.Interval - nextIntervalMs) > 1)
            {
                this.systemTrayUpdateTimer.Interval = nextIntervalMs;
            }
        }

        private async Task<bool> UpdateSystemTrayStatusAsync()
        {
            try
            {
                var powerPlanService = this.serviceProvider.GetRequiredService<IPowerPlanService>();
                var performanceService = this.serviceProvider.GetRequiredService<IPerformanceMonitoringService>();

                var activePowerPlan = await powerPlanService.GetActivePowerPlan();
                var currentMetrics = await performanceService.GetSystemMetricsAsync(lightweight: true);

                await this.Dispatcher.InvokeAsync(() =>
                {
                    this.systemTrayService.UpdateSystemStatus(
                        activePowerPlan?.Name ?? "Unknown",
                        currentMetrics?.TotalCpuUsage ?? 0.0,
                        currentMetrics?.MemoryUsagePercentage ?? 0.0);
                });

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update system tray status: {ex.Message}");
                return false;
            }
        }

        private void InitializeNotifications()
        {
            try
            {
                // Subscribe to settings changes to update notification service
                this.settingsService.SettingsChanged += this.OnSettingsChanged;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize notifications: {ex.Message}");
            }
        }

        private async Task InitializeMonitoringAsync()
        {
            try
            {
                // Subscribe to monitoring status changes
                this.processMonitorService.MonitoringStatusChanged += this.OnMonitoringStatusChanged;

                // Update tray with initial monitoring status
                this.systemTrayService.UpdateMonitoringStatus(
                    this.processMonitorService.IsMonitoring,
                    this.processMonitorService.IsWmiAvailable);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize monitoring: {ex.Message}");
            }
        }

        private async Task StartProcessMonitoringManagerAsync()
        {
            try
            {
                this.LogDebug("Subscribing to process monitor manager events...");
                // Subscribe to process monitor manager events
                this.processMonitorManagerService.ServiceStatusChanged += this.OnProcessMonitorManagerStatusChanged;

                this.LogDebug("Starting process monitoring manager service...");
                // Start the process monitoring manager service with internal timeout
                var startTask = this.processMonitorManagerService.StartAsync();
                var timeoutTask = Task.Delay(6000); // 6 second internal timeout
                var completedTask = await Task.WhenAny(startTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    this.LogDebug("ProcessMonitorManagerService.StartAsync() timed out internally");
                    throw new TimeoutException("Process monitoring manager service startup timed out");
                }

                await startTask; // Get any exceptions
                this.LogDebug("Process monitoring manager service started, showing notification...");

                await this.notificationService.ShowSuccessNotificationAsync(
                    "ThreadPilot Started",
                    "Process monitoring and power plan management is now active");

                this.LogDebug("Success notification shown");
            }
            catch (Exception ex)
            {
                this.LogDebug($"Failed to start process monitoring manager: {ex.Message}");
                try
                {
                    await this.notificationService.ShowErrorNotificationAsync(
                        "Startup Error",
                        "Failed to start process monitoring manager",
                        ex);
                }
                catch (Exception notificationEx)
                {
                    this.LogDebug($"Failed to show error notification: {notificationEx.Message}");
                }
                throw; // Re-throw to be caught by outer handler
            }
        }

        private void OnSettingsChanged(object? sender, ApplicationSettingsChangedEventArgs e)
        {
            // Update tray service with new settings
            this.systemTrayService.UpdateSettings(e.NewSettings);

            var useDarkTheme = e.NewSettings.HasUserThemePreference
                ? e.NewSettings.UseDarkTheme
                : this.themeService.GetSystemUsesDarkTheme();

            this.themeService.ApplyTheme(useDarkTheme);
            this.mainWindowViewModel.IsDarkTheme = useDarkTheme;
            this.systemTrayService.ApplyTheme(useDarkTheme);
            DwmHelper.ApplyWindowCaptionTheme(this, useDarkTheme);
        }

        private void OnMonitoringStatusChanged(object? sender, MonitoringStatusEventArgs e)
        {
            // Update tray icon and status
            this.systemTrayService.UpdateMonitoringStatus(e.IsMonitoring, e.IsWmiAvailable);

            // Show notification if there's an error
            if (e.Error != null && this.settingsService.Settings.EnableErrorNotifications)
            {
                this.notificationService.ShowErrorNotificationAsync(
                    "Monitoring Error",
                    e.StatusMessage ?? "An error occurred with process monitoring",
                    e.Error);
            }
        }

        private void OnProcessMonitorManagerStatusChanged(object? sender, ServiceStatusEventArgs e)
        {
            // Update main window status
            this.mainWindowViewModel.UpdateProcessMonitoringStatus(e.IsRunning, e.Status);

            // Show notification for critical status changes
            if (!e.IsRunning && e.Error != null && this.settingsService.Settings.EnableErrorNotifications)
            {
                this.notificationService.ShowErrorNotificationAsync(
                    "Process Monitoring Error",
                    e.Details ?? "Process monitoring manager encountered an error",
                    e.Error);
            }
        }

        protected override void OnStateChanged(EventArgs e)
        {
            try
            {
                if (this.WindowState == WindowState.Minimized && this.settingsService.Settings.MinimizeToTray)
                {
                    this.ShowInTaskbar = false;
                    this.Hide();
                    this.systemTrayService.Show();

                    this.SuspendHiddenModeRefreshes();

                    // Pause process refresh when minimized to reduce resource usage
                    if (this.processViewModel != null)
                    {
                        this.processViewModel.PauseRefresh();
                    }

                    if (this.performanceViewModel != null)
                    {
                        _ = this.performanceViewModel.SuspendBackgroundMonitoringAsync();
                    }
                }
                else if (this.WindowState == WindowState.Normal)
                {
                    this.ShowInTaskbar = true;

                    this.ResumeForegroundRefreshes();

                    // Resume process refresh when restored
                    if (this.processViewModel != null)
                    {
                        this.processViewModel.ResumeRefresh();
                    }

                    if (this.performanceViewModel != null)
                    {
                        _ = this.performanceViewModel.ResumeBackgroundMonitoringAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling window state change: {ex.Message}");
            }

            base.OnStateChanged(e);
        }

        private void SuspendHiddenModeRefreshes()
        {
            this.isSystemTrayUpdatesSuspended = true;
            this.systemTrayUpdateTimer?.Stop();
            Interlocked.Exchange(ref this.isSystemTrayUpdateInProgress, 0);
            this.powerPlanViewModel.PauseAutoRefresh();
        }

        private void ResumeForegroundRefreshes()
        {
            this.isSystemTrayUpdatesSuspended = false;
            this.systemTrayUpdateFailureStreak = 0;
            this.systemTrayUpdateTimer?.Stop();
            if (this.systemTrayUpdateTimer != null)
            {
                this.systemTrayUpdateTimer.Interval = SystemTrayUpdateBaseIntervalMs;
            }
            this.systemTrayUpdateTimer?.Start();
            this.powerPlanViewModel.ResumeAutoRefresh(refreshImmediately: true);

            _ = this.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    var updateSucceeded = await this.UpdateSystemTrayStatusAsync();
                    this.ApplySystemTrayUpdateBackoff(updateSucceeded);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to refresh tray status after resume: {ex.Message}");
                }
            });
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            try
            {
                DwmHelper.ApplyWindowCaptionTheme(this, this.themeService.IsDarkTheme);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to apply window caption theme: {ex.Message}");
            }
        }

        [System.Diagnostics.Conditional("DEBUG")]
        private void LogDebug(string message)
        {
            try
            {
                var timestampedMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [MainWindow] {message}";
                System.Diagnostics.Debug.WriteLine(timestampedMessage);
                File.AppendAllText(this.debugLogPath, timestampedMessage + Environment.NewLine);
            }
            catch
            {
                // Swallow logging failures to avoid impacting runtime behavior
            }
        }

        private void OnWindowLoaded(object? sender, RoutedEventArgs e)
        {
            TaskSafety.FireAndForget(this.OnWindowLoadedAsync(), ex =>
            {
                this.LogDebug($"OnWindowLoaded failed: {ex.Message}");
            });
        }

        private async Task OnWindowLoadedAsync()
        {
            this.Loaded -= this.OnWindowLoaded;
            await this.InitializeKeyboardShortcutsAsync();
        }

        private void OnOpenRulesRequested(object? sender, EventArgs e)
        {
            this.SelectMainTab("Rules");
        }

        private void ShowWindowFromTray(string? tabTag = null)
        {
            this.ShowInTaskbar = true;
            this.Visibility = Visibility.Visible;

            if (!this.IsVisible)
            {
                this.Show();
            }

            if (this.WindowState == WindowState.Minimized)
            {
                this.WindowState = WindowState.Normal;
            }

            // Force foreground restoration when invoked from tray context menu.
            this.Topmost = true;
            this.Activate();
            this.Focus();
            this.Topmost = false;

            if (tabTag != null)
            {
                _ = this.Dispatcher.InvokeAsync(() => this.SelectMainTab(tabTag));
            }
        }

        private void SelectMainTab(string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return;
            }

            this.ApplySectionVisibility(tag);

            // Keep NavigationView internal state aligned when possible.
            this.RootNavigation.Navigate(tag);

            if (string.Equals(tag, "Performance", StringComparison.Ordinal))
            {
                this.TryShowPerformanceIntro();
            }
        }

        private void TryShowPerformanceIntro()
        {
            if (this.isPerformanceIntroVisible || !this.isInitializationComplete)
            {
                return;
            }

            try
            {
                var settings = this.settingsService.Settings;
                if (settings.HasSeenPerformanceIntro)
                {
                    return;
                }

                this.isPerformanceIntroVisible = true;
                this.PerformanceIntroOverlay.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                this.LogDebug($"Failed to show Performance intro overlay: {ex.Message}");
            }
        }

        private void HidePerformanceIntro()
        {
            this.isPerformanceIntroVisible = false;
            this.PerformanceIntroOverlay.Visibility = Visibility.Collapsed;
        }

        private async void PerformanceIntroContinue_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settings = this.settingsService.Settings;
                if (!settings.HasSeenPerformanceIntro)
                {
                    settings.HasSeenPerformanceIntro = true;
                    await this.settingsService.UpdateSettingsAsync(settings);
                }
            }
            catch (Exception ex)
            {
                this.LogDebug($"Failed to persist Performance intro state: {ex.Message}");
            }
            finally
            {
                this.HidePerformanceIntro();
            }
        }

        // Elevation Warning Modal Management
        private bool isElevationWarningVisible = false;
        private double previousElevationAppContentOpacity = 1;
        private double previousElevationBackdropBlurRadius = 0;

        private void TryShowElevationWarning()
        {
            if (this.isElevationWarningVisible || !this.isInitializationComplete)
            {
                return;
            }

            try
            {
                var settings = this.settingsService.Settings;
                
                // Only show if user is not admin AND hasn't dismissed the warning yet
                if (this.elevationService?.IsRunningAsAdministrator() == true || settings.HasSeenElevationWarning)
                {
                    return;
                }

                this.isElevationWarningVisible = true;
                var elevationOverlay = this.FindName("ElevationWarningOverlay") as Grid;
                if (elevationOverlay != null)
                {
                    elevationOverlay.Visibility = Visibility.Visible;
                }

                // Apply blur and disable interaction
                this.previousElevationAppContentOpacity = this.UIContent.Opacity;
                this.UIContent.IsHitTestVisible = false;
                this.UIContent.Opacity = 0.74;

                var elevationBlur = this.FindName("ElevationWarningBlur") as BlurEffect;
                if (elevationBlur != null)
                {
                    this.previousElevationBackdropBlurRadius = elevationBlur.Radius;
                    elevationBlur.Radius = 16;
                }
            }
            catch (Exception ex)
            {
                this.LogDebug($"Failed to show elevation warning overlay: {ex.Message}");
            }
        }

        private void HideElevationWarning()
        {
            this.isElevationWarningVisible = false;
            var elevationOverlay = this.FindName("ElevationWarningOverlay") as Grid;
            if (elevationOverlay != null)
            {
                elevationOverlay.Visibility = Visibility.Collapsed;
            }

            // Restore interaction and remove blur
            this.UIContent.IsHitTestVisible = true;
            this.UIContent.Opacity = this.previousElevationAppContentOpacity;

            var elevationBlur = this.FindName("ElevationWarningBlur") as BlurEffect;
            if (elevationBlur != null)
            {
                elevationBlur.Radius = this.previousElevationBackdropBlurRadius;
            }
        }

        private void ElevationWarningDismiss_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settings = this.settingsService.Settings;
                if (!settings.HasSeenElevationWarning)
                {
                    settings.HasSeenElevationWarning = true;
                    _ = this.settingsService.UpdateSettingsAsync(settings);
                }
            }
            catch (Exception ex)
            {
                this.LogDebug($"Failed to persist elevation warning dismiss state: {ex.Message}");
            }
            finally
            {
                this.HideElevationWarning();
            }
        }

        private async void ElevationWarningRequestElevation_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (this.elevationService != null)
                {
                    var success = await this.elevationService.RequestElevationIfNeeded();
                    if (success)
                    {
                        System.Diagnostics.Debug.WriteLine("Elevation requested successfully from warning dialog");
                    }
                }
            }
            catch (Exception ex)
            {
                this.LogDebug($"Failed to request elevation from warning dialog: {ex.Message}");
            }
            finally
            {
                // Hide the warning after attempting elevation (regardless of success)
                this.HideElevationWarning();
            }
        }

        private void ApplySectionVisibility(string tag)
        {
            this.ProcessManagementTab.Visibility = tag == "Process" ? Visibility.Visible : Visibility.Collapsed;
            this.CoreMasksTab.Visibility = tag == "Masks" ? Visibility.Visible : Visibility.Collapsed;
            this.PowerPlanViewControl.Visibility = tag == "Power" ? Visibility.Visible : Visibility.Collapsed;
            this.AssociationView.Visibility = tag == "Rules" ? Visibility.Visible : Visibility.Collapsed;
            this.PerformanceViewControl.Visibility = tag == "Performance" ? Visibility.Visible : Visibility.Collapsed;
            this.LogViewerViewControl.Visibility = tag == "Logs" ? Visibility.Visible : Visibility.Collapsed;
            this.SystemTweaksView.Visibility = tag == "Tweaks" ? Visibility.Visible : Visibility.Collapsed;
            this.SettingsView.Visibility = tag == "Settings" ? Visibility.Visible : Visibility.Collapsed;

            this.NavProcess.IsActive = tag == "Process";
            this.NavMasks.IsActive = tag == "Masks";
            this.NavPower.IsActive = tag == "Power";
            this.NavRules.IsActive = tag == "Rules";
            this.NavPerf.IsActive = tag == "Performance";
            this.NavLogs.IsActive = tag == "Logs";
            this.NavTweaks.IsActive = tag == "Tweaks";
            this.NavSettings.IsActive = tag == "Settings";
        }

        private void NavMenuItem_Click(object sender, RoutedEventArgs e)
        {
            TaskSafety.FireAndForget(this.NavMenuItem_ClickAsync(sender, e), ex =>
            {
                this.LogDebug($"NavMenuItem_Click failed: {ex.Message}");
            });
        }

        private async Task NavMenuItem_ClickAsync(object sender, RoutedEventArgs e)
        {
            if (!await this.navigationBehavior.TryEnterAsync())
            {
                return;
            }

            try
            {
                var invokedItem = sender as Wpf.Ui.Controls.NavigationViewItem;
                if (invokedItem == null)
                {
                    return;
                }

                var tag = invokedItem.Tag?.ToString();
                if (string.IsNullOrEmpty(tag))
                {
                    return;
                }

                if (!this.IsLoaded)
                {
                    return;
                }

                var canNavigate = await NavigationBehavior.EnsureCanNavigateAsync(tag, this.settingsViewModel);
                if (!canNavigate)
                {
                    return;
                }

                this.ApplySectionVisibility(tag);

                if (string.Equals(tag, "Performance", StringComparison.Ordinal))
                {
                    this.TryShowPerformanceIntro();
                }
            }
            finally
            {
                this.navigationBehavior.Exit();
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (this.isPerformingShutdown)
            {
                base.OnClosing(e);
                return;
            }

            if (this.isPerformanceIntroVisible)
            {
                e.Cancel = true;
                System.Windows.MessageBox.Show(
                    "Please complete the Performance introduction before closing the application.\n\nClick 'Continue to Performance' to proceed.",
                    "Performance Introduction Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            e.Cancel = true;
            _ = this.HandleWindowCloseAsync();
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                this.Loaded -= this.OnWindowLoaded;
                this.processViewModel.OpenRulesRequested -= this.OnOpenRulesRequested;

                this.settingsService.SettingsChanged -= this.OnSettingsChanged;
                this.processMonitorService.MonitoringStatusChanged -= this.OnMonitoringStatusChanged;
                this.processMonitorManagerService.ServiceStatusChanged -= this.OnProcessMonitorManagerStatusChanged;
                this.keyboardShortcutService.ShortcutActivated -= this.OnShortcutActivated;

                this.UnsubscribeSystemTrayEvents();

                this.systemTrayUpdateTimer?.Stop();
                this.systemTrayUpdateTimer?.Dispose();

                this.initializationTimeoutTimer?.Stop();
                this.initializationTimeoutTimer?.Dispose();

                this.navigationBehavior.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disposing timers: {ex.Message}");
            }

            base.OnClosed(e);
        }

        private void UnsubscribeSystemTrayEvents()
        {
            this.systemTrayService.ShowMainWindowRequested -= this.OnShowMainWindowRequested;
            this.systemTrayService.DashboardRequested -= this.OnDashboardRequested;
            this.systemTrayService.ExitRequested -= this.OnExitRequested;
            this.systemTrayService.MonitoringToggleRequested -= this.OnMonitoringToggleRequested;
            this.systemTrayService.SettingsRequested -= this.OnSettingsRequested;
            this.systemTrayService.PowerPlanChangeRequested -= this.OnPowerPlanChangeRequested;
            this.systemTrayService.ProfileApplicationRequested -= this.OnProfileApplicationRequested;
            this.systemTrayService.PerformanceDashboardRequested -= this.OnPerformanceDashboardRequested;
        }
    }
}
