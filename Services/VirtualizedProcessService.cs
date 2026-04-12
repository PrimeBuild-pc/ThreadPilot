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
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using ThreadPilot.Models;

    /// <summary>
    /// Implementation of virtualized process service with batch loading and caching.
    /// </summary>
    public class VirtualizedProcessService : IVirtualizedProcessService, IDisposable
    {
        private readonly IProcessService processService;
        private readonly IMemoryCache cache;
        private readonly ILogger<VirtualizedProcessService> logger;
        private readonly IRetryPolicyService retryPolicy;
        private readonly SemaphoreSlim loadingSemaphore = new(1, 1);
        private readonly ConcurrentDictionary<int, ProcessBatchResult> batchCache = new();
        private readonly System.Threading.Timer backgroundPreloadTimer;

        private List<ProcessModel>? allProcesses;
        private DateTime lastFullRefresh = DateTime.MinValue;
        private bool disposed;

        public VirtualizedProcessConfig Configuration { get; set; } = new();

        public event EventHandler<BatchLoadProgressEventArgs>? BatchLoadProgress;

        public event EventHandler<ProcessBatchResult>? BackgroundBatchLoaded;

        public VirtualizedProcessService(
            IProcessService processService,
            IMemoryCache cache,
            ILogger<VirtualizedProcessService> logger,
            IRetryPolicyService retryPolicy)
        {
            this.processService = processService ?? throw new ArgumentNullException(nameof(processService));
            this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));

            // Set up background preloading timer
            this.backgroundPreloadTimer = new System.Threading.Timer(this.BackgroundPreloadCallback, null, Timeout.Infinite, Timeout.Infinite);
        }

        public async Task InitializeAsync()
        {
            this.logger.LogInformation("Initializing VirtualizedProcessService with batch size: {BatchSize}", this.Configuration.BatchSize);

            // Perform initial load to get total count
            await this.RefreshAllProcessesAsync(false);

            if (this.Configuration.EnableBackgroundLoading)
            {
                this.backgroundPreloadTimer.Change(this.Configuration.RefreshInterval, this.Configuration.RefreshInterval);
            }
        }

        public async Task<int> GetTotalProcessCountAsync(bool activeApplicationsOnly = false)
        {
            await this.EnsureProcessesLoadedAsync(activeApplicationsOnly);

            if (activeApplicationsOnly)
            {
                return this.allProcesses?.Count(p => p.HasVisibleWindow) ?? 0;
            }

            return this.allProcesses?.Count ?? 0;
        }

        public async Task<ProcessBatchResult> LoadProcessBatchAsync(int batchIndex, bool activeApplicationsOnly = false)
        {
            var cacheKey = $"batch_{batchIndex}_{activeApplicationsOnly}";

            if (this.batchCache.TryGetValue(cacheKey.GetHashCode(), out var cachedBatch))
            {
                this.logger.LogDebug("Returning cached batch {BatchIndex}", batchIndex);
                return cachedBatch;
            }

            return await this.retryPolicy.ExecuteAsync(
                async () =>
            {
                var stopwatch = Stopwatch.StartNew();

                await this.EnsureProcessesLoadedAsync(activeApplicationsOnly);

                var filteredProcesses = activeApplicationsOnly
                    ? this.allProcesses?.Where(p => p.HasVisibleWindow).ToList() ?? new List<ProcessModel>()
                    : this.allProcesses ?? new List<ProcessModel>();

                var totalCount = filteredProcesses.Count;
                var totalBatches = (int)Math.Ceiling((double)totalCount / this.Configuration.BatchSize);

                var startIndex = batchIndex * this.Configuration.BatchSize;
                var batchProcesses = filteredProcesses
                    .Skip(startIndex)
                    .Take(this.Configuration.BatchSize)
                    .ToList();

                var result = new ProcessBatchResult
                {
                    Processes = batchProcesses,
                    BatchIndex = batchIndex,
                    TotalBatches = totalBatches,
                    TotalProcessCount = totalCount,
                    HasMoreBatches = batchIndex < totalBatches - 1,
                    LoadTime = stopwatch.Elapsed,
                };

                // Cache the result
                this.batchCache.TryAdd(cacheKey.GetHashCode(), result);

                this.logger.LogDebug(
                    "Loaded batch {BatchIndex}/{TotalBatches} with {ProcessCount} processes in {LoadTime}ms",
                    batchIndex, totalBatches, batchProcesses.Count, stopwatch.ElapsedMilliseconds);

                return result;
            }, this.retryPolicy.CreateProcessOperationPolicy());
        }

        public async Task<List<ProcessBatchResult>> LoadProcessBatchesAsync(int startBatchIndex, int batchCount, bool activeApplicationsOnly = false)
        {
            var results = new List<ProcessBatchResult>();
            var totalBatches = await this.GetTotalBatchCountAsync(activeApplicationsOnly);

            for (int i = 0; i < batchCount && (startBatchIndex + i) < totalBatches; i++)
            {
                var batchIndex = startBatchIndex + i;
                var batch = await this.LoadProcessBatchAsync(batchIndex, activeApplicationsOnly);
                results.Add(batch);

                // Report progress
                this.BatchLoadProgress?.Invoke(this, new BatchLoadProgressEventArgs
                {
                    LoadedBatches = i + 1,
                    TotalBatches = batchCount,
                    LoadedProcesses = results.Sum(r => r.Processes.Count),
                    TotalProcesses = batch.TotalProcessCount,
                    StatusMessage = $"Loaded batch {batchIndex + 1} of {totalBatches}",
                });
            }

            return results;
        }

        public async Task PreloadNextBatchAsync(int currentBatchIndex, bool activeApplicationsOnly = false)
        {
            if (!this.Configuration.EnableBackgroundLoading)
            {
                return;
            }

            var nextBatchIndex = currentBatchIndex + 1;
            var totalBatches = await this.GetTotalBatchCountAsync(activeApplicationsOnly);

            if (nextBatchIndex < totalBatches)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var batch = await this.LoadProcessBatchAsync(nextBatchIndex, activeApplicationsOnly);
                        this.BackgroundBatchLoaded?.Invoke(this, batch);
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogWarning(ex, "Failed to preload batch {BatchIndex}", nextBatchIndex);
                    }
                });
            }
        }

        public async Task<List<ProcessModel>> SearchProcessesAsync(string searchTerm, bool activeApplicationsOnly = false)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return new List<ProcessModel>();
            }

            await this.EnsureProcessesLoadedAsync(activeApplicationsOnly);

            var filteredProcesses = activeApplicationsOnly
                ? this.allProcesses?.Where(p => p.HasVisibleWindow) ?? Enumerable.Empty<ProcessModel>()
                : this.allProcesses ?? Enumerable.Empty<ProcessModel>();

            return filteredProcesses
                .Where(p => p.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                           (p.MainWindowTitle?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();
        }

        public async Task<ProcessBatchResult> RefreshBatchAsync(int batchIndex, bool activeApplicationsOnly = false)
        {
            var cacheKey = $"batch_{batchIndex}_{activeApplicationsOnly}";
            this.batchCache.TryRemove(cacheKey.GetHashCode(), out _);

            // Force refresh of all processes
            await this.RefreshAllProcessesAsync(activeApplicationsOnly);

            return await this.LoadProcessBatchAsync(batchIndex, activeApplicationsOnly);
        }

        public void ClearCache()
        {
            this.batchCache.Clear();
            this.allProcesses = null;
            this.lastFullRefresh = DateTime.MinValue;
            this.logger.LogInformation("Cleared virtualized process cache");
        }

        private async Task EnsureProcessesLoadedAsync(bool activeApplicationsOnly)
        {
            if (this.allProcesses == null || DateTime.UtcNow - this.lastFullRefresh > this.Configuration.RefreshInterval)
            {
                await this.RefreshAllProcessesAsync(activeApplicationsOnly);
            }
        }

        private async Task RefreshAllProcessesAsync(bool activeApplicationsOnly)
        {
            await this.loadingSemaphore.WaitAsync();
            try
            {
                this.logger.LogDebug("Refreshing all processes (activeOnly: {ActiveOnly})", activeApplicationsOnly);

                var processes = activeApplicationsOnly
                    ? await this.processService.GetActiveApplicationsAsync()
                    : await this.processService.GetProcessesAsync();

                this.allProcesses = processes.ToList();
                this.lastFullRefresh = DateTime.UtcNow;

                // Clear batch cache since underlying data changed
                this.batchCache.Clear();

                this.logger.LogInformation("Refreshed {ProcessCount} processes", this.allProcesses.Count);
            }
            finally
            {
                this.loadingSemaphore.Release();
            }
        }

        private async Task<int> GetTotalBatchCountAsync(bool activeApplicationsOnly)
        {
            var totalCount = await this.GetTotalProcessCountAsync(activeApplicationsOnly);
            return (int)Math.Ceiling((double)totalCount / this.Configuration.BatchSize);
        }

        private void BackgroundPreloadCallback(object? state)
        {
            TaskSafety.FireAndForget(this.BackgroundPreloadCallbackAsync(), ex =>
            {
                this.logger.LogWarning(ex, "Background process refresh failed");
            });
        }

        private async Task BackgroundPreloadCallbackAsync()
        {
            try
            {
                // Refresh processes in background
                await this.RefreshAllProcessesAsync(false);
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Background process refresh failed");
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    this.backgroundPreloadTimer?.Dispose();
                    this.loadingSemaphore?.Dispose();
                    this.batchCache.Clear();
                    this.logger.LogInformation("VirtualizedProcessService disposed");
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

