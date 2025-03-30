using System;
using System.Collections.Generic;

namespace ThreadPilot.Services
{
    /// <summary>
    /// A simple service locator for dependency injection
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();
        
        /// <summary>
        /// Registers a service interface with its implementation
        /// </summary>
        /// <typeparam name="TInterface">The interface type</typeparam>
        /// <typeparam name="TImplementation">The implementation type</typeparam>
        /// <exception cref="ArgumentException">Thrown if the implementation does not implement the interface</exception>
        public static void Register<TInterface, TImplementation>() 
            where TInterface : class
            where TImplementation : class, TInterface, new()
        {
            if (!typeof(TInterface).IsAssignableFrom(typeof(TImplementation)))
            {
                throw new ArgumentException($"{typeof(TImplementation).Name} does not implement {typeof(TInterface).Name}");
            }
            
            _services[typeof(TInterface)] = new TImplementation();
        }
        
        /// <summary>
        /// Registers a specific instance as the implementation for an interface
        /// </summary>
        /// <typeparam name="TInterface">The interface type</typeparam>
        /// <param name="implementation">The implementation instance</param>
        public static void RegisterInstance<TInterface>(TInterface implementation) where TInterface : class
        {
            _services[typeof(TInterface)] = implementation;
        }
        
        /// <summary>
        /// Gets the service implementation for the specified interface
        /// </summary>
        /// <typeparam name="T">The interface type</typeparam>
        /// <returns>The service implementation</returns>
        /// <exception cref="InvalidOperationException">Thrown if the service is not registered</exception>
        public static T Get<T>() where T : class
        {
            if (!_services.TryGetValue(typeof(T), out var service))
            {
                throw new InvalidOperationException($"Service of type {typeof(T).Name} is not registered");
            }
            
            return (T)service;
        }
        
        /// <summary>
        /// Clears all registered services
        /// </summary>
        public static void Clear()
        {
            _services.Clear();
        }
    }
}