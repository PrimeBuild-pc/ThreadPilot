using System;
using System.IO;
using System.Windows;
using ThreadPilot.Services;

namespace ThreadPilot
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Create necessary directories
            CreateApplicationDirectories();

            // Register services
            RegisterServices();

            // Set up unhandled exception handling
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;

            // Initialize logging
            InitializeLogging();
        }

        private void CreateApplicationDirectories()
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ThreadPilot");

            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }

            var directories = new[]
            {
                Path.Combine(appDataPath, "Logs"),
                Path.Combine(appDataPath, "PowerProfiles"),
                Path.Combine(appDataPath, "Settings")
            };

            foreach (var directory in directories)
            {
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }
        }

        private void RegisterServices()
        {
            // Registration of services will be implemented here
            // For example:
            // ServiceLocator.Instance.Register<ISystemInfoService>(new SystemInfoService());
            // ServiceLocator.Instance.Register<IProcessService>(new ProcessService());
            // ServiceLocator.Instance.Register<IPowerProfileService>(new PowerProfileService());
            // ServiceLocator.Instance.Register<INotificationService>(new NotificationService());
            // ServiceLocator.Instance.Register<IFileDialogService>(new FileDialogService());
        }

        private void InitializeLogging()
        {
            // Logging initialization will be implemented here
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            LogUnhandledException(e.ExceptionObject as Exception);
        }

        private void Current_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            LogUnhandledException(e.Exception);
            e.Handled = true;
        }

        private void LogUnhandledException(Exception? exception)
        {
            if (exception == null)
            {
                return;
            }

            try
            {
                var logDirectoryPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ThreadPilot",
                    "Logs");

                if (!Directory.Exists(logDirectoryPath))
                {
                    Directory.CreateDirectory(logDirectoryPath);
                }

                var logFilePath = Path.Combine(logDirectoryPath, $"Error_{DateTime.Now:yyyyMMdd_HHmmss}.log");

                using var writer = new StreamWriter(logFilePath, false);
                writer.WriteLine($"Time: {DateTime.Now}");
                writer.WriteLine($"Message: {exception.Message}");
                writer.WriteLine($"Stack Trace: {exception.StackTrace}");

                if (exception.InnerException != null)
                {
                    writer.WriteLine($"Inner Exception: {exception.InnerException.Message}");
                    writer.WriteLine($"Inner Stack Trace: {exception.InnerException.StackTrace}");
                }
            }
            catch
            {
                // If logging fails, we can't do much more
            }

            MessageBox.Show($"An unexpected error occurred: {exception.Message}\n\nPlease check the logs for more details.",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}