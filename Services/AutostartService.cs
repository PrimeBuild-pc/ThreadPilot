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
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Win32;

    /// <summary>
    /// Service for managing Windows autostart functionality using registry.
    /// </summary>
    public partial class AutostartService : IAutostartService
    {
        private const string REGISTRYKEYPATH = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string APPLICATIONNAME = "ThreadPilot";
        private const string SCHEDULEDTASKNAME = "ThreadPilot_Startup";
        private static readonly string SchTasksExecutablePath = Path.Combine(Environment.SystemDirectory, "schtasks.exe");
        private static readonly TimeSpan ScheduledTaskTimeout = TimeSpan.FromSeconds(20);

        private readonly ILogger<AutostartService> logger;
        private readonly IElevationService elevationService;
        private bool isAutostartEnabled;
        private string? autostartPath;

        public event EventHandler<AutostartStatusChangedEventArgs>? AutostartStatusChanged;

        public bool IsAutostartEnabled => this.isAutostartEnabled;

        public string? AutostartPath => this.autostartPath;

        public AutostartService(ILogger<AutostartService> logger, IElevationService elevationService)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.elevationService = elevationService ?? throw new ArgumentNullException(nameof(elevationService));

            // Initialize current status without surfacing startup exceptions.
            TaskSafety.FireAndForget(this.CheckAutostartStatusAsync(), ex =>
            {
                LogAutostartInitializationFailed(this.logger, ex);
            });
        }

        public async Task<bool> EnableAutostartAsync(bool startMinimized = true)
        {
            try
            {
                var executablePath = this.GetExecutablePath();
                if (string.IsNullOrEmpty(executablePath))
                {
                    LogAutostartExecutablePathMissing(this.logger);
                    return false;
                }

                var arguments = this.GetAutostartArguments(startMinimized);
                var fullCommand = $"\"{executablePath}\" {arguments}";

                using var key = Registry.CurrentUser.OpenSubKey(REGISTRYKEYPATH, true);
                if (key == null)
                {
                    LogAutostartRegistryKeyMissing(this.logger);
                    return false;
                }

                key.SetValue(APPLICATIONNAME, fullCommand, RegistryValueKind.String);

                // For elevated apps, also create a scheduled task as backup
                if (this.elevationService.IsRunningAsAdministrator())
                {
                    await this.CreateElevatedStartupTask(executablePath, arguments);
                }

                this.isAutostartEnabled = true;
                this.autostartPath = fullCommand;

                LogAutostartEnabled(this.logger, fullCommand);

                this.AutostartStatusChanged?.Invoke(this, new AutostartStatusChangedEventArgs(
                    true, startMinimized, fullCommand));

                return true;
            }
            catch (Exception ex)
            {
                LogEnableAutostartFailed(this.logger, ex);

                this.AutostartStatusChanged?.Invoke(this, new AutostartStatusChangedEventArgs(
                    false, startMinimized, null, ex));

                return false;
            }
        }

        public async Task<bool> DisableAutostartAsync()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(REGISTRYKEYPATH, true);
                if (key == null)
                {
                    LogAutostartRegistryRemovalKeyMissing(this.logger);
                    return false;
                }

                if (key.GetValue(APPLICATIONNAME) != null)
                {
                    key.DeleteValue(APPLICATIONNAME, false);
                    LogAutostartDisabled(this.logger);
                }

                // Also remove the scheduled task if it exists
                await this.RemoveElevatedStartupTask();

                this.isAutostartEnabled = false;
                this.autostartPath = null;

                this.AutostartStatusChanged?.Invoke(this, new AutostartStatusChangedEventArgs(false));

                return true;
            }
            catch (Exception ex)
            {
                LogDisableAutostartFailed(this.logger, ex);

                this.AutostartStatusChanged?.Invoke(this, new AutostartStatusChangedEventArgs(
                    false, false, null, ex));

                return false;
            }
        }

        public Task<bool> CheckAutostartStatusAsync()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(REGISTRYKEYPATH, false);
                if (key == null)
                {
                    this.isAutostartEnabled = false;
                    this.autostartPath = null;
                    return Task.FromResult(false);
                }

                var value = key.GetValue(APPLICATIONNAME) as string;
                this.isAutostartEnabled = !string.IsNullOrEmpty(value);
                this.autostartPath = value;

                LogAutostartStatusChecked(this.logger, this.isAutostartEnabled, this.autostartPath);

                return Task.FromResult(this.isAutostartEnabled);
            }
            catch (Exception ex)
            {
                LogCheckAutostartStatusFailed(this.logger, ex);
                this.isAutostartEnabled = false;
                this.autostartPath = null;
                return Task.FromResult(false);
            }
        }

        public async Task<bool> UpdateAutostartAsync(bool startMinimized = true)
        {
            if (!this.isAutostartEnabled)
            {
                return await this.EnableAutostartAsync(startMinimized);
            }

            // Re-enable with new parameters
            await this.DisableAutostartAsync();
            return await this.EnableAutostartAsync(startMinimized);
        }

        public string GetAutostartArguments(bool startMinimized = true)
        {
            var args = new System.Collections.Generic.List<string>();

            if (startMinimized)
            {
                args.Add("--start-minimized");
            }

            // Add any other startup arguments as needed
            args.Add("--autostart");

            return string.Join(" ", args);
        }

        private string? GetExecutablePath()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var location = assembly.Location;

                // Handle .NET Core/5+ scenarios where Location might be empty
                if (string.IsNullOrEmpty(location))
                {
                    location = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                }

                // If we're running from a .dll, try to find the .exe
                if (!string.IsNullOrEmpty(location) && location.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    var exePath = Path.ChangeExtension(location, ".exe");
                    if (File.Exists(exePath))
                    {
                        location = exePath;
                    }
                }

                return location;
            }
            catch (Exception ex)
            {
                LogGetExecutablePathFailed(this.logger, ex);
                return null;
            }
        }

        /// <summary>
        /// Creates a scheduled task for elevated startup as a backup to registry autostart.
        /// </summary>
        private async Task CreateElevatedStartupTask(string executablePath, string arguments)
        {
            try
            {
                if (!IsValidExecutablePath(executablePath))
                {
                    LogSkipElevatedTaskInvalidPath(this.logger, executablePath);
                    return;
                }

                var taskRunCommand = BuildAutostartCommand(executablePath, arguments);
                var result = await RunSchTasksAsync(new List<string>
                {
                    "/Create",
                    "/TN", SCHEDULEDTASKNAME,
                    "/TR", taskRunCommand,
                    "/SC", "ONLOGON",
                    "/RL", "HIGHEST",
                    "/F",
                    "/RU", Environment.UserName,
                });

                if (result.ExitCode == 0)
                {
                    LogElevatedStartupTaskCreated(this.logger);
                }
                else
                {
                    LogCreateElevatedStartupTaskFailed(this.logger, result.ExitCode, result.StandardError);
                }
            }
            catch (Exception ex)
            {
                LogCreateElevatedStartupTaskException(this.logger, ex);
            }
        }

        /// <summary>
        /// Removes the elevated startup scheduled task.
        /// </summary>
        private async Task RemoveElevatedStartupTask()
        {
            try
            {
                var result = await RunSchTasksAsync(new List<string>
                {
                    "/Delete",
                    "/TN", SCHEDULEDTASKNAME,
                    "/F",
                });

                if (result.ExitCode == 0)
                {
                    LogElevatedStartupTaskRemoved(this.logger);
                }
                else
                {
                    // Task might not exist, which is fine.
                    LogScheduledTaskRemovalExitCode(this.logger, result.ExitCode);
                }
            }
            catch (Exception ex)
            {
                LogRemoveElevatedStartupTaskException(this.logger, ex);
            }
        }

        [LoggerMessage(EventId = 4200, Level = LogLevel.Debug, Message = "Autostart status initialization failed")]
        private static partial void LogAutostartInitializationFailed(ILogger logger, Exception ex);

        [LoggerMessage(EventId = 4201, Level = LogLevel.Error, Message = "Could not determine executable path for autostart")]
        private static partial void LogAutostartExecutablePathMissing(ILogger logger);

        [LoggerMessage(EventId = 4202, Level = LogLevel.Error, Message = "Could not open registry key for autostart")]
        private static partial void LogAutostartRegistryKeyMissing(ILogger logger);

        [LoggerMessage(EventId = 4203, Level = LogLevel.Information, Message = "Autostart enabled: {Command}")]
        private static partial void LogAutostartEnabled(ILogger logger, string command);

        [LoggerMessage(EventId = 4204, Level = LogLevel.Error, Message = "Failed to enable autostart")]
        private static partial void LogEnableAutostartFailed(ILogger logger, Exception ex);

        [LoggerMessage(EventId = 4205, Level = LogLevel.Warning, Message = "Could not open registry key for autostart removal")]
        private static partial void LogAutostartRegistryRemovalKeyMissing(ILogger logger);

        [LoggerMessage(EventId = 4206, Level = LogLevel.Information, Message = "Autostart disabled")]
        private static partial void LogAutostartDisabled(ILogger logger);

        [LoggerMessage(EventId = 4207, Level = LogLevel.Error, Message = "Failed to disable autostart")]
        private static partial void LogDisableAutostartFailed(ILogger logger, Exception ex);

        [LoggerMessage(EventId = 4208, Level = LogLevel.Debug, Message = "Autostart status checked: {IsEnabled}, Path: {Path}")]
        private static partial void LogAutostartStatusChecked(ILogger logger, bool isEnabled, string? path);

        [LoggerMessage(EventId = 4209, Level = LogLevel.Error, Message = "Failed to check autostart status")]
        private static partial void LogCheckAutostartStatusFailed(ILogger logger, Exception ex);

        [LoggerMessage(EventId = 4210, Level = LogLevel.Error, Message = "Failed to get executable path")]
        private static partial void LogGetExecutablePathFailed(ILogger logger, Exception ex);

        [LoggerMessage(EventId = 4211, Level = LogLevel.Warning, Message = "Skipping elevated startup task creation due to invalid executable path: {Path}")]
        private static partial void LogSkipElevatedTaskInvalidPath(ILogger logger, string path);

        [LoggerMessage(EventId = 4212, Level = LogLevel.Information, Message = "Created elevated startup task successfully")]
        private static partial void LogElevatedStartupTaskCreated(ILogger logger);

        [LoggerMessage(EventId = 4213, Level = LogLevel.Warning, Message = "Failed to create elevated startup task. Exit code: {ExitCode}, Error: {Error}")]
        private static partial void LogCreateElevatedStartupTaskFailed(ILogger logger, int exitCode, string error);

        [LoggerMessage(EventId = 4214, Level = LogLevel.Warning, Message = "Failed to create elevated startup task")]
        private static partial void LogCreateElevatedStartupTaskException(ILogger logger, Exception ex);

        [LoggerMessage(EventId = 4215, Level = LogLevel.Information, Message = "Removed elevated startup task successfully")]
        private static partial void LogElevatedStartupTaskRemoved(ILogger logger);

        [LoggerMessage(EventId = 4216, Level = LogLevel.Debug, Message = "Scheduled task removal completed with exit code: {ExitCode}")]
        private static partial void LogScheduledTaskRemovalExitCode(ILogger logger, int exitCode);

        [LoggerMessage(EventId = 4217, Level = LogLevel.Warning, Message = "Failed to remove elevated startup task")]
        private static partial void LogRemoveElevatedStartupTaskException(ILogger logger, Exception ex);

        private static bool IsValidExecutablePath(string executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath) || !Path.IsPathRooted(executablePath))
            {
                return false;
            }

            return File.Exists(executablePath) &&
                   string.Equals(Path.GetExtension(executablePath), ".exe", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildAutostartCommand(string executablePath, string arguments)
        {
            var trimmedArguments = arguments?.Trim();
            return string.IsNullOrWhiteSpace(trimmedArguments)
                ? $"\"{executablePath}\""
                : $"\"{executablePath}\" {trimmedArguments}";
        }

        private static async Task<ProcessResult> RunSchTasksAsync(IReadOnlyList<string> arguments)
        {
            if (!File.Exists(SchTasksExecutablePath))
            {
                return new ProcessResult(-1, string.Empty, "schtasks.exe not found in system directory");
            }

            var processInfo = new ProcessStartInfo
            {
                FileName = SchTasksExecutablePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            foreach (var argument in arguments)
            {
                processInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                return new ProcessResult(-1, string.Empty, "Could not start schtasks.exe");
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            var exitTask = process.WaitForExitAsync();
            var completedTask = await Task.WhenAny(exitTask, Task.Delay(ScheduledTaskTimeout));
            if (completedTask != exitTask)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best-effort kill for timeout.
                }

                return new ProcessResult(-1, await outputTask, $"schtasks timeout after {ScheduledTaskTimeout.TotalSeconds} seconds");
            }

            await exitTask;
            return new ProcessResult(process.ExitCode, await outputTask, await errorTask);
        }

        private readonly struct ProcessResult
        {
            public ProcessResult(int exitCode, string standardOutput, string standardError)
            {
                this.ExitCode = exitCode;
                this.StandardOutput = standardOutput;
                this.StandardError = standardError;
            }

            public int ExitCode { get; }

            public string StandardOutput { get; }

            public string StandardError { get; }
        }
    }
}

