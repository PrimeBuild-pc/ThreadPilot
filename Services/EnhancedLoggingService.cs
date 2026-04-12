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
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Enhanced logging service with file persistence and structured logging.
    /// </summary>
    public class EnhancedLoggingService : IEnhancedLoggingService, IDisposable
    {
        private readonly ILogger<EnhancedLoggingService> logger;
        private readonly IApplicationSettingsService settingsService;
        private readonly SemaphoreSlim fileLock = new(1, 1);
        private readonly ConcurrentQueue<LogEntry> logQueue = new();
        private readonly System.Threading.Timer flushTimer;
        private readonly string logDirectory;
        private string currentLogFilePath;
        private bool isInitialized;
        private bool disposed;

        // PERFORMANCE IMPROVEMENT: Correlation tracking for better debugging
        internal readonly AsyncLocal<string?> CorrelationId = new();
        internal readonly ConcurrentDictionary<string, DateTime> OperationStartTimes = new();

        public string CurrentLogFilePath => this.currentLogFilePath;

        public string LogDirectoryPath => this.logDirectory;

        public bool IsDebugLoggingEnabled => this.settingsService.Settings.EnableDebugLogging;

        public event EventHandler<CriticalErrorEventArgs>? CriticalErrorOccurred;

        public EnhancedLoggingService(ILogger<EnhancedLoggingService> logger, IApplicationSettingsService settingsService)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

            // Set up log directory
            this.logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ThreadPilot", "Logs");
            this.currentLogFilePath = this.GetCurrentLogFilePath();

            // Create flush timer (flush every 5 seconds)
            this.flushTimer = new System.Threading.Timer(this.FlushLogs, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }

        public async Task InitializeAsync()
        {
            if (this.isInitialized)
            {
                return;
            }

            try
            {
                // Ensure log directory exists
                Directory.CreateDirectory(this.logDirectory);

                // Create initial log file if it doesn't exist
                if (!File.Exists(this.currentLogFilePath))
                {
                    await this.CreateNewLogFileAsync();
                }

                // Log initialization
                await this.LogSystemEventAsync("LoggingService", "Enhanced logging service initialized", LogLevel.Information);

                // Clean up old logs
                await this.CleanupOldLogsAsync();

                this.isInitialized = true;
                this.logger.LogInformation("Enhanced logging service initialized. Log directory: {LogDirectory}", this.logDirectory);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to initialize enhanced logging service");
                throw;
            }
        }

        public async Task LogPowerPlanChangeAsync(string fromPlan, string toPlan, string reason, string? processName = null)
        {
            var properties = new Dictionary<string, object>
            {
                ["FromPlan"] = fromPlan,
                ["ToPlan"] = toPlan,
                ["Reason"] = reason,
                ["ProcessName"] = processName ?? "N/A",
            };

            var message = processName != null
                ? $"Power plan changed from '{fromPlan}' to '{toPlan}' due to process '{processName}' ({reason})"
                : $"Power plan changed from '{fromPlan}' to '{toPlan}' ({reason})";

            await this.LogStructuredEventAsync("PowerPlan", message, LogLevel.Information, properties);
        }

        public async Task LogProcessMonitoringEventAsync(string eventType, string processName, int processId, string details)
        {
            var properties = new Dictionary<string, object>
            {
                ["EventType"] = eventType,
                ["ProcessName"] = processName,
                ["ProcessId"] = processId,
                ["Details"] = details,
            };

            var message = $"Process monitoring event: {eventType} - {processName} (PID: {processId}) - {details}";
            await this.LogStructuredEventAsync("ProcessMonitoring", message, LogLevel.Information, properties);
        }

        public async Task LogUserActionAsync(string action, string details, string? context = null)
        {
            var properties = new Dictionary<string, object>
            {
                ["Action"] = action,
                ["Details"] = details,
                ["Context"] = context ?? "N/A",
            };

            var message = $"User action: {action} - {details}";
            if (!string.IsNullOrEmpty(context))
            {
                message += $" (Context: {context})";
            }

            await this.LogStructuredEventAsync("UserAction", message, LogLevel.Information, properties);
        }

        public async Task LogSystemEventAsync(string eventType, string message, LogLevel level = LogLevel.Information)
        {
            var properties = new Dictionary<string, object>
            {
                ["EventType"] = eventType,
            };

            await this.LogStructuredEventAsync("System", message, level, properties);
        }

        public async Task LogErrorAsync(Exception exception, string context, Dictionary<string, object>? additionalData = null)
        {
            var properties = new Dictionary<string, object>
            {
                ["Context"] = context,
                ["ExceptionType"] = exception.GetType().Name,
                ["StackTrace"] = exception.StackTrace ?? "N/A",
            };

            if (additionalData != null)
            {
                foreach (var kvp in additionalData)
                {
                    properties[kvp.Key] = kvp.Value;
                }
            }

            var message = $"Error in {context}: {exception.Message}";
            await this.LogStructuredEventAsync("Error", message, LogLevel.Error, properties, exception);

            // Raise critical error event for severe exceptions
            if (exception is OutOfMemoryException or StackOverflowException or AccessViolationException)
            {
                this.CriticalErrorOccurred?.Invoke(this, new CriticalErrorEventArgs(exception, context, additionalData));
            }
        }

        public async Task LogApplicationLifecycleEventAsync(string eventType, string details)
        {
            var properties = new Dictionary<string, object>
            {
                ["EventType"] = eventType,
                ["Details"] = details,
                ["Version"] = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown",
            };

            var message = $"Application {eventType}: {details}";
            await this.LogStructuredEventAsync("Lifecycle", message, LogLevel.Information, properties);
        }

        public IDisposable BeginScope(string operationName, object? parameters = null)
        {
            var correlationId = Guid.NewGuid().ToString("N")[..8];
            this.CorrelationId.Value = correlationId;
            this.OperationStartTimes[correlationId] = DateTime.UtcNow;

            var parametersDict = parameters != null
                ? JsonSerializer.Serialize(parameters)
                : "{}";

            this.logger.LogInformation(
                "Operation {OperationName} started with correlation {CorrelationId} and parameters {Parameters}",
                operationName, correlationId, parametersDict);

            return new OperationScope(this, operationName, correlationId);
        }

        public string? GetCurrentCorrelationId()
        {
            return this.CorrelationId.Value;
        }

        private async Task LogStructuredEventAsync(string category, string message, LogLevel level, Dictionary<string, object> properties, Exception? exception = null)
        {
            if (!this.isInitialized && category != "System")
            {
                return;
            }

            // Skip debug messages if debug logging is disabled
            if (level == LogLevel.Debug && !this.IsDebugLoggingEnabled)
            {
                return;
            }

            var logEntry = new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = level,
                Category = category,
                Message = message,
                Exception = exception?.ToString(),
                Properties = properties,
                CorrelationId = Thread.CurrentThread.ManagedThreadId.ToString(),
            };

            this.logQueue.Enqueue(logEntry);

            // Force immediate flush for errors and critical events
            if (level >= LogLevel.Error)
            {
                await this.FlushLogsAsync();
            }
        }

        private void FlushLogs(object? state)
        {
            TaskSafety.FireAndForget(this.FlushLogsAsync(), ex =>
            {
                this.logger.LogWarning(ex, "Periodic log flush failed");
            });
        }

        private async Task FlushLogsAsync()
        {
            if (this.logQueue.IsEmpty)
            {
                return;
            }

            await this.fileLock.WaitAsync();
            try
            {
                // Check if we need to rotate the log file
                await this.CheckLogRotationAsync();

                var logEntries = new List<LogEntry>();
                while (this.logQueue.TryDequeue(out var entry))
                {
                    logEntries.Add(entry);
                }

                if (logEntries.Count == 0)
                {
                    return;
                }

                // Write entries to file
                await this.WriteLogEntriesToFileAsync(logEntries);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to flush logs to file");
            }
            finally
            {
                this.fileLock.Release();
            }
        }

        private async Task WriteLogEntriesToFileAsync(List<LogEntry> entries)
        {
            var logLines = entries.Select(this.FormatLogEntry);
            await File.AppendAllLinesAsync(this.currentLogFilePath, logLines);
        }

        private string FormatLogEntry(LogEntry entry)
        {
            var logData = new
            {
                timestamp = entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                level = entry.Level.ToString(),
                category = entry.Category,
                message = entry.Message,
                exception = entry.Exception,
                properties = entry.Properties,
                correlationId = entry.CorrelationId,
            };

            return JsonSerializer.Serialize(logData, new JsonSerializerOptions { WriteIndented = false });
        }

        private async Task CheckLogRotationAsync()
        {
            var fileInfo = new FileInfo(this.currentLogFilePath);
            var maxSizeBytes = this.settingsService.Settings.MaxLogFileSizeMb * 1024 * 1024;

            if (fileInfo.Exists && fileInfo.Length > maxSizeBytes)
            {
                // Rotate log file
                var rotatedPath = Path.Combine(this.logDirectory, $"ThreadPilot_{DateTime.UtcNow:yyyyMMdd_HHmmss}.log");
                File.Move(this.currentLogFilePath, rotatedPath);
                await this.CreateNewLogFileAsync();
            }
        }

        private async Task CreateNewLogFileAsync()
        {
            this.currentLogFilePath = this.GetCurrentLogFilePath();
            await File.WriteAllTextAsync(this.currentLogFilePath, $"# ThreadPilot Log File - Created {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC{Environment.NewLine}");
        }

        private string GetCurrentLogFilePath()
        {
            return Path.Combine(this.logDirectory, "ThreadPilot.log");
        }

        public async Task<List<LogEntry>> GetRecentLogEntriesAsync(int count = 100)
        {
            return await this.GetLogEntriesAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);
        }

        public async Task<List<LogEntry>> GetLogEntriesAsync(DateTime fromDate, DateTime toDate)
        {
            var entries = new List<LogEntry>();

            await this.fileLock.WaitAsync();
            try
            {
                var logFiles = Directory.GetFiles(this.logDirectory, "*.log")
                    .OrderByDescending(f => new FileInfo(f).CreationTime);

                foreach (var logFile in logFiles)
                {
                    var fileEntries = await this.ReadLogEntriesFromFileAsync(logFile, fromDate, toDate);
                    entries.AddRange(fileEntries);
                }

                return entries.OrderByDescending(e => e.Timestamp).Take(1000).ToList();
            }
            finally
            {
                this.fileLock.Release();
            }
        }

        private async Task<List<LogEntry>> ReadLogEntriesFromFileAsync(string filePath, DateTime fromDate, DateTime toDate)
        {
            var entries = new List<LogEntry>();

            try
            {
                var lines = await File.ReadAllLinesAsync(filePath);
                foreach (var line in lines)
                {
                    if (line.StartsWith("#") || string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    try
                    {
                        var logData = JsonSerializer.Deserialize<JsonElement>(line);
                        var timestamp = DateTime.Parse(logData.GetProperty("timestamp").GetString()!);

                        if (timestamp >= fromDate && timestamp <= toDate)
                        {
                            var entry = new LogEntry
                            {
                                Timestamp = timestamp,
                                Level = Enum.Parse<LogLevel>(logData.GetProperty("level").GetString()!),
                                Category = logData.GetProperty("category").GetString()!,
                                Message = logData.GetProperty("message").GetString()!,
                                Exception = logData.TryGetProperty("exception", out var ex) ? ex.GetString() : null,
                                CorrelationId = logData.TryGetProperty("correlationId", out var cid) ? cid.GetString() : null,
                            };

                            if (logData.TryGetProperty("properties", out var props))
                            {
                                entry.Properties = JsonSerializer.Deserialize<Dictionary<string, object>>(props.GetRawText()) ?? new();
                            }

                            entries.Add(entry);
                        }
                    }
                    catch
                    {
                        // Skip malformed log entries
                    }
                }
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Failed to read log entries from file: {FilePath}", filePath);
            }

            return entries;
        }

        public async Task CleanupOldLogsAsync()
        {
            await this.fileLock.WaitAsync();
            try
            {
                var retentionDate = DateTime.UtcNow.AddDays(-this.settingsService.Settings.LogRetentionDays);
                var logFiles = Directory.GetFiles(this.logDirectory, "*.log");

                foreach (var logFile in logFiles)
                {
                    var fileInfo = new FileInfo(logFile);
                    if (fileInfo.CreationTime < retentionDate && Path.GetFileName(logFile) != "ThreadPilot.log")
                    {
                        try
                        {
                            File.Delete(logFile);
                            this.logger.LogDebug("Deleted old log file: {LogFile}", logFile);
                        }
                        catch (Exception ex)
                        {
                            this.logger.LogWarning(ex, "Failed to delete old log file: {LogFile}", logFile);
                        }
                    }
                }
            }
            finally
            {
                this.fileLock.Release();
            }
        }

        public async Task<LogFileStatistics> GetLogStatisticsAsync()
        {
            await this.fileLock.WaitAsync();
            try
            {
                var stats = new LogFileStatistics();
                var logFiles = Directory.GetFiles(this.logDirectory, "*.log");

                stats.TotalLogFiles = logFiles.Length;

                foreach (var logFile in logFiles)
                {
                    var fileInfo = new FileInfo(logFile);
                    stats.TotalLogSizeBytes += fileInfo.Length;

                    if (Path.GetFileName(logFile) == "ThreadPilot.log")
                    {
                        stats.CurrentFileSizeBytes = fileInfo.Length;
                    }

                    if (stats.OldestLogDate == default || fileInfo.CreationTime < stats.OldestLogDate)
                    {
                        stats.OldestLogDate = fileInfo.CreationTime;
                    }

                    if (fileInfo.CreationTime > stats.NewestLogDate)
                    {
                        stats.NewestLogDate = fileInfo.CreationTime;
                    }
                }

                return stats;
            }
            finally
            {
                this.fileLock.Release();
            }
        }

        public async Task<string> ExportLogsAsync(DateTime fromDate, DateTime toDate, string? exportPath = null)
        {
            exportPath ??= Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"ThreadPilot_Logs_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

            var entries = await this.GetLogEntriesAsync(fromDate, toDate);
            var exportLines = entries.Select(e => $"{e.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{e.Level}] {e.Category}: {e.Message}");

            await File.WriteAllLinesAsync(exportPath, exportLines);
            return exportPath;
        }

        public async Task UpdateConfigurationAsync(bool enableDebugLogging, int maxFileSizeMb, int retentionDays)
        {
            var updatedSettings = this.settingsService.Settings;
            updatedSettings.EnableDebugLogging = enableDebugLogging;
            updatedSettings.MaxLogFileSizeMb = maxFileSizeMb;
            updatedSettings.LogRetentionDays = retentionDays;

            await this.settingsService.UpdateSettingsAsync(updatedSettings);
            await this.LogSystemEventAsync("Configuration", $"Logging configuration updated: Debug={enableDebugLogging}, MaxSize={maxFileSizeMb}MB, Retention={retentionDays}days");
        }

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.flushTimer?.Dispose();
            this.FlushLogsAsync().Wait(TimeSpan.FromSeconds(5));
            this.fileLock?.Dispose();
            this.disposed = true;
        }
    }

    /// <summary>
    /// Operation scope for correlation tracking.
    /// </summary>
    internal class OperationScope : IDisposable
    {
        private readonly EnhancedLoggingService loggingService;
        private readonly string operationName;
        private readonly string correlationId;
        private readonly DateTime startTime;
        private bool disposed;

        public OperationScope(EnhancedLoggingService loggingService, string operationName, string correlationId)
        {
            this.loggingService = loggingService;
            this.operationName = operationName;
            this.correlationId = correlationId;
            this.startTime = DateTime.UtcNow;
        }

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            var duration = DateTime.UtcNow - this.startTime;
            this.loggingService.OperationStartTimes.TryRemove(this.correlationId, out _);
            this.loggingService.CorrelationId.Value = null;

            // Use the public logging method instead of accessing private _logger
            _ = this.loggingService.LogSystemEventAsync(
                "OperationCompleted",
                $"Operation {this.operationName} completed with correlation {this.correlationId} in {duration.TotalMilliseconds}ms");

            this.disposed = true;
        }
    }
}

