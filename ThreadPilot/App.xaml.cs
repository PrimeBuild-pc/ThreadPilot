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
        private readonly string _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        private readonly string _powerProfilesDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PowerProfiles");
        
        /// <summary>
        /// Application startup handler
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Create necessary directories if they don't exist
            Directory.CreateDirectory(_logDirectory);
            Directory.CreateDirectory(_powerProfilesDirectory);
            
            // Register services
            ServiceLocator.Register<IProcessService, ProcessService>();
            ServiceLocator.Register<IPowerProfileService, PowerProfileService>();
            ServiceLocator.Register<IFileDialogService, FileDialogService>();
            ServiceLocator.Register<INotificationService, NotificationService>();
            ServiceLocator.Register<ISystemInfoService, SystemInfoService>();
            
            // Set up global exception handling
            Current.DispatcherUnhandledException += CurrentOnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
        }
        
        /// <summary>
        /// Handle unhandled exceptions in the dispatcher
        /// </summary>
        private void CurrentOnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            LogException(e.Exception);
            MessageBox.Show($"An unexpected error occurred: {e.Exception.Message}\n\nThe error has been logged.", 
                "ThreadPilot Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
        
        /// <summary>
        /// Handle unhandled exceptions in the current domain
        /// </summary>
        private void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception exception)
            {
                LogException(exception);
                MessageBox.Show($"A critical error occurred: {exception.Message}\n\nThe application will now terminate.", 
                    "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// Log exceptions to a file
        /// </summary>
        private void LogException(Exception exception)
        {
            try
            {
                string filePath = Path.Combine(_logDirectory, $"Error_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                using (StreamWriter writer = new StreamWriter(filePath))
                {
                    writer.WriteLine($"Exception Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    writer.WriteLine($"Exception Type: {exception.GetType().FullName}");
                    writer.WriteLine($"Exception Message: {exception.Message}");
                    writer.WriteLine($"Stack Trace: {exception.StackTrace}");
                    
                    if (exception.InnerException != null)
                    {
                        writer.WriteLine("\nInner Exception:");
                        writer.WriteLine($"Type: {exception.InnerException.GetType().FullName}");
                        writer.WriteLine($"Message: {exception.InnerException.Message}");
                        writer.WriteLine($"Stack Trace: {exception.InnerException.StackTrace}");
                    }
                }
            }
            catch
            {
                // If logging fails, we can't do much but try to show a message box
                MessageBox.Show("Failed to log exception details to file.", "Logging Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}