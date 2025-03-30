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
            InitializeServices();
            
            // Check and create application directories
            EnsureApplicationDirectories();
        }
        
        private void InitializeServices()
        {
            // Initialize service locator with services
            var fileDialogService = new FileDialogService();
            ServiceLocator.RegisterService<IFileDialogService>(fileDialogService);
            
            var powerProfileService = new PowerProfileService();
            ServiceLocator.RegisterService<IPowerProfileService>(powerProfileService);
            
            var bundledPowerProfilesService = new BundledPowerProfilesService();
            ServiceLocator.RegisterService<IBundledPowerProfilesService>(bundledPowerProfilesService);
            
            // Additional services will be registered here as needed
        }
        
        private void EnsureApplicationDirectories()
        {
            try
            {
                // Get application data directory
                string appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ThreadPilot");
                
                // Create main application directory if it doesn't exist
                if (!Directory.Exists(appDataPath))
                {
                    Directory.CreateDirectory(appDataPath);
                }
                
                // Create profiles directory
                string profilesPath = Path.Combine(appDataPath, "Profiles");
                if (!Directory.Exists(profilesPath))
                {
                    Directory.CreateDirectory(profilesPath);
                }
                
                // Create settings directory
                string settingsPath = Path.Combine(appDataPath, "Settings");
                if (!Directory.Exists(settingsPath))
                {
                    Directory.CreateDirectory(settingsPath);
                }
                
                // Create logs directory
                string logsPath = Path.Combine(appDataPath, "Logs");
                if (!Directory.Exists(logsPath))
                {
                    Directory.CreateDirectory(logsPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating application directories: {ex.Message}", 
                    "ThreadPilot", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // Log the error
            string errorMessage = $"An unhandled exception occurred: {e.Exception.Message}";
            LogError(errorMessage, e.Exception);
            
            // Show error message to user
            MessageBox.Show($"An unexpected error occurred. Please restart the application.\n\nError details: {e.Exception.Message}",
                "ThreadPilot Error", MessageBoxButton.OK, MessageBoxImage.Error);
            
            // Mark as handled to prevent application crash
            e.Handled = true;
        }
        
        private void LogError(string message, Exception ex)
        {
            try
            {
                string logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ThreadPilot", "Logs", "error.log");
                
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string errorDetails = $"{timestamp} - {message}\n{ex}\n\n";
                
                File.AppendAllText(logPath, errorDetails);
            }
            catch
            {
                // If logging itself fails, there's not much we can do
            }
        }
    }
}