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
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Coordinates proper disposal of services in priority order.
    /// </summary>
    public class ServiceDisposalCoordinator : IServiceDisposalCoordinator
    {
        private readonly ILogger<ServiceDisposalCoordinator> logger;
        private readonly List<DisposalItem> disposalItems = new();
        private readonly object lockObject = new();
        private bool disposed;

        public bool IsDisposed => this.disposed;

        public ServiceDisposalCoordinator(ILogger<ServiceDisposalCoordinator> logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void RegisterService(string serviceName, IDisposable service, int priority = 0)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                throw new ArgumentException("Service name cannot be null or empty", nameof(serviceName));
            }

            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            lock (this.lockObject)
            {
                if (this.disposed)
                {
                    throw new ObjectDisposedException(nameof(ServiceDisposalCoordinator));
                }

                this.disposalItems.Add(new DisposalItem
                {
                    Name = serviceName,
                    Priority = priority,
                    DisposalAction = () =>
                    {
                        service.Dispose();
                        return Task.CompletedTask;
                    },
                });

                this.logger.LogDebug(
                    "Registered service for disposal: {ServiceName} (Priority: {Priority})",
                    serviceName, priority);
            }
        }

        public void RegisterAsyncService(string serviceName, IAsyncDisposable service, int priority = 0)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                throw new ArgumentException("Service name cannot be null or empty", nameof(serviceName));
            }

            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            lock (this.lockObject)
            {
                if (this.disposed)
                {
                    throw new ObjectDisposedException(nameof(ServiceDisposalCoordinator));
                }

                this.disposalItems.Add(new DisposalItem
                {
                    Name = serviceName,
                    Priority = priority,
                    DisposalAction = async () => await service.DisposeAsync(),
                });

                this.logger.LogDebug(
                    "Registered async service for disposal: {ServiceName} (Priority: {Priority})",
                    serviceName, priority);
            }
        }

        public void RegisterDisposalAction(string actionName, Func<Task> disposalAction, int priority = 0)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                throw new ArgumentException("Action name cannot be null or empty", nameof(actionName));
            }

            if (disposalAction == null)
            {
                throw new ArgumentNullException(nameof(disposalAction));
            }

            lock (this.lockObject)
            {
                if (this.disposed)
                {
                    throw new ObjectDisposedException(nameof(ServiceDisposalCoordinator));
                }

                this.disposalItems.Add(new DisposalItem
                {
                    Name = actionName,
                    Priority = priority,
                    DisposalAction = disposalAction,
                });

                this.logger.LogDebug(
                    "Registered disposal action: {ActionName} (Priority: {Priority})",
                    actionName, priority);
            }
        }

        public async Task DisposeAllAsync()
        {
            if (this.disposed)
            {
                return;
            }

            List<DisposalItem> itemsToDispose;
            lock (this.lockObject)
            {
                if (this.disposed)
                {
                    return;
                }

                // Sort by priority (higher priority disposed first)
                itemsToDispose = this.disposalItems.OrderByDescending(x => x.Priority).ToList();
                this.disposed = true;
            }

            this.logger.LogInformation("Starting coordinated disposal of {Count} services/actions", itemsToDispose.Count);

            foreach (var item in itemsToDispose)
            {
                try
                {
                    this.logger.LogDebug("Disposing: {Name} (Priority: {Priority})", item.Name, item.Priority);
                    await item.DisposalAction();
                    this.logger.LogDebug("Successfully disposed: {Name}", item.Name);
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "Error disposing {Name}: {Error}", item.Name, ex.Message);
                    // Continue with other disposals even if one fails
                }
            }

            this.logger.LogInformation("Coordinated disposal completed");
        }

        public void Dispose()
        {
            this.DisposeAllAsync().GetAwaiter().GetResult();
        }

        private class DisposalItem
        {
            public string Name { get; set; } = string.Empty;

            public int Priority { get; set; }

            public Func<Task> DisposalAction { get; set; } = () => Task.CompletedTask;
        }
    }
}

