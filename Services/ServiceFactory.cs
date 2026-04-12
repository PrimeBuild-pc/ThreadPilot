/*
 * ThreadPilot - Advanced Windows Process and Power Plan Manager
 * Copyright (C) 2025 Prime Build
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, version 3 only.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
namespace ThreadPilot.Services
{
    using System;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using ThreadPilot.Services.Core;

    /// <summary>
    /// Factory for creating and managing service instances with proper dependency resolution.
    /// </summary>
    public interface IServiceFactory
    {
        /// <summary>
        /// Create a service instance of the specified type.
        /// </summary>
        T CreateService<T>()
            where T : class;

        /// <summary>
        /// Create a service instance with additional parameters.
        /// </summary>
        T CreateService<T>(params object[] parameters)
            where T : class;

        /// <summary>
        /// Get or create a singleton service instance.
        /// </summary>
        T GetSingletonService<T>()
            where T : class;

        /// <summary>
        /// Initialize all core services.
        /// </summary>
        Task InitializeAllServicesAsync();

        /// <summary>
        /// Dispose all managed services.
        /// </summary>
        Task DisposeAllServicesAsync();
    }

    /// <summary>
    /// Implementation of service factory with dependency injection support.
    /// </summary>
    public class ServiceFactory : IServiceFactory, IDisposable
    {
        private readonly IServiceProvider serviceProvider;
        private readonly ILogger<ServiceFactory> logger;
        private readonly Dictionary<Type, object> singletonInstances = new();
        private readonly List<ISystemService> managedServices = new();
        private bool disposed;

        public ServiceFactory(IServiceProvider serviceProvider, ILogger<ServiceFactory> logger)
        {
            this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public T CreateService<T>()
            where T : class
        {
            try
            {
                var service = this.serviceProvider.GetRequiredService<T>();

                // Track system services for lifecycle management
                if (service is ISystemService systemService)
                {
                    this.managedServices.Add(systemService);
                }

                return service;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to create service of type {ServiceType}", typeof(T).Name);
                throw;
            }
        }

        public T CreateService<T>(params object[] parameters)
            where T : class
        {
            try
            {
                // For services with additional parameters, use ActivatorUtilities
                var service = ActivatorUtilities.CreateInstance<T>(this.serviceProvider, parameters);

                if (service is ISystemService systemService)
                {
                    this.managedServices.Add(systemService);
                }

                return service;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to create service of type {ServiceType} with parameters", typeof(T).Name);
                throw;
            }
        }

        public T GetSingletonService<T>()
            where T : class
        {
            var serviceType = typeof(T);

            if (this.singletonInstances.TryGetValue(serviceType, out var existingInstance))
            {
                return (T)existingInstance;
            }

            var newInstance = this.CreateService<T>();
            this.singletonInstances[serviceType] = newInstance;

            return newInstance;
        }

        public async Task InitializeAllServicesAsync()
        {
            this.logger.LogInformation("Initializing all managed services");

            var initializationTasks = this.managedServices.Select(async service =>
            {
                try
                {
                    await service.InitializeAsync();
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "Failed to initialize service {ServiceType}", service.GetType().Name);
                    throw;
                }
            });

            await Task.WhenAll(initializationTasks);
            this.logger.LogInformation("All managed services initialized successfully");
        }

        public async Task DisposeAllServicesAsync()
        {
            this.logger.LogInformation("Disposing all managed services");

            var disposalTasks = this.managedServices.Select(async service =>
            {
                try
                {
                    await service.DisposeAsync();
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "Error disposing service {ServiceType}", service.GetType().Name);
                }
            });

            await Task.WhenAll(disposalTasks);
            this.managedServices.Clear();
            this.singletonInstances.Clear();

            this.logger.LogInformation("All managed services disposed");
        }

        public void Dispose()
        {
            if (!this.disposed)
            {
                _ = Task.Run(async () => await this.DisposeAllServicesAsync());
                this.disposed = true;
            }
        }
    }
}

