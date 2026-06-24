namespace ThreadPilot.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using ThreadPilot.Models;

    public class VirtualizedProcessConfig
    {
        public int BatchSize { get; set; } = 50;

        public int PreloadBatches { get; set; } = 2;

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

    public class BatchLoadProgressEventArgs : EventArgs
    {
        public int LoadedBatches { get; set; }

        public int TotalBatches { get; set; }

        public int LoadedProcesses { get; set; }

        public int TotalProcesses { get; set; }

        public double ProgressPercentage => this.TotalBatches > 0 ? (double)this.LoadedBatches / this.TotalBatches * 100 : 0;

        public string StatusMessage { get; set; } = string.Empty;
    }

    public interface IVirtualizedProcessService
    {
        VirtualizedProcessConfig Configuration { get; set; }

        Task InitializeAsync();

        Task<int> GetTotalProcessCountAsync(bool activeApplicationsOnly = false);

        Task<ProcessBatchResult> LoadProcessBatchAsync(int batchIndex, bool activeApplicationsOnly = false);

        Task<List<ProcessBatchResult>> LoadProcessBatchesAsync(int startBatchIndex, int batchCount, bool activeApplicationsOnly = false);

        Task PreloadNextBatchAsync(int currentBatchIndex, bool activeApplicationsOnly = false);

        Task<List<ProcessModel>> SearchProcessesAsync(string searchTerm, bool activeApplicationsOnly = false);

        Task<ProcessBatchResult> RefreshBatchAsync(int batchIndex, bool activeApplicationsOnly = false);

        void ClearCache();

        event EventHandler<BatchLoadProgressEventArgs>? BatchLoadProgress;

        event EventHandler<ProcessBatchResult>? BackgroundBatchLoaded;
    }
}

