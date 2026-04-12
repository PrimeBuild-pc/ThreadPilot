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
    using System.Threading.Tasks;

    /// <summary>
    /// Interface for coordinating proper disposal of services.
    /// </summary>
    public interface IServiceDisposalCoordinator : IDisposable
    {
        /// <summary>
        /// Register a service for coordinated disposal.
        /// </summary>
        void RegisterService(string serviceName, IDisposable service, int priority = 0);

        /// <summary>
        /// Register an async disposable service.
        /// </summary>
        void RegisterAsyncService(string serviceName, IAsyncDisposable service, int priority = 0);

        /// <summary>
        /// Register a custom disposal action.
        /// </summary>
        void RegisterDisposalAction(string actionName, Func<Task> disposalAction, int priority = 0);

        /// <summary>
        /// Dispose all registered services in priority order.
        /// </summary>
        Task DisposeAllAsync();

        /// <summary>
        /// Gets a value indicating whether get disposal status.
        /// </summary>
        bool IsDisposed { get; }
    }
}

