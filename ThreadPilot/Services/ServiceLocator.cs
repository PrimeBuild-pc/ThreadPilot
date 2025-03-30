using System;
using System.Collections.Generic;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service locator pattern implementation for dependency injection
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();
        
        /// <summary>
        /// Register a service
        /// </summary>
        /// <typeparam name="TInterface">Interface type</typeparam>
        /// <typeparam name="TImplementation">Implementation type</typeparam>
        /// <param name="instance">Optional existing instance</param>
        public static void Register<TInterface, TImplementation>(TImplementation? instance = null)
            where TInterface : class
            where TImplementation : class, TInterface
        {
            // If no instance is provided, create a new one
            if (instance == null)
            {
                instance = Activator.CreateInstance<TImplementation>();
            }
            
            _services[typeof(TInterface)] = instance;
        }
        
        /// <summary>
        /// Register a singleton service instance
        /// </summary>
        /// <typeparam name="TInterface">Interface type</typeparam>
        /// <param name="instance">Instance to register</param>
        public static void RegisterInstance<TInterface>(TInterface instance)
            where TInterface : class
        {
            _services[typeof(TInterface)] = instance;
        }
        
        /// <summary>
        /// Get a service
        /// </summary>
        /// <typeparam name="T">Service type</typeparam>
        /// <returns>Service instance</returns>
        public static T Get<T>() where T : class
        {
            var type = typeof(T);
            
            if (!_services.TryGetValue(type, out var service))
            {
                throw new InvalidOperationException($"Service of type {type.Name} is not registered");
            }
            
            return (T)service;
        }
        
        /// <summary>
        /// Check if a service is registered
        /// </summary>
        /// <typeparam name="T">Service type</typeparam>
        /// <returns>True if registered, false otherwise</returns>
        public static bool IsRegistered<T>() where T : class
        {
            return _services.ContainsKey(typeof(T));
        }
        
        /// <summary>
        /// Remove a service
        /// </summary>
        /// <typeparam name="T">Service type</typeparam>
        public static void Unregister<T>() where T : class
        {
            _services.Remove(typeof(T));
        }
        
        /// <summary>
        /// Clear all registered services
        /// </summary>
        public static void Clear()
        {
            _services.Clear();
        }
    }
}