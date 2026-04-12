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
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Implementation of service health monitoring.
    /// </summary>
    public class ServiceHealthMonitor : IServiceHealthMonitor, IDisposable
    {
        private readonly ILogger<ServiceHealthMonitor> logger;
        private readonly ConcurrentDictionary<string, Func<Task<ServiceHealthResult>>> healthChecks = new();
        private readonly ConcurrentDictionary<string, ServiceHealthResult> lastResults = new();
        private readonly object lockObject = new();
        private bool disposed;

        public event EventHandler<ServiceHealthChangedEventArgs>? ServiceHealthChanged;

        public ServiceHealthMonitor(ILogger<ServiceHealthMonitor> logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void RegisterService(string serviceName, Func<Task<ServiceHealthResult>> healthCheck)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                throw new ArgumentException("Service name cannot be null or empty", nameof(serviceName));
            }

            if (healthCheck == null)
            {
                throw new ArgumentNullException(nameof(healthCheck));
            }

            this.healthChecks.AddOrUpdate(serviceName, healthCheck, (key, oldValue) => healthCheck);
            this.logger.LogInformation("Registered health check for service: {ServiceName}", serviceName);
        }

        public void UnregisterService(string serviceName)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                return;
            }

            this.healthChecks.TryRemove(serviceName, out _);
            this.lastResults.TryRemove(serviceName, out _);
            this.logger.LogInformation("Unregistered health check for service: {ServiceName}", serviceName);
        }

        public async Task<ServiceHealthResult> CheckServiceHealthAsync(string serviceName)
        {
            if (!this.healthChecks.TryGetValue(serviceName, out var healthCheck))
            {
                return new ServiceHealthResult
                {
                    ServiceName = serviceName,
                    Status = ServiceHealthStatus.Critical,
                    Description = "Service not registered for health monitoring",
                    CheckTime = DateTime.UtcNow,
                };
            }

            var stopwatch = Stopwatch.StartNew();
            ServiceHealthResult result;

            try
            {
                result = await healthCheck();
                result.ResponseTime = stopwatch.Elapsed;
                result.CheckTime = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Health check failed for service: {ServiceName}", serviceName);
                result = new ServiceHealthResult
                {
                    ServiceName = serviceName,
                    Status = ServiceHealthStatus.Critical,
                    Description = $"Health check threw exception: {ex.Message}",
                    ResponseTime = stopwatch.Elapsed,
                    CheckTime = DateTime.UtcNow,
                    Exception = ex,
                };
            }

            // Check if status changed and raise event
            if (this.lastResults.TryGetValue(serviceName, out var lastResult))
            {
                if (lastResult.Status != result.Status)
                {
                    this.ServiceHealthChanged?.Invoke(this, new ServiceHealthChangedEventArgs
                    {
                        ServiceName = serviceName,
                        PreviousStatus = lastResult.Status,
                        CurrentStatus = result.Status,
                        HealthResult = result,
                    });
                }
            }

            this.lastResults.AddOrUpdate(serviceName, result, (key, oldValue) => result);
            return result;
        }

        public async Task<Dictionary<string, ServiceHealthResult>> CheckAllServicesHealthAsync()
        {
            var results = new Dictionary<string, ServiceHealthResult>();
            var tasks = this.healthChecks.Keys.Select(async serviceName =>
            {
                var result = await this.CheckServiceHealthAsync(serviceName);
                lock (this.lockObject)
                {
                    results[serviceName] = result;
                }
            });

            await Task.WhenAll(tasks);
            return results;
        }

        public Dictionary<string, ServiceHealthResult> GetCurrentHealthStatus()
        {
            return this.lastResults.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    this.healthChecks.Clear();
                    this.lastResults.Clear();
                    this.logger.LogInformation("ServiceHealthMonitor disposed");
                }
                this.disposed = true;
            }
        }

        public void Dispose()
        {
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}

