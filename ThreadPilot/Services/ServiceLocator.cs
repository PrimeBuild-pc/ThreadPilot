using System;
using System.Collections.Generic;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service locator singleton for dependency injection
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();
        
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
        /// Register a service
        /// </summary>
        /// <typeparam name="T">Service type</typeparam>
        /// <param name="implementation">Service implementation</param>
        public static void Register<T>(T implementation) where T : class
        {
            _services[typeof(T)] = implementation ?? throw new ArgumentNullException(nameof(implementation));
        }
        
        /// <summary>
        /// Get a service
        /// </summary>
        /// <typeparam name="T">Service type</typeparam>
        /// <returns>Service implementation</returns>
        public static T Get<T>() where T : class
        {
            if (_services.TryGetValue(typeof(T), out var service))
            {
                return (T)service;
            }
            
            throw new InvalidOperationException($"Service of type {typeof(T).Name} is not registered.");
        }
    }
}