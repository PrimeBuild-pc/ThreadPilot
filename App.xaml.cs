using System;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ThreadPilot.Services;
using ThreadPilot.ViewModels;

namespace ThreadPilot
{
    public partial class App : System.Windows.Application
    {
        private Mutex? _singleInstanceMutex;
        public IServiceProvider ServiceProvider { get; private set; }

        public App()
        {
            ServiceCollection services = new ServiceCollection();

            // Use the new centralized service configuration
            services.ConfigureApplicationServices();

            ServiceProvider = services.BuildServiceProvider();

            // Validate service configuration
            ServiceConfiguration.ValidateServiceConfiguration(ServiceProvider);
        }



        protected override void OnStartup(StartupEventArgs e)
        {
            // Enforce single-instance: bail out if another instance is already running
            bool createdNew;
            _singleInstanceMutex = new Mutex(initiallyOwned: true, name: "Global\\ThreadPilot_SingleInstance", createdNew: out createdNew);
            if (!createdNew)
            {
                System.Windows.MessageBox.Show(
                    "ThreadPilot è già in esecuzione.",
                    "Istanza già aperta",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                Shutdown();
                return;
            }

            // Set up global exception handlers first
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            DispatcherUnhandledException += OnDispatcherUnhandledException;

            // Check elevation status first
            var elevationService = ServiceProvider.GetRequiredService<IElevationService>();
            var logger = ServiceProvider.GetRequiredService<ILogger<App>>();

            if (!elevationService.IsRunningAsAdministrator())
            {
                logger.LogWarning("Application is not running with administrator privileges");
                ShowElevationRequiredMessage();

                // Allow the application to continue in read-only mode
                // The UI will handle showing elevation prompts as needed
            }
            else
            {
                logger.LogInformation("Application is running with administrator privileges");
            }

            base.OnStartup(e);

            // Parse command line arguments
            bool startMinimized = false;
            bool isAutostart = false;
            bool isTestMode = false;

            foreach (var arg in e.Args)
            {
                switch (arg.ToLowerInvariant())
                {
                    case "--test":
                        isTestMode = true;
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
                }
            }

            // Check for test mode
            if (isTestMode)
            {
                // Run in console test mode
                AllocConsole();
                _ = Task.Run(async () =>
                {
                    await TestRunner.RunTests();
                    Dispatcher.Invoke(() => Shutdown());
                });
                return;
            }

            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();

            // Handle startup behavior with comprehensive error handling
            try
            {
                logger.LogInformation("Attempting to show main window...");

                // Ensure the window is properly initialized
                if (mainWindow == null)
                {
                    throw new InvalidOperationException("MainWindow could not be created");
                }

                // Show the window with explicit visibility settings
                mainWindow.Visibility = Visibility.Visible;
                mainWindow.WindowState = (startMinimized || isAutostart)
                    ? WindowState.Minimized
                    : WindowState.Normal;
                mainWindow.Show();
                if (mainWindow.WindowState != WindowState.Minimized)
                {
                    mainWindow.Activate();
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
                Shutdown(1);
                return;
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_singleInstanceMutex != null)
            {
                try
                {
                    _singleInstanceMutex.ReleaseMutex();
                }
                catch
                {
                    // Ignore; we just want to clean up quietly
                }
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
            }

            base.OnExit(e);
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        /// <summary>
        /// Shows a message to the user about elevation requirements
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
        /// Handles unhandled exceptions in the application domain
        /// </summary>
        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            var logger = ServiceProvider?.GetService<ILogger<App>>();

            logger?.LogCritical(exception, "Unhandled exception occurred");

            var errorMessage = $"A critical error occurred:\n\n{exception?.Message}\n\nThe application will now exit.";
            System.Windows.MessageBox.Show(errorMessage, "Critical Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }

        /// <summary>
        /// Handles unhandled exceptions on the UI thread
        /// </summary>
        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            var logger = ServiceProvider?.GetService<ILogger<App>>();

            logger?.LogError(e.Exception, "Unhandled dispatcher exception occurred");

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
        }
    }
}

