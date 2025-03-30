using System;
using System.Collections.Generic;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service locator for dependency injection
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();
        
        /// <summary>
        /// Register service by interface
        /// </summary>
        /// <typeparam name="TInterface">Interface type</typeparam>
        /// <param name="service">Service implementation</param>
        public static void Register<TInterface>(object service)
        {
            var type = typeof(TInterface);
            
            if (_services.ContainsKey(type))
            {
                _services[type] = service;
            }
            else
            {
                _services.Add(type, service);
            }
        }
        
        /// <summary>
        /// Resolve service by interface
        /// </summary>
        /// <typeparam name="TInterface">Interface type</typeparam>
        /// <returns>Service implementation or null if not registered</returns>
        public static TInterface Resolve<TInterface>() where TInterface : class
        {
            var type = typeof(TInterface);
            
            if (_services.TryGetValue(type, out var service))
            {
                return service as TInterface;
            }
            
            return null;
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