using System;
using System.Collections.Generic;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Simple service locator for dependency injection
    /// </summary>
    public static class ServiceLocator
    {
        /// <summary>
        /// Dictionary of registered services
        /// </summary>
        private static readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();
        
        /// <summary>
        /// Initialize the service locator with default service implementations
        /// </summary>
        public static void Initialize()
        {
            // Register default service implementations
            Register<INotificationService>(new NotificationService());
            Register<IFileDialogService>(new FileDialogService());
            
            // These services will be implemented later
            // Register<ISystemInfoService>(new SystemInfoService());
            // Register<IProcessService>(new ProcessService());
            // Register<IPowerProfileService>(new PowerProfileService());
        }
        
        /// <summary>
        /// Register a service implementation
        /// </summary>
        /// <typeparam name="T">Service interface type</typeparam>
        /// <param name="implementation">Service implementation</param>
        public static void Register<T>(T implementation) where T : class
        {
            Type type = typeof(T);
            
            if (_services.ContainsKey(type))
            {
                _services[type] = implementation;
            }
            else
            {
                _services.Add(type, implementation);
            }
        }
        
        /// <summary>
        /// Get a service implementation
        /// </summary>
        /// <typeparam name="T">Service interface type</typeparam>
        /// <returns>Service implementation</returns>
        public static T Get<T>() where T : class
        {
            Type type = typeof(T);
            
            if (_services.TryGetValue(type, out object service))
            {
                return (T)service;
            }
            
            throw new InvalidOperationException($"Service of type {type.Name} is not registered.");
        }
        
        /// <summary>
        /// Check if a service is registered
        /// </summary>
        /// <typeparam name="T">Service interface type</typeparam>
        /// <returns>True if the service is registered</returns>
        public static bool IsRegistered<T>() where T : class
        {
            return _services.ContainsKey(typeof(T));
        }
        
        /// <summary>
        /// Unregister a service
        /// </summary>
        /// <typeparam name="T">Service interface type</typeparam>
        public static void Unregister<T>() where T : class
        {
            Type type = typeof(T);
            
            if (_services.ContainsKey(type))
            {
                _services.Remove(type);
            }
        }
    }
}