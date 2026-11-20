namespace ThreadPilot.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using ThreadPilot.Models;

    public class VirtualizedProcessConfig
    {
        public int BatchSize { get; set; } = 50;

        public bool EnableBackgroundLoading { get; set; } = true;

        public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromSeconds(5);
    }

    public class ProcessBatchResult
    {
        public List<ProcessModel> Processes { get; set; } = new();

        public int BatchIndex { get; set; }

        public int TotalBatches { get; set; }

        public int TotalProcessCount { get; set; }

        public bool HasMoreBatches { get; set; }

        public TimeSpan LoadTime { get; set; }
    }

    public interface IVirtualizedProcessService
    {
        VirtualizedProcessConfig Configuration { get; set; }

        Task InitializeAsync();

        Task<int> GetTotalProcessCountAsync(bool activeApplicationsOnly = false);

        Task<ProcessBatchResult> LoadProcessBatchAsync(int batchIndex, bool activeApplicationsOnly = false);

        Task PreloadNextBatchAsync(int currentBatchIndex, bool activeApplicationsOnly = false);

        event EventHandler<ProcessBatchResult>? BackgroundBatchLoaded;
    }
}

