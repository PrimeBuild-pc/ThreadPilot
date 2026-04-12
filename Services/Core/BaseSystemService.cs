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
namespace ThreadPilot.Services.Core
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Base implementation for system services with common functionality.
    /// </summary>
    public abstract class BaseSystemService : ISystemService, IDisposable
    {
        protected readonly ILogger Logger;
        private bool isAvailable;
        private bool disposed;

        public bool IsAvailable
        {
            get => this.isAvailable;
            protected set
            {
                if (this.isAvailable != value)
                {
                    this.isAvailable = value;
                    this.OnAvailabilityChanged(value);
                }
            }
        }

        public event EventHandler<ServiceAvailabilityChangedEventArgs>? AvailabilityChanged;

        protected BaseSystemService(ILogger logger)
        {
            this.Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public virtual async Task InitializeAsync()
        {
            try
            {
                this.Logger.LogInformation("Initializing {ServiceType}", this.GetType().Name);
                await this.InitializeServiceAsync();
                this.IsAvailable = true;
                this.Logger.LogInformation("{ServiceType} initialized successfully", this.GetType().Name);
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "Failed to initialize {ServiceType}", this.GetType().Name);
                this.IsAvailable = false;
                throw;
            }
        }

        public virtual async Task DisposeAsync()
        {
            if (this.disposed)
            {
                return;
            }

            try
            {
                this.Logger.LogInformation("Disposing {ServiceType}", this.GetType().Name);
                await this.DisposeServiceAsync();
                this.IsAvailable = false;
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "Error disposing {ServiceType}", this.GetType().Name);
            }
            finally
            {
                this.disposed = true;
            }
        }

        protected abstract Task InitializeServiceAsync();

        protected abstract Task DisposeServiceAsync();

        protected virtual void OnAvailabilityChanged(bool isAvailable, string? reason = null)
        {
            this.AvailabilityChanged?.Invoke(this, new ServiceAvailabilityChangedEventArgs(isAvailable, reason));
        }

        protected void ThrowIfDisposed()
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException(this.GetType().Name);
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed && disposing)
            {
                _ = Task.Run(async () => await this.DisposeAsync());
            }
        }
    }
}

