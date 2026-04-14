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
    using System.Linq;
    using System.Security.Principal;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Threading;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    public partial class App : System.Windows.Application
    {
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
            // Enforce single-instance: bail out if another instance is already running
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

            // Set up global exception handlers first
            AppDomain.CurrentDomain.UnhandledException += this.OnUnhandledException;
            this.DispatcherUnhandledException += this.OnDispatcherUnhandledException;

            // Check elevation status first
            var elevationService = this.ServiceProvider.GetRequiredService<IElevationService>();
            var logger = this.ServiceProvider.GetRequiredService<ILogger<App>>();

            if (!elevationService.IsRunningAsAdministrator())
            {
                logger.LogWarning("Application is not running with administrator privileges. Requesting elevation before startup.");

                var elevationRequested = elevationService.RequestElevationIfNeeded().GetAwaiter().GetResult();
                if (!elevationRequested)
                {
                    logger.LogWarning("Elevation was declined or failed. Application startup will be terminated.");
                    System.Windows.MessageBox.Show(
                        "ThreadPilot requires administrator privileges to start.\n\n" +
                        "The application will now close.",
                        "Administrator Privileges Required",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    this.Shutdown(1);
                    return;
                }

                // An elevated instance has been requested; terminate this non-elevated instance.
                this.Shutdown();
                return;
            }
            else
            {
                logger.LogInformation("Application is running with administrator privileges");
            }

            base.OnStartup(e);

            // Parse command line arguments
            bool startMinimized = false;
            bool isAutostart = false;
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
                }
            }

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

            try
            {
                var settingsService = this.ServiceProvider.GetRequiredService<IApplicationSettingsService>();
                var themeService = this.ServiceProvider.GetRequiredService<IThemeService>();

                Task.Run(async () => await settingsService.LoadSettingsAsync()).GetAwaiter().GetResult();
                var settings = settingsService.Settings;
                var useDarkTheme = settings.HasUserThemePreference
                    ? settings.UseDarkTheme
                    : themeService.GetSystemUsesDarkTheme();

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

        protected override void OnExit(ExitEventArgs e)
        {
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
            var exception = e.ExceptionObject as Exception;
            var logger = this.ServiceProvider?.GetService<ILogger<App>>();

            logger?.LogCritical(exception, "Unhandled exception occurred");

            var errorMessage = $"A critical error occurred:\n\n{exception?.Message}\n\nThe application will now exit.";
            System.Windows.MessageBox.Show(errorMessage, "Critical Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }

        /// <summary>
        /// Handles unhandled exceptions on the UI thread.
        /// </summary>
        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            var logger = this.ServiceProvider?.GetService<ILogger<App>>();

            logger?.LogError(e.Exception, "Unhandled dispatcher exception occurred");

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
    }
}


