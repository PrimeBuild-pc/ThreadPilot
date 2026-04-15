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
            bool startMinimized = false;
            bool isAutostart = false;
            bool isSmokeTest = false;
            bool registerLaunchTask = false;
            bool launchedViaTask = false;
#if DEBUG
            bool isTestMode = false;
#endif

            foreach (var arg in e.Args)
            {
                switch (arg.ToLowerInvariant())
                {
#if DEBUG
                    case "--test":
                        isTestMode = true;
                        break;
#endif
                    case "--smoke-test":
                        isSmokeTest = true;
                        break;
                    case "--start-minimized":
                        startMinimized = true;
                        break;
                    case "--autostart":
                        isAutostart = true;
                        break;
                    case "--startup": // Alternative startup argument
                        isAutostart = true;
                        startMinimized = true;
                        break;
                    case RegisterLaunchTaskArgument:
                        registerLaunchTask = true;
                        break;
                    case LaunchedViaTaskArgument:
                        launchedViaTask = true;
                        break;
                }
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
                if (launchedViaTask)
                {
                    logger.LogWarning("Application was launched via managed task marker but is still not elevated. Continuing in limited mode.");
                }
#if DEBUG
                else if (!isSmokeTest && !isTestMode)
#else
                else if (!isSmokeTest)
#endif
                {
                    var launchedElevatedInstance = Task.Run(async () => await elevatedTaskService.TryRunLaunchTaskAsync()).GetAwaiter().GetResult();
                    if (launchedElevatedInstance)
                    {
                        logger.LogInformation("Managed elevated launch task started successfully. Exiting current non-elevated instance.");
                        this.Shutdown();
                        return;
                    }

                    if (!registerLaunchTask)
                    {
                        logger.LogInformation("Managed elevated launch task is unavailable. Requesting one-time elevation to bootstrap persistent launch.");
                        var restartInitiated = Task.Run(async () => await elevationService.RestartWithElevation(new[] { RegisterLaunchTaskArgument })).GetAwaiter().GetResult();
                        if (restartInitiated)
                        {
                            return;
                        }
                    }
                }

                logger.LogWarning("Application is running without administrator privileges. Elevated operations will require explicit elevation.");
            }

            // Enforce single-instance after elevation bootstrap logic to avoid mutex races during handoff.
            if (!isSmokeTest)
            {
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
            }

            base.OnStartup(e);

            // Check for test mode
#if DEBUG
            if (isTestMode)
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

            if (isSmokeTest)
            {
                var smokeTestResult = Task.Run(async () => await this.RunSmokeTestAsync(logger)).GetAwaiter().GetResult();
                this.Shutdown(smokeTestResult);
                return;
            }

            try
            {
                var settingsService = this.ServiceProvider.GetRequiredService<IApplicationSettingsService>();
                var themeService = this.ServiceProvider.GetRequiredService<IThemeService>();

                Task.Run(async () => await settingsService.LoadSettingsAsync()).GetAwaiter().GetResult();
                var settings = settingsService.Settings;
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

            // Handle startup behavior with comprehensive error handling
            try
            {
                logger.LogInformation("Attempting to show main window...");

                // Ensure the window is properly initialized
                if (mainWindow == null)
                {
                    throw new InvalidOperationException("MainWindow could not be created");
                }

                if (isAutostart)
                {
                    mainWindow.ShowInTaskbar = false;
                    mainWindow.Visibility = Visibility.Hidden;
                    mainWindow.WindowState = WindowState.Minimized;
                    mainWindow.Show();
                    mainWindow.Hide();
                }
                else
                {
                    // Show the window with explicit visibility settings
                    mainWindow.ShowInTaskbar = true;
                    mainWindow.Visibility = Visibility.Visible;
                    mainWindow.WindowState = startMinimized
                        ? WindowState.Minimized
                        : WindowState.Normal;
                    mainWindow.Show();
                    if (mainWindow.WindowState != WindowState.Minimized)
                    {
                        mainWindow.Activate();
                    }
                }

                logger.LogInformation("Main window displayed successfully");
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

        private async Task<int> RunSmokeTestAsync(ILogger logger)
        {
            try
            {
                logger.LogInformation("Starting ThreadPilot smoke test");

                var settingsService = this.ServiceProvider.GetRequiredService<IApplicationSettingsService>();
                await settingsService.LoadSettingsAsync().ConfigureAwait(false);
                _ = this.ServiceProvider.GetRequiredService<ProcessViewModel>();
                _ = this.ServiceProvider.GetRequiredService<PowerPlanViewModel>();

                logger.LogInformation("ThreadPilot smoke test completed successfully");
                return 0;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "ThreadPilot smoke test failed");
                return 1;
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
                "ThreadPilot is running with limited privileges. Some features may not be available.\n\n" +
                "For full functionality including process affinity and power plan management, " +
                "administrator privileges are required.\n\n" +
                "You can request elevation from the application menu when needed.",
                "Limited Privileges",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
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


