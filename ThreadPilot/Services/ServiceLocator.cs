using System;
using System.Collections.Generic;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service locator
    /// </summary>
    public static class ServiceLocator
    {
        // Services dictionary
        private static readonly Dictionary<Type, object> Services = new Dictionary<Type, object>();
        
        /// <summary>
        /// Initialize services
        /// </summary>
        public static void Initialize()
        {
            // Register services
            Register<INotificationService>(new NotificationService());
            Register<IFileDialogService>(new FileDialogService());
            Register<ISystemInfoService>(new SystemInfoService());
            Register<IProcessService>(new ProcessService());
            Register<IPowerProfileService>(new PowerProfileService());
        }
        
        /// <summary>
        /// Cleanup services
        /// </summary>
        public static void Cleanup()
        {
            // Dispose services
            foreach (var service in Services.Values)
            {
                if (service is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            
            // Clear services
            Services.Clear();
        }
        
        /// <summary>
        /// Register service
        /// </summary>
        public static void Register<T>(T service) where T : class
        {
            Services[typeof(T)] = service;
        }
        
        /// <summary>
        /// Get service
        /// </summary>
        public static T Get<T>() where T : class
        {
            if (Services.TryGetValue(typeof(T), out var service))
            {
                return (T)service;
            }
            
            throw new InvalidOperationException($"Service {typeof(T).Name} not registered");
        }
    }
}