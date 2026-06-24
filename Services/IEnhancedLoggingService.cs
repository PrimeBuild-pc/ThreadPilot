namespace ThreadPilot.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    public interface IEnhancedLoggingService
    {
        string CurrentLogFilePath { get; }

        string LogDirectoryPath { get; }

        bool IsDebugLoggingEnabled { get; }

        event EventHandler<CriticalErrorEventArgs>? CriticalErrorOccurred;

        Task InitializeAsync();

        Task LogPowerPlanChangeAsync(string fromPlan, string toPlan, string reason, string? processName = null);

        Task LogProcessMonitoringEventAsync(string eventType, string processName, int processId, string details);

        Task LogUserActionAsync(string action, string details, string? context = null);

        Task LogSystemEventAsync(string eventType, string message, LogLevel level = LogLevel.Information);

        Task LogErrorAsync(Exception exception, string context, Dictionary<string, object>? additionalData = null);

        Task LogApplicationLifecycleEventAsync(string eventType, string details);

        Task<List<LogEntry>> GetRecentLogEntriesAsync(int count = 100);

        IDisposable BeginScope(string operationName, object? parameters = null);

        string? GetCurrentCorrelationId();

        Task<List<LogEntry>> GetLogEntriesAsync(DateTime fromDate, DateTime toDate);

        Task CleanupOldLogsAsync();

        Task<LogFileStatistics> GetLogStatisticsAsync();

        Task<string> ExportLogsAsync(DateTime fromDate, DateTime toDate, string? exportPath = null);

        Task UpdateConfigurationAsync(bool enableDebugLogging, int maxFileSizeMb, int retentionDays);
    }

    public class CriticalErrorEventArgs : EventArgs
    {
        public Exception Exception { get; }

        public string Context { get; }

        public DateTime Timestamp { get; }

        public Dictionary<string, object> AdditionalData { get; }

        public CriticalErrorEventArgs(Exception exception, string context, Dictionary<string, object>? additionalData = null)
        {
            this.Exception = exception;
            this.Context = context;
            this.Timestamp = DateTime.UtcNow;
            this.AdditionalData = additionalData ?? new Dictionary<string, object>();
        }
    }

    public class LogEntry
    {
        public DateTime Timestamp { get; set; }

        public LogLevel Level { get; set; }

        public string Category { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string? Exception { get; set; }

        public Dictionary<string, object> Properties { get; set; } = new();

        public string? CorrelationId { get; set; }
    }

    public class LogFileStatistics
    {
        public long CurrentFileSizeBytes { get; set; }

        public int TotalLogFiles { get; set; }

        public long TotalLogSizeBytes { get; set; }

        public DateTime OldestLogDate { get; set; }

        public DateTime NewestLogDate { get; set; }

        public int ErrorCount { get; set; }

        public int WarningCount { get; set; }

        public int InfoCount { get; set; }
    }
}

