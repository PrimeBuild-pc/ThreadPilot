using System;
using System.Collections.Generic;

namespace ThreadPilot.Services
{
    /// <summary>
    /// A simple service locator for managing application services
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> Services = new Dictionary<Type, object>();
        private static bool _isInitialized;
        
        /// <summary>
        /// Initialize the service locator with all required services
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized)
            {
                return;
            }
            
            // Register all services
            RegisterService<INotificationService>(new NotificationService());
            RegisterService<IFileDialogService>(new FileDialogService());
            RegisterService<ISystemInfoService>(new SystemInfoService());
            RegisterService<IProcessService>(new ProcessService());
            RegisterService<IPowerProfileService>(new PowerProfileService());
            
            _isInitialized = true;
        }
        
        /// <summary>
        /// Register a service
        /// </summary>
        /// <typeparam name="T">The service interface type</typeparam>
        /// <param name="service">The service implementation</param>
        public static void RegisterService<T>(T service) where T : class
        {
            Services[typeof(T)] = service;
        }
        
        /// <summary>
        /// Get a registered service
        /// </summary>
        /// <typeparam name="T">The service interface type</typeparam>
        /// <returns>The service implementation</returns>
        public static T GetService<T>() where T : class
        {
            if (Services.TryGetValue(typeof(T), out var service))
            {
                return (T)service;
            }
            
            throw new InvalidOperationException($"Service of type {typeof(T).Name} is not registered");
        }
    }
}