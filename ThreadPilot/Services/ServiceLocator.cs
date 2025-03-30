using System;
using System.Collections.Generic;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service locator for dependency injection
    /// </summary>
    public static class ServiceLocator
    {
        // Dictionary of services
        private static readonly Dictionary<Type, object> Services = new Dictionary<Type, object>();
        
        /// <summary>
        /// Initialize the service locator
        /// </summary>
        public static void Initialize()
        {
            // Register services
            RegisterService<INotificationService>(new NotificationService());
            RegisterService<IFileDialogService>(new FileDialogService());
            RegisterService<ISystemInfoService>(new SystemInfoService());
            RegisterService<IProcessService>(new ProcessService());
            RegisterService<IPowerProfileService>(new PowerProfileService());
        }
        
        /// <summary>
        /// Register a service
        /// </summary>
        public static void RegisterService<T>(object service)
        {
            Services[typeof(T)] = service;
        }
        
        /// <summary>
        /// Get a service
        /// </summary>
        public static T Get<T>()
        {
            if (Services.TryGetValue(typeof(T), out var service))
            {
                return (T)service;
            }
            
            throw new InvalidOperationException($"Service {typeof(T).Name} not registered");
        }
    }
}