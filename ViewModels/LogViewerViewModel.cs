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
        private readonly IEnhancedLoggingService _loggingService;
        private readonly IApplicationSettingsService _settingsService;
        private readonly ILogger<LogViewerViewModel> _logger;

        [ObservableProperty]
        private ObservableCollection<LogEntryDisplayModel> _logEntries = new();

        [ObservableProperty]
        private LogEntryDisplayModel? _selectedLogEntry;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private LogLevel _selectedLogLevel = LogLevel.Information;

        [ObservableProperty]
        private string _selectedCategory = "All";

        [ObservableProperty]
        private DateTime _fromDate = DateTime.Today.AddDays(-7);

        [ObservableProperty]
        private DateTime _toDate = DateTime.Today.AddDays(1);

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private LogFileStatistics? _logStatistics;

        [ObservableProperty]
        private bool _enableDebugLogging;

        [ObservableProperty]
        private int _maxLogFileSizeMb = 10;

        [ObservableProperty]
        private int _logRetentionDays = 7;

        [ObservableProperty]
        private bool _autoRefresh = true;

        [ObservableProperty]
        private int _refreshIntervalSeconds = 30;

        public ObservableCollection<string> AvailableCategories { get; } = new()
        {
            "All", "PowerPlan", "ProcessMonitoring", "GameBoost", "UserAction", "System", "Error", "Performance"
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
            IEnhancedLoggingService loggingService,
            IApplicationSettingsService settingsService,
            ILogger<LogViewerViewModel> logger)
        {
            this._loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            this._settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            this._logger = logger ?? throw new ArgumentNullException(nameof(logger));

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

            // Start auto-refresh if enabled
            if (this._autoRefresh)
            {
                this.StartAutoRefresh();
            }
        }

        public async Task InitializeAsync()
        {
            try
            {
                this.IsLoading = true;
                this.StatusMessage = "Loading logs...";

                await this.RefreshLogsAsync();
                await this.RefreshStatisticsAsync();

                this.StatusMessage = "Ready";
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to initialize log viewer");
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
                this.StatusMessage = "Refreshing logs...";

                var logEntries = await this._loggingService.GetLogEntriesAsync(this.FromDate, this.ToDate);
                
                // Filter by category and log level
                var filteredEntries = logEntries.Where(entry =>
                {
                    var categoryMatch = this.SelectedCategory == "All" || entry.Category == this.SelectedCategory;
                    var levelMatch = entry.Level >= this.SelectedLogLevel;
                    var searchMatch = string.IsNullOrEmpty(this.SearchText) ||
                                    entry.Message.Contains(this.SearchText, StringComparison.OrdinalIgnoreCase) ||
                                    entry.Category.Contains(this.SearchText, StringComparison.OrdinalIgnoreCase);

                    return categoryMatch && levelMatch && searchMatch;
                }).ToList();

                // Convert to display models
                var displayModels = filteredEntries.Select(entry => new LogEntryDisplayModel
                {
                    Timestamp = entry.Timestamp,
                    Level = entry.Level,
                    Category = entry.Category,
                    Message = entry.Message,
                    Exception = entry.Exception,
                    Properties = entry.Properties,
                    CorrelationId = entry.CorrelationId
                }).ToList();

                // PERFORMANCE OPTIMIZATION: Replace collection instead of Clear() + Add() loop
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.LogEntries = new ObservableCollection<LogEntryDisplayModel>(displayModels);
                    this.StatusMessage = $"Loaded {this.LogEntries.Count} log entries";
                });
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to refresh logs");
                this.StatusMessage = $"Error refreshing logs: {ex.Message}";
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
                this.LogStatistics = await this._loggingService.GetLogStatisticsAsync();
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to refresh log statistics");
            }
        }

        private async Task ClearLogsAsync()
        {
            try
            {
                this.LogEntries.Clear();
                this.StatusMessage = "Log display cleared";
                await this._loggingService.LogUserActionAsync("LogsCleared", "User cleared log display");
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to clear logs");
                this.StatusMessage = $"Error clearing logs: {ex.Message}";
            }
        }

        private async Task ExportLogsAsync()
        {
            try
            {
                this.IsLoading = true;
                this.StatusMessage = "Exporting logs...";

                var exportPath = await this._loggingService.ExportLogsAsync(this.FromDate, this.ToDate);
                this.StatusMessage = $"Logs exported to: {exportPath}";

                await this._loggingService.LogUserActionAsync(
                    "LogsExported",
                    $"Logs exported to {exportPath}",
                    $"DateRange: {this.FromDate:yyyy-MM-dd} to {this.ToDate:yyyy-MM-dd}");
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to export logs");
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

                await this._loggingService.CleanupOldLogsAsync();
                await this.RefreshStatisticsAsync();

                this.StatusMessage = "Old logs cleaned up successfully";
                await this._loggingService.LogUserActionAsync("LogsCleanup", "User initiated log cleanup");
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to cleanup old logs");
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
                await this._loggingService.UpdateConfigurationAsync(this.EnableDebugLogging, this.MaxLogFileSizeMb, this.LogRetentionDays);

                this.StatusMessage = "Logging settings saved successfully";
                await this._loggingService.LogUserActionAsync(
                    "LoggingSettingsChanged",
                    $"Debug: {this.EnableDebugLogging}, MaxSize: {this.MaxLogFileSizeMb}MB, Retention: {this.LogRetentionDays} days");
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to save logging settings");
                this.StatusMessage = $"Error saving settings: {ex.Message}";
            }
        }

        private void OpenLogDirectory()
        {
            try
            {
                var logDirectory = this._loggingService.LogDirectoryPath;
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
                this._logger.LogError(ex, "Failed to open log directory");
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
                var logText = $"[{logEntry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{logEntry.Level}] {logEntry.Category}: {logEntry.Message}";
                if (!string.IsNullOrEmpty(logEntry.Exception))
                {
                    logText += $"\nException: {logEntry.Exception}";
                }

                System.Windows.Clipboard.SetText(logText);
                this.StatusMessage = "Log entry copied to clipboard";
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to copy log entry to clipboard");
                this.StatusMessage = "Failed to copy log entry";
            }
        }

        private void LoadSettings()
        {
            var settings = this._settingsService.Settings;
            this.EnableDebugLogging = settings.EnableDebugLogging;
            this.MaxLogFileSizeMb = settings.MaxLogFileSizeMb;
            this.LogRetentionDays = settings.LogRetentionDays;
        }

        private void StartAutoRefresh()
        {
            // Implementation for auto-refresh timer would go here
            // For now, we'll keep it simple without the timer
        }

        partial void OnSearchTextChanged(string value)
        {
            // Trigger refresh when search text changes - marshal to UI thread to prevent cross-thread access exceptions
            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(async () => await this.RefreshLogsAsync());
        }

        partial void OnSelectedCategoryChanged(string value)
        {
            // Trigger refresh when category changes - marshal to UI thread to prevent cross-thread access exceptions
            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(async () => await this.RefreshLogsAsync());
        }

        partial void OnSelectedLogLevelChanged(LogLevel value)
        {
            // Trigger refresh when log level changes - marshal to UI thread to prevent cross-thread access exceptions
            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(async () => await this.RefreshLogsAsync());
        }
    }

    /// <summary>
    /// Display model for log entries in the UI.
    /// </summary>
    public class LogEntryDisplayModel
    {
        public DateTime Timestamp { get; set; }

        public LogLevel Level { get; set; }

        public string Category { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string? Exception { get; set; }

        public Dictionary<string, object> Properties { get; set; } = new();

        public string? CorrelationId { get; set; }

        public string LevelColor => this.Level switch
        {
            LogLevel.Critical => "#FF0000",
            LogLevel.Error => "#FF4444",
            LogLevel.Warning => "#FFA500",
            LogLevel.Information => "#0066CC",
            LogLevel.Debug => "#808080",
            LogLevel.Trace => "#C0C0C0",
            _ => "#000000"
        };

        public string FormattedTimestamp => this.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");

        public string ShortMessage => this.Message.Length > 100 ? this.Message.Substring(0, 100) + "..." : this.Message;

        public bool HasException => !string.IsNullOrEmpty(this.Exception);

        public bool HasProperties => this.Properties.Any();
    }
}

