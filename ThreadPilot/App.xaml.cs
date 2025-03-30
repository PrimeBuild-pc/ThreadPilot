using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
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
            
            // Initialize service locator
            ServiceLocator.Initialize();
            
            // Setup unhandled exception handling
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Application.Current.DispatcherUnhandledException += App_DispatcherUnhandledException;
            
            // Create application directories if they don't exist
            EnsureDirectoriesExist();
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogException(e.Exception);
            e.Handled = true;
            
            MessageBox.Show($"An unexpected error occurred: {e.Exception.Message}\n\nThe error has been logged.",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                LogException(ex);
            }
        }
        
        private void LogException(Exception ex)
        {
            try
            {
                string logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                
                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }
                
                string logFile = Path.Combine(logDirectory, $"error_{DateTime.Now:yyyyMMdd}.log");
                string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n\n";
                
                File.AppendAllText(logFile, logMessage);
            }
            catch
            {
                // If logging fails, there's not much we can do
            }
        }
        
        private void EnsureDirectoriesExist()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            
            // Make sure these directories exist
            Directory.CreateDirectory(Path.Combine(baseDir, "Profiles"));
            Directory.CreateDirectory(Path.Combine(baseDir, "Logs"));
            Directory.CreateDirectory(Path.Combine(baseDir, "BundledProfiles"));
        }
    }
}