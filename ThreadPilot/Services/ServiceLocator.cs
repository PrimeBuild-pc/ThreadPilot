using System;
using System.Collections.Generic;

namespace ThreadPilot
{
    /// <summary>
    /// Service locator for dependency injection
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();
        
        /// <summary>
        /// Initialize all services
        /// </summary>
        public static void Initialize()
        {
            // Register services
            RegisterService<IProcessMonitoringService>(new ProcessMonitoringService());
            RegisterService<ISystemInfoService>(new SystemInfoService());
            RegisterService<IFileDialogService>(new FileDialogService());
            RegisterService<IPowerProfileService>(new PowerProfileService());
            RegisterService<IBundledPowerProfilesService>(new BundledPowerProfilesService());
            RegisterService<INotificationService>(new NotificationService());
            RegisterService<ISettingsService>(new SettingsService());
            
            // Register view models
            RegisterService<ProcessesViewModel>(new ProcessesViewModel());
            RegisterService<PowerProfilesViewModel>(new PowerProfilesViewModel());
            RegisterService<DashboardViewModel>(new DashboardViewModel());
            RegisterService<SettingsViewModel>(new SettingsViewModel());
            RegisterService<MainViewModel>(new MainViewModel());
        }
        
        /// <summary>
        /// Register a service
        /// </summary>
        public static void RegisterService<T>(T service)
        {
            _services[typeof(T)] = service;
        }
        
        /// <summary>
        /// Get a service
        /// </summary>
        public static T GetService<T>()
        {
            if (_services.TryGetValue(typeof(T), out var service))
            {
                return (T)service;
            }
            
            throw new InvalidOperationException($"Service of type {typeof(T).Name} is not registered");
        }
    }
}