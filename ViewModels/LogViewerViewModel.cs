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
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ThreadPilot.Models;
using ThreadPilot.Services;

namespace ThreadPilot.ViewModels
{
    /// <summary>
    /// ViewModel for the log viewer and management interface.
    /// </summary>
    public partial class LogViewerViewModel : ObservableObject
    {
        private readonly IActivityAuditService activityAuditService;
        private readonly IEnhancedLoggingService loggingService;
        private readonly IApplicationSettingsService settingsService;
        private readonly ILogger<LogViewerViewModel> logger;

        [ObservableProperty]
        private ObservableCollection<LogEntryDisplayModel> logEntries = new();

        [ObservableProperty]
        private LogEntryDisplayModel? selectedLogEntry;

        [ObservableProperty]
        private string searchText = string.Empty;

        [ObservableProperty]
        private LogLevel selectedLogLevel = LogLevel.Information;

        [ObservableProperty]
        private string selectedCategory = "All";

        [ObservableProperty]
        private DateTime fromDate = DateTime.Today.AddDays(-7);

        [ObservableProperty]
        private DateTime toDate = DateTime.Today.AddDays(1);

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private string statusMessage = "Ready";

        [ObservableProperty]
        private LogFileStatistics? logStatistics;

        [ObservableProperty]
        private bool enableDebugLogging;

        [ObservableProperty]
        private int maxLogFileSizeMb = 10;

        [ObservableProperty]
        private int logRetentionDays = 7;

        [ObservableProperty]
        private bool autoRefresh = true;

        [ObservableProperty]
        private int refreshIntervalSeconds = 30;

        public ObservableCollection<string> AvailableCategories { get; } = new()
        {
            "All",
            "Process",
            "Affinity",
            "Priority",
            "Memory Priority",
            "Rules",
            "Power Plans",
            "Settings",
            "Tweaks",
            "Optimization",
            "Diagnostics",
            "Safety",
        };

        public ObservableCollection<LogLevel> AvailableLogLevels { get; } = new()
        {
            LogLevel.Trace, LogLevel.Debug, LogLevel.Information, LogLevel.Warning, LogLevel.Error, LogLevel.Critical
        };

        public ICommand RefreshLogsCommand { get; }

        public ICommand ClearLogsCommand { get; }

        public ICommand ExportLogsCommand { get; }

        public ICommand CleanupOldLogsCommand { get; }

        public ICommand SaveSettingsCommand { get; }

        public ICommand OpenLogDirectoryCommand { get; }

        public ICommand CopyLogEntryCommand { get; }

        public LogViewerViewModel(
            IActivityAuditService activityAuditService,
            IEnhancedLoggingService loggingService,
            IApplicationSettingsService settingsService,
            ILogger<LogViewerViewModel> logger)
        {
            this.activityAuditService = activityAuditService ?? throw new ArgumentNullException(nameof(activityAuditService));
            this.loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Initialize commands
            this.RefreshLogsCommand = new AsyncRelayCommand(this.RefreshLogsAsync);
            this.ClearLogsCommand = new AsyncRelayCommand(this.ClearLogsAsync);
            this.ExportLogsCommand = new AsyncRelayCommand(this.ExportLogsAsync);
            this.CleanupOldLogsCommand = new AsyncRelayCommand(this.CleanupOldLogsAsync);
            this.SaveSettingsCommand = new AsyncRelayCommand(this.SaveSettingsAsync);
            this.OpenLogDirectoryCommand = new RelayCommand(this.OpenLogDirectory);
            this.CopyLogEntryCommand = new RelayCommand<LogEntryDisplayModel>(this.CopyLogEntry);

            // Load initial settings
            this.LoadSettings();
            this.activityAuditService.EntryAdded += this.OnActivityEntryAdded;

            // Start auto-refresh if enabled
            if (this.autoRefresh)
            {
                this.StartAutoRefresh();
            }
        }

        public async Task InitializeAsync()
        {
            try
            {
                this.IsLoading = true;
                this.StatusMessage = "Loading activity...";

                await this.RefreshLogsAsync();
                await this.RefreshStatisticsAsync();

                this.StatusMessage = "Ready";
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to initialize log viewer");
                this.StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                this.IsLoading = false;
            }
        }

        private async Task RefreshLogsAsync()
        {
            try
            {
                this.IsLoading = true;
                this.StatusMessage = "Refreshing activity...";

                var logEntries = await this.activityAuditService.GetEntriesAsync(this.FromDate, this.ToDate);

                // Filter by category and log level
                var filteredEntries = logEntries.Where(entry =>
                    this.ShouldDisplay(entry)).ToList();

                // Convert to display models
                var displayModels = filteredEntries.Select(ToDisplayModel).ToList();

                // PERFORMANCE OPTIMIZATION: Replace collection instead of Clear() + Add() loop
                await InvokeOnUiAsync(() =>
                {
                    this.LogEntries = new ObservableCollection<LogEntryDisplayModel>(displayModels);
                    this.StatusMessage = $"Loaded {this.LogEntries.Count} log entries";
                });
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to refresh logs");
                this.StatusMessage = $"Error refreshing activity: {ex.Message}";
            }
            finally
            {
                this.IsLoading = false;
            }
        }

        private async Task RefreshStatisticsAsync()
        {
            try
            {
                this.LogStatistics = await this.loggingService.GetLogStatisticsAsync();
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to refresh log statistics");
            }
        }

        private async Task ClearLogsAsync()
        {
            try
            {
                this.LogEntries.Clear();
                this.StatusMessage = "Activity display cleared";
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to clear logs");
                this.StatusMessage = $"Error clearing logs: {ex.Message}";
            }
        }

        private async Task ExportLogsAsync()
        {
            try
            {
                this.IsLoading = true;
                this.StatusMessage = "Exporting activity...";

                var entries = await this.activityAuditService.GetEntriesAsync(this.FromDate, this.ToDate);
                var exportPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    $"ThreadPilot_Activity_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                var exportLines = entries.Select(e =>
                    $"{e.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{e.Severity}] {e.Category}: {e.Message}" +
                    (string.IsNullOrWhiteSpace(e.Details) ? string.Empty : $" ({e.Details})"));
                await File.WriteAllLinesAsync(exportPath, exportLines);
                this.StatusMessage = $"Activity exported to: {exportPath}";

                await this.activityAuditService.LogInfoAsync(
                    "Diagnostics",
                    $"Activity exported to {Path.GetFileName(exportPath)}",
                    $"DateRange: {this.FromDate:yyyy-MM-dd} to {this.ToDate:yyyy-MM-dd}");
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to export logs");
                this.StatusMessage = $"Error exporting logs: {ex.Message}";
            }
            finally
            {
                this.IsLoading = false;
            }
        }

        private async Task CleanupOldLogsAsync()
        {
            try
            {
                this.IsLoading = true;
                this.StatusMessage = "Cleaning up old logs...";

                await this.loggingService.CleanupOldLogsAsync();
                await this.RefreshStatisticsAsync();

                this.StatusMessage = "Old diagnostic log files cleaned up successfully";
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to cleanup old logs");
                this.StatusMessage = $"Error cleaning up logs: {ex.Message}";
            }
            finally
            {
                this.IsLoading = false;
            }
        }

        private async Task SaveSettingsAsync()
        {
            try
            {
                await this.loggingService.UpdateConfigurationAsync(this.EnableDebugLogging, this.MaxLogFileSizeMb, this.LogRetentionDays);

                this.StatusMessage = "Diagnostic logging settings saved successfully";
                await this.activityAuditService.LogInfoAsync(
                    "Diagnostics",
                    "Diagnostic logging settings saved",
                    $"Debug: {this.EnableDebugLogging}, MaxSize: {this.MaxLogFileSizeMb}MB, Retention: {this.LogRetentionDays} days");
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to save logging settings");
                this.StatusMessage = $"Error saving settings: {ex.Message}";
            }
        }

        private void OpenLogDirectory()
        {
            try
            {
                var logDirectory = this.loggingService.LogDirectoryPath;
                if (Directory.Exists(logDirectory))
                {
                    System.Diagnostics.Process.Start("explorer.exe", logDirectory);
                }
                else
                {
                    this.StatusMessage = "Log directory not found";
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to open log directory");
                this.StatusMessage = $"Error opening log directory: {ex.Message}";
            }
        }

        private void CopyLogEntry(LogEntryDisplayModel? logEntry)
        {
            if (logEntry == null)
            {
                return;
            }

            try
            {
                var logText = $"[{logEntry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{logEntry.Status}] {logEntry.Category}: {logEntry.Message}";
                if (!string.IsNullOrEmpty(logEntry.Details))
                {
                    logText += $"\nDetails: {logEntry.Details}";
                }

                System.Windows.Clipboard.SetText(logText);
                this.StatusMessage = "Log entry copied to clipboard";
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to copy log entry to clipboard");
                this.StatusMessage = "Failed to copy log entry";
            }
        }

        private void LoadSettings()
        {
            var settings = this.settingsService.Settings;
            this.EnableDebugLogging = settings.EnableDebugLogging;
            this.MaxLogFileSizeMb = settings.MaxLogFileSizeMb;
            this.LogRetentionDays = settings.LogRetentionDays;
        }

        private void StartAutoRefresh()
        {
            // Implementation for auto-refresh timer would go here
            // For now, we'll keep it simple without the timer
        }

        private void OnActivityEntryAdded(object? sender, ActivityAuditEntry entry)
        {
            if (!this.ShouldDisplay(entry))
            {
                return;
            }

            _ = InvokeOnUiAsync(() =>
            {
                this.LogEntries.Insert(0, ToDisplayModel(entry));
                while (this.LogEntries.Count > 1000)
                {
                    this.LogEntries.RemoveAt(this.LogEntries.Count - 1);
                }

                this.StatusMessage = $"Loaded {this.LogEntries.Count} activity entries";
            });
        }

        private bool ShouldDisplay(ActivityAuditEntry entry)
        {
            var categoryMatch = this.SelectedCategory == "All" || entry.Category == this.SelectedCategory;
            var levelMatch = ToLogLevel(entry.Severity) >= this.SelectedLogLevel;
            var searchMatch = string.IsNullOrEmpty(this.SearchText) ||
                entry.Message.Contains(this.SearchText, StringComparison.OrdinalIgnoreCase) ||
                entry.Category.Contains(this.SearchText, StringComparison.OrdinalIgnoreCase) ||
                (entry.Details?.Contains(this.SearchText, StringComparison.OrdinalIgnoreCase) ?? false);

            return categoryMatch && levelMatch && searchMatch;
        }

        private static LogEntryDisplayModel ToDisplayModel(ActivityAuditEntry entry) =>
            new()
            {
                Timestamp = entry.Timestamp,
                Level = ToLogLevel(entry.Severity),
                AuditSeverity = entry.Severity,
                Category = entry.Category,
                Message = entry.Message,
                Details = entry.Details,
            };

        partial void OnSearchTextChanged(string value)
        {
            // Trigger refresh when search text changes - marshal to UI thread to prevent cross-thread access exceptions
            _ = InvokeOnUiAsync(async () => await this.RefreshLogsAsync());
        }

        partial void OnSelectedCategoryChanged(string value)
        {
            // Trigger refresh when category changes - marshal to UI thread to prevent cross-thread access exceptions
            _ = InvokeOnUiAsync(async () => await this.RefreshLogsAsync());
        }

        partial void OnSelectedLogLevelChanged(LogLevel value)
        {
            // Trigger refresh when log level changes - marshal to UI thread to prevent cross-thread access exceptions
            _ = InvokeOnUiAsync(async () => await this.RefreshLogsAsync());
        }

        private static Task InvokeOnUiAsync(Action action)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
                return Task.CompletedTask;
            }

            return dispatcher.InvokeAsync(action).Task;
        }

        private static Task InvokeOnUiAsync(Func<Task> action)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                return action();
            }

            return dispatcher.InvokeAsync(action).Task.Unwrap();
        }

        private static LogLevel ToLogLevel(ActivityAuditSeverity severity) =>
            severity switch
            {
                ActivityAuditSeverity.Error => LogLevel.Error,
                ActivityAuditSeverity.Warning => LogLevel.Warning,
                _ => LogLevel.Information,
            };
    }

    /// <summary>
    /// Display model for log entries in the UI.
    /// </summary>
    public class LogEntryDisplayModel
    {
        public DateTime Timestamp { get; set; }

        public LogLevel Level { get; set; }

        public ActivityAuditSeverity? AuditSeverity { get; set; }

        public string Category { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string? Exception { get; set; }

        public string? Details { get; set; }

        public Dictionary<string, object> Properties { get; set; } = new();

        public string? CorrelationId { get; set; }

        public string LevelColor => this.AuditSeverity switch
        {
            ActivityAuditSeverity.Error => "#FF4444",
            ActivityAuditSeverity.Warning => "#FFA500",
            ActivityAuditSeverity.Success => "#107C10",
            ActivityAuditSeverity.Info => "#0066CC",
            _ => this.Level switch
        {
            LogLevel.Critical => "#FF0000",
            LogLevel.Error => "#FF4444",
            LogLevel.Warning => "#FFA500",
            LogLevel.Information => "#0066CC",
            LogLevel.Debug => "#808080",
            LogLevel.Trace => "#C0C0C0",
            _ => "#000000"
        },
        };

        public string Status => this.AuditSeverity?.ToString() ?? this.Level.ToString();

        public string FormattedTimestamp => this.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");

        public string ShortMessage => this.Message.Length > 100 ? this.Message.Substring(0, 100) + "..." : this.Message;

        public bool HasException => !string.IsNullOrEmpty(this.Exception);

        public bool HasDetails => !string.IsNullOrEmpty(this.Details);

        public bool HasProperties => this.Properties.Any();
    }
}

