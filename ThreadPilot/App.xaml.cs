using System;
using System.Windows;
using System.Threading.Tasks;
using System.IO;
using System.Windows.Media;
using ThreadPilot.Services;

namespace ThreadPilot
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        // Service container for the application
        public static ServiceContainer Services { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Initialize the service container
            InitializeServices();
            
            // Ensure the required directories are created
            EnsureApplicationDirectories();
            
            // Check if the application is running with admin rights
            CheckAdminRights();
        }

        private void InitializeServices()
        {
            // Create and configure all services
            Services = new ServiceContainer();
            
            // Register all services
            Services.Register<INotificationService>(new NotificationService());
            Services.Register<IFileDialogService>(new FileDialogService());
            Services.Register<IPowerProfileService>(new PowerProfileService());
            Services.Register<IBundledPowerProfilesService>(new BundledPowerProfilesService());
            Services.Register<IProcessService>(new ProcessService());
            Services.Register<IAffinityService>(new AffinityService());
            Services.Register<ISystemOptimizationService>(new SystemOptimizationService());
            Services.Register<ISettingsService>(new SettingsService());
            
            // Log service registration completion
            Console.WriteLine("All services have been registered.");
        }

        private void EnsureApplicationDirectories()
        {
            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ThreadPilot");
            
            // Create application directories if they don't exist
            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }
            
            string profilesDir = Path.Combine(appDataPath, "Profiles");
            if (!Directory.Exists(profilesDir))
            {
                Directory.CreateDirectory(profilesDir);
            }
            
            string settingsDir = Path.Combine(appDataPath, "Settings");
            if (!Directory.Exists(settingsDir))
            {
                Directory.CreateDirectory(settingsDir);
            }
            
            string logsDir = Path.Combine(appDataPath, "Logs");
            if (!Directory.Exists(logsDir))
            {
                Directory.CreateDirectory(logsDir);
            }
        }

        private void CheckAdminRights()
        {
            bool isAdmin = new AdminRightsChecker().IsRunningAsAdmin();
            
            if (!isAdmin)
            {
                MessageBox.Show(
                    "ThreadPilot is running without administrative privileges. " +
                    "Some features may not work correctly.\n\n" +
                    "To access all features, please restart the application by right-clicking it and selecting 'Run as administrator'.",
                    "Limited Functionality",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        
        // Simple service container implementation
        public class ServiceContainer
        {
            private readonly System.Collections.Generic.Dictionary<Type, object> _services = 
                new System.Collections.Generic.Dictionary<Type, object>();

            public void Register<T>(T service) where T : class
            {
                _services[typeof(T)] = service;
            }

            public T Resolve<T>() where T : class
            {
                if (_services.TryGetValue(typeof(T), out object service))
                {
                    return (T)service;
                }

                throw new InvalidOperationException($"Service of type {typeof(T).Name} is not registered.");
            }
        }
        
        // Helper class to check admin rights
        private class AdminRightsChecker
        {
            public bool IsRunningAsAdmin()
            {
                try
                {
                    System.Security.Principal.WindowsIdentity identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                    System.Security.Principal.WindowsPrincipal principal = new System.Security.Principal.WindowsPrincipal(identity);
                    return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
                }
                catch
                {
                    return false;
                }
            }
        }
    }
}