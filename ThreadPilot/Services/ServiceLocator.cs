using System;
using System.Collections.Generic;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service locator for dependency injection.
    /// </summary>
    public class ServiceLocator
    {
        private static readonly Lazy<ServiceLocator> _instance = new(() => new ServiceLocator());
        private readonly Dictionary<Type, object> _services = new();

        /// <summary>
        /// Gets the instance of the service locator.
        /// </summary>
        public static ServiceLocator Instance => _instance.Value;

        /// <summary>
        /// Registers a service.
        /// </summary>
        /// <typeparam name="TService">The service type.</typeparam>
        /// <param name="service">The service instance.</param>
        public void Register<TService>(TService service) where TService : class
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            _services[typeof(TService)] = service;
        }

        /// <summary>
        /// Gets a service.
        /// </summary>
        /// <typeparam name="TService">The service type.</typeparam>
        /// <returns>The service instance.</returns>
        public TService Get<TService>() where TService : class
        {
            if (_services.TryGetValue(typeof(TService), out var service))
            {
                return (TService)service;
            }

            throw new KeyNotFoundException($"Service of type {typeof(TService).Name} is not registered.");
        }

        /// <summary>
        /// Checks if a service is registered.
        /// </summary>
        /// <typeparam name="TService">The service type.</typeparam>
        /// <returns>True if the service is registered, false otherwise.</returns>
        public bool IsRegistered<TService>() where TService : class
        {
            return _services.ContainsKey(typeof(TService));
        }

        /// <summary>
        /// Unregisters a service.
        /// </summary>
        /// <typeparam name="TService">The service type.</typeparam>
        public void Unregister<TService>() where TService : class
        {
            if (_services.ContainsKey(typeof(TService)))
            {
                _services.Remove(typeof(TService));
            }
        }

        /// <summary>
        /// Clears all registered services.
        /// </summary>
        public void Clear()
        {
            _services.Clear();
        }
    }
}