namespace ThreadPilot.Services
{
    using System;
    using System.Threading.Tasks;
    using ThreadPilot.Models;

    public interface IProcessMonitorService : IDisposable
    {
        event EventHandler<ProcessEventArgs>? ProcessStarted;

        event EventHandler<ProcessEventArgs>? ProcessStopped;

        event EventHandler<MonitoringStatusEventArgs>? MonitoringStatusChanged;

        bool IsMonitoring { get; }

        bool IsWmiAvailable { get; }

        bool IsFallbackPollingActive { get; }

        Task StartMonitoringAsync();

        Task StopMonitoringAsync();

        Task<IEnumerable<ProcessModel>> GetRunningProcessesAsync();

        Task<bool> IsProcessRunningAsync(string executableName);

        void UpdateSettings();
    }

    public class ProcessEventArgs : EventArgs
    {
        public ProcessModel Process { get; }

        public DateTime Timestamp { get; }

        public ProcessEventArgs(ProcessModel process)
        {
            this.Process = process;
            this.Timestamp = DateTime.Now;
        }
    }

    public class MonitoringStatusEventArgs : EventArgs
    {
        public bool IsMonitoring { get; }

        public bool IsWmiAvailable { get; }

        public bool IsFallbackPollingActive { get; }

        public string? StatusMessage { get; }

        public Exception? Error { get; }

        public MonitoringStatusEventArgs(bool isMonitoring, bool isWmiAvailable, bool isFallbackPollingActive, string? statusMessage = null, Exception? error = null)
        {
            this.IsMonitoring = isMonitoring;
            this.IsWmiAvailable = isWmiAvailable;
            this.IsFallbackPollingActive = isFallbackPollingActive;
            this.StatusMessage = statusMessage;
            this.Error = error;
        }
    }
}

