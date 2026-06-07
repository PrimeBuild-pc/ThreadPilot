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
#if DEBUG
using ThreadPilot.Tests;
#endif
using ThreadPilot.Services;
using ThreadPilot.ViewModels;

namespace ThreadPilot
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Threading;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using ThreadPilot.Helpers;
    using ThreadPilot.Models;

    public partial class App : System.Windows.Application
    {
        private const string RegisterLaunchTaskArgument = "--register-launch-task";
        private const string LaunchedViaTaskArgument = "--launched-via-task";

        private Mutex? singleInstanceMutex;
        private int uiExceptionDialogOpen;
        private DateTime lastUiExceptionDialogUtc = DateTime.MinValue;

        public IServiceProvider ServiceProvider { get; private set; }

        public App()
        {
            ServiceCollection services = new ServiceCollection();

            // Use the new centralized service configuration
            services.ConfigureApplicationServices();

            this.ServiceProvider = services.BuildServiceProvider();

            // Validate service configuration
            ServiceConfiguration.ValidateServiceConfiguration(this.ServiceProvider);
        }



        protected override void OnStartup(StartupEventArgs e)
        {
            // Parse command line arguments early so special startup modes can short-circuit normal flow.
            var startupMode = StartupMode.Parse(e.Args);
            bool effectiveStartMinimized = false;
            ApplicationSettingsModel? loadedSettings = null;

            effectiveStartMinimized = startupMode.StartMinimized;

            if (startupMode.IsSmokeTest)
            {
                var smokeLogger = this.ServiceProvider.GetRequiredService<ILogger<App>>();
                var smokeTestResult = this.RunSmokeTestWithTimeout(smokeLogger, TimeSpan.FromSeconds(10));
                Environment.ExitCode = smokeTestResult;
                this.Shutdown(smokeTestResult);
                Environment.Exit(smokeTestResult);
                return;
            }

            // Set up global exception handlers first
            AppDomain.CurrentDomain.UnhandledException += this.OnUnhandledException;
            this.DispatcherUnhandledException += this.OnDispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += this.OnUnobservedTaskException;

            // Check elevation status first
            var elevationService = this.ServiceProvider.GetRequiredService<IElevationService>();
            var elevatedTaskService = this.ServiceProvider.GetRequiredService<IElevatedTaskService>();
            var logger = this.ServiceProvider.GetRequiredService<ILogger<App>>();
            var isRunningAsAdministrator = elevationService.IsRunningAsAdministrator();

            if (isRunningAsAdministrator)
            {
                logger.LogInformation("Application is running with administrator privileges");

                var launchTaskEnsured = Task.Run(async () => await elevatedTaskService.EnsureLaunchTaskAsync()).GetAwaiter().GetResult();
                if (!launchTaskEnsured)
                {
                    logger.LogWarning("Failed to ensure managed elevated launch task during startup. Future launches may require one-time elevation again.");
                }
            }
            else
            {
                if (startupMode.LaunchedViaTask)
                {
                    logger.LogError("Application was launched via managed task marker but is still not elevated.");
                }
#if DEBUG
                else if (!startupMode.IsTestMode)
#else
                else
#endif
                {
                    var launchedElevatedInstance = Task.Run(async () => await elevatedTaskService.TryRunLaunchTaskAsync()).GetAwaiter().GetResult();
                    if (launchedElevatedInstance)
                    {
                        logger.LogInformation("Managed elevated launch task started successfully. Exiting current non-elevated instance.");
                        this.Shutdown();
                        return;
                    }

                    if (!startupMode.RegisterLaunchTask)
                    {
                        logger.LogInformation("Managed elevated launch task is unavailable. Requesting one-time elevation to bootstrap persistent launch.");
                        var restartInitiated = Task.Run(async () => await elevationService.RestartWithElevation(new[] { RegisterLaunchTaskArgument })).GetAwaiter().GetResult();
                        if (restartInitiated)
                        {
                            return;
                        }
                    }
                }

#if DEBUG
                if (!startupMode.IsTestMode)
#else
                if (true)
#endif
                {
                    logger.LogError("ThreadPilot requires administrator privileges and cannot continue without elevation.");
                    this.ShowElevationRequiredMessage();
                    this.Shutdown(1);
                    return;
                }
            }

            // Enforce single-instance after elevation bootstrap logic to avoid mutex races during handoff.
            bool createdNew;
            this.singleInstanceMutex = new Mutex(initiallyOwned: true, name: "Global\\ThreadPilot_SingleInstance", createdNew: out createdNew);
            if (!createdNew)
            {
                System.Windows.MessageBox.Show(
                    "ThreadPilot is already running.",
                    "Instance already open",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                this.Shutdown();
                return;
            }

            base.OnStartup(e);

            // Check for test mode
#if DEBUG
            if (startupMode.IsTestMode)
            {
                // Run in console test mode
                AllocConsole();
                _ = Task.Run(async () =>
                {
                    await TestRunner.RunTests();
                    this.Dispatcher.Invoke(() => this.Shutdown());
                });
                return;
            }
#endif

            try
            {
                var settingsService = this.ServiceProvider.GetRequiredService<IApplicationSettingsService>();
                var themeService = this.ServiceProvider.GetRequiredService<IThemeService>();
                var localizationService = this.ServiceProvider.GetRequiredService<ILocalizationService>();

                Task.Run(async () => await settingsService.LoadSettingsAsync()).GetAwaiter().GetResult();
                var settings = settingsService.Settings;
                loadedSettings = settings;
                localizationService.ApplyLanguage(settings.Language);
                effectiveStartMinimized = startupMode.StartMinimized || settings.StartMinimized;
                var useDarkTheme = settings.HasUserThemePreference
                    ? settings.UseDarkTheme
                    : themeService.GetSystemUsesDarkTheme();

                if (!settings.HasUserThemePreference && settings.UseDarkTheme != useDarkTheme)
                {
                    settings.UseDarkTheme = useDarkTheme;
                    Task.Run(async () => await settingsService.UpdateSettingsAsync(settings)).GetAwaiter().GetResult();
                }

                themeService.ApplyTheme(useDarkTheme);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to preload theme settings during startup");
            }

            var mainWindow = this.ServiceProvider.GetRequiredService<MainWindow>();
            this.MainWindow = mainWindow;

            // Handle startup behavior with comprehensive error handling
            try
            {
                logger.LogInformation("Attempting to show main window...");

                // Ensure the window is properly initialized
                if (mainWindow == null)
                {
                    throw new InvalidOperationException("MainWindow could not be created");
                }

                var startupWindowBehavior = StartupWindowBehavior.Resolve(startupMode.IsAutostart, effectiveStartMinimized);
                var showStartupSuggestion = loadedSettings != null
                    && StartupMinimizedSuggestionPolicy.ShouldShow(loadedSettings, startupWindowBehavior);
                mainWindow.ConfigureStartupMode(
                    isSilentStartupMode: !startupWindowBehavior.ShouldShowWindow,
                    showStartupMinimizedSuggestionOnReady: showStartupSuggestion);

                mainWindow.ShowInTaskbar = startupWindowBehavior.ShowInTaskbar;
                mainWindow.Visibility = startupWindowBehavior.Visibility;
                mainWindow.WindowState = startupWindowBehavior.WindowState;

                if (startupWindowBehavior.ShouldShowWindow)
                {
                    mainWindow.Show();

                    if (startupWindowBehavior.HideAfterShow)
                    {
                        mainWindow.Hide();
                    }
                    else if (startupWindowBehavior.ActivateAfterShow)
                    {
                        mainWindow.EnsureDashboardVisibleOnScreen();
                        mainWindow.Activate();
                    }
                }

                logger.LogInformation("Startup window behavior applied successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Critical error during application startup");

                // Show error message and exit gracefully
                var errorMessage = $"ThreadPilot failed to start:\n\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}";
                System.Windows.MessageBox.Show(errorMessage, "ThreadPilot Startup Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                // Exit the application
                this.Shutdown(1);
                return;
            }
        }

        private int RunSmokeTestWithTimeout(ILogger logger, TimeSpan timeout)
        {
            var smokeTestTask = Task.Run(() => this.RunSmokeTest(logger));
            if (smokeTestTask.Wait(timeout))
            {
                return smokeTestTask.GetAwaiter().GetResult();
            }

            logger.LogError("ThreadPilot smoke test timed out after {TimeoutSeconds} seconds", timeout.TotalSeconds);
            return 2;
        }

        private int RunSmokeTest(ILogger logger)
        {
            try
            {
                logger.LogInformation("Starting ThreadPilot smoke test");

                _ = this.ServiceProvider.GetRequiredService<ILoggerFactory>();
                _ = this.ServiceProvider.GetRequiredService<IApplicationSettingsService>();
                _ = this.ServiceProvider.GetRequiredService<IThemeService>();
                _ = this.ServiceProvider.GetRequiredService<ILocalizationService>();

                if (!System.IO.Directory.Exists(AppContext.BaseDirectory))
                {
                    throw new InvalidOperationException("Application base directory was not found.");
                }

                logger.LogInformation("ThreadPilot smoke test completed successfully");
                return 0;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "ThreadPilot smoke test failed");
                return 1;
            }
        }

        private readonly struct StartupMode
        {
            public bool StartMinimized { get; init; }

            public bool IsAutostart { get; init; }

            public bool IsSmokeTest { get; init; }

            public bool RegisterLaunchTask { get; init; }

            public bool LaunchedViaTask { get; init; }

            public bool IsTestMode { get; init; }

            public static StartupMode Parse(IEnumerable<string> args)
            {
                var mode = default(StartupMode);
                foreach (var arg in args)
                {
                    switch (arg.ToLowerInvariant())
                    {
                        case "--test":
                            mode = mode with { IsTestMode = true };
                            break;
                        case "--smoke-test":
                            mode = mode with { IsSmokeTest = true };
                            break;
                        case "--start-minimized":
                            mode = mode with { StartMinimized = true };
                            break;
                        case "--autostart":
                            mode = mode with { IsAutostart = true };
                            break;
                        case "--startup":
                            mode = mode with { IsAutostart = true, StartMinimized = true };
                            break;
                        case RegisterLaunchTaskArgument:
                            mode = mode with { RegisterLaunchTask = true };
                            break;
                        case LaunchedViaTaskArgument:
                            mode = mode with { LaunchedViaTask = true };
                            break;
                    }
                }

                return mode;
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException -= this.OnUnhandledException;
            this.DispatcherUnhandledException -= this.OnDispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException -= this.OnUnobservedTaskException;

            if (this.singleInstanceMutex != null)
            {
                try
                {
                    this.singleInstanceMutex.ReleaseMutex();
                }
                catch
                {
                    // Ignore; we just want to clean up quietly
                }
                this.singleInstanceMutex.Dispose();
                this.singleInstanceMutex = null;
            }

            base.OnExit(e);
        }

#if DEBUG
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool AllocConsole();
#endif

        /// <summary>
        /// Shows a message to the user about elevation requirements.
        /// </summary>
        private void ShowElevationRequiredMessage()
        {
            // Don't show the message during autostart to avoid interrupting the user
            var args = Environment.GetCommandLineArgs();
            if (args.Any(arg => arg.Equals("--autostart", StringComparison.OrdinalIgnoreCase) ||
                               arg.Equals("--startup", StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            System.Windows.MessageBox.Show(
                "ThreadPilot requires administrator privileges to start.\n\n" +
                "Please relaunch the application and approve the UAC prompt.\n\n" +
                "This instance will now close.",
                "Administrator Privileges Required",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        /// <summary>
        /// Handles unhandled exceptions in the application domain.
        /// </summary>
        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception ?? new InvalidOperationException("Unhandled non-Exception object was raised.");
            this.ReportUnhandledException(exception, "AppDomain.CurrentDomain.UnhandledException", LogLevel.Critical);

            var errorMessage = $"A critical error occurred:\n\n{exception?.Message}\n\nThe application will now exit.";
            System.Windows.MessageBox.Show(errorMessage, "Critical Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }

        /// <summary>
        /// Handles unhandled exceptions on the UI thread.
        /// </summary>
        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            this.ReportUnhandledException(e.Exception, "Application.DispatcherUnhandledException", LogLevel.Error);

            if (Interlocked.CompareExchange(ref this.uiExceptionDialogOpen, 1, 0) != 0)
            {
                e.Handled = true;
                return;
            }

            if (DateTime.UtcNow - this.lastUiExceptionDialogUtc < TimeSpan.FromSeconds(2))
            {
                e.Handled = true;
                Interlocked.Exchange(ref this.uiExceptionDialogOpen, 0);
                return;
            }

            this.lastUiExceptionDialogUtc = DateTime.UtcNow;

            var errorMessage = $"An error occurred in the user interface:\n\n{e.Exception.Message}\n\nDo you want to continue?";
            var result = System.Windows.MessageBox.Show(errorMessage, "UI Error",
                MessageBoxButton.YesNo, MessageBoxImage.Error);

            if (result == MessageBoxResult.Yes)
            {
                e.Handled = true; // Continue running
            }
            else
            {
                e.Handled = false; // Let the application crash
            }

            Interlocked.Exchange(ref this.uiExceptionDialogOpen, 0);
        }

        /// <summary>
        /// Handles unobserved task exceptions from fire-and-forget tasks that escaped local handlers.
        /// </summary>
        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            var exception = e.Exception.Flatten();
            this.ReportUnhandledException(exception, "TaskScheduler.UnobservedTaskException", LogLevel.Error);
            e.SetObserved();
        }

        private void ReportUnhandledException(Exception exception, string source, LogLevel level)
        {
            var logger = this.ServiceProvider?.GetService<ILogger<App>>();
            if (level == LogLevel.Critical)
            {
                logger?.LogCritical(exception, "Unhandled exception in {Source}", source);
            }
            else
            {
                logger?.LogError(exception, "Unhandled exception in {Source}", source);
            }

            var enhancedLogger = this.ServiceProvider?.GetService<IEnhancedLoggingService>();
            if (enhancedLogger == null)
            {
                return;
            }

            var errorCode = exception is ThreadPilotException typedException
                ? typedException.ErrorCode.ToString()
                : ErrorCode.Unhandled.ToString();

            var context = new Dictionary<string, object>
            {
                ["Source"] = source,
                [LogProperties.ErrorCode] = errorCode,
                [LogProperties.CorrelationId] = enhancedLogger.GetCurrentCorrelationId() ?? "N/A",
                ["IsTerminatingLevel"] = level == LogLevel.Critical,
            };

            TaskSafety.FireAndForget(
                enhancedLogger.LogErrorAsync(exception, source, context),
                logFailure => logger?.LogWarning(logFailure, "Failed to persist unhandled exception report"));
        }
    }
}
