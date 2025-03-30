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
        /// <summary>
        /// Current application version
        /// </summary>
        public static readonly Version Version = new Version(1, 0, 0, 0);
        
        /// <summary>
        /// Application data directory
        /// </summary>
        public static readonly string AppDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ThreadPilot");
        
        /// <summary>
        /// Profile directory
        /// </summary>
        public static readonly string ProfileDirectory = Path.Combine(AppDataDirectory, "Profiles");
        
        /// <summary>
        /// Log directory
        /// </summary>
        public static readonly string LogDirectory = Path.Combine(AppDataDirectory, "Logs");
        
        /// <summary>
        /// Settings file path
        /// </summary>
        public static readonly string SettingsFilePath = Path.Combine(AppDataDirectory, "settings.json");
        
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Ensure application directories exist
            EnsureDirectoriesExist();
            
            // Register services
            RegisterServices();
            
            // Set up unhandled exception handling
            SetupExceptionHandling();
        }
        
        /// <summary>
        /// Ensures application directories exist
        /// </summary>
        private void EnsureDirectoriesExist()
        {
            var directories = new[]
            {
                AppDataDirectory,
                ProfileDirectory,
                LogDirectory
            };
            
            foreach (var directory in directories)
            {
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }
        }
        
        /// <summary>
        /// Registers application services
        /// </summary>
        private void RegisterServices()
        {
            // Register services in the service locator
            ServiceLocator.Register<INotificationService>(new NotificationService());
            ServiceLocator.Register<IFileDialogService>(new FileDialogService());
            ServiceLocator.Register<ISystemInfoService>(new SystemInfoService());
            ServiceLocator.Register<IProcessService>(new ProcessService());
            ServiceLocator.Register<IPowerProfileService>(new PowerProfileService());
        }
        
        /// <summary>
        /// Sets up global exception handling
        /// </summary>
        private void SetupExceptionHandling()
        {
            // Handle UI thread exceptions
            DispatcherUnhandledException += (sender, e) =>
            {
                HandleException(e.Exception);
                e.Handled = true;
            };
            
            // Handle non-UI thread exceptions
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                if (e.ExceptionObject is Exception exception)
                {
                    HandleException(exception);
                }
            };
            
            // Handle task exceptions
            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                HandleException(e.Exception);
                e.SetObserved();
            };
        }
        
        /// <summary>
        /// Handles exceptions
        /// </summary>
        /// <param name="exception">Exception</param>
        private void HandleException(Exception exception)
        {
            // Log the exception
            LogException(exception);
            
            // Show error message
            var notificationService = ServiceLocator.Resolve<INotificationService>();
            notificationService?.ShowError($"An error occurred: {exception.Message}");
        }
        
        /// <summary>
        /// Logs exceptions to file
        /// </summary>
        /// <param name="exception">Exception</param>
        private void LogException(Exception exception)
        {
            try
            {
                var logFile = Path.Combine(LogDirectory, $"error_{DateTime.Now:yyyyMMdd}.log");
                var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {exception.GetType().Name}: {exception.Message}\r\n{exception.StackTrace}\r\n\r\n";
                
                File.AppendAllText(logFile, logMessage);
            }
            catch
            {
                // Ignore errors in the error handler
            }
        }
    }
}