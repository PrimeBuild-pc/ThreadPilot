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

        private readonly ILogger<AutostartService> logger;
        private readonly IElevationService elevationService;
        private readonly IElevatedTaskService elevatedTaskService;
        private bool isAutostartEnabled;
        private string? autostartPath;

        public event EventHandler<AutostartStatusChangedEventArgs>? AutostartStatusChanged;

        public bool IsAutostartEnabled => this.isAutostartEnabled;

        public string? AutostartPath => this.autostartPath;

        public AutostartService(ILogger<AutostartService> logger, IElevationService elevationService, IElevatedTaskService elevatedTaskService)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.elevationService = elevationService ?? throw new ArgumentNullException(nameof(elevationService));
            this.elevatedTaskService = elevatedTaskService ?? throw new ArgumentNullException(nameof(elevatedTaskService));

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

                // Clean up legacy registry-based startup to keep a single elevated startup mechanism.
                this.TryRemoveLegacyRegistryAutostart();

                if (!this.elevationService.IsRunningAsAdministrator())
                {
                    LogAutostartRequiresElevation(this.logger);

                    var elevationRequested = await this.elevationService.RequestElevationIfNeeded();
                    if (!elevationRequested)
                    {
                        return false;
                    }

                    LogAutostartDeferredToElevatedInstance(this.logger);
                    return false;
                }

                var scheduledTaskCreated = await this.elevatedTaskService.EnsureAutostartTaskAsync(executablePath, arguments);
                if (!scheduledTaskCreated)
                {
                    LogAutostartTaskRegistrationFailed(this.logger);
                    return false;
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
                if (!this.elevationService.IsRunningAsAdministrator())
                {
                    LogAutostartDisableRequiresElevation(this.logger);

                    var elevationRequested = await this.elevationService.RequestElevationIfNeeded();
                    if (!elevationRequested)
                    {
                        return false;
                    }

                    LogAutostartDisableDeferredToElevatedInstance(this.logger);
                    return false;
                }

                this.TryRemoveLegacyRegistryAutostart();

                var scheduledTaskRemoved = await this.elevatedTaskService.RemoveAutostartTaskAsync();
                if (!scheduledTaskRemoved)
                {
                    LogAutostartTaskRemovalFailed(this.logger);
                    return false;
                }

                LogAutostartDisabled(this.logger);

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

        public async Task<bool> CheckAutostartStatusAsync()
        {
            try
            {
                var taskRegistered = await this.elevatedTaskService.IsAutostartTaskRegisteredAsync();
                var legacyRegistryValue = this.TryReadLegacyRegistryAutostart();

                this.isAutostartEnabled = taskRegistered || !string.IsNullOrWhiteSpace(legacyRegistryValue);
                this.autostartPath = taskRegistered
                    ? $"task://{this.elevatedTaskService.AutostartTaskName}"
                    : legacyRegistryValue;

                LogAutostartStatusChecked(this.logger, this.isAutostartEnabled, this.autostartPath);

                return this.isAutostartEnabled;
            }
            catch (Exception ex)
            {
                LogCheckAutostartStatusFailed(this.logger, ex);
                this.isAutostartEnabled = false;
                this.autostartPath = null;
                return false;
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

        private string? TryReadLegacyRegistryAutostart()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(REGISTRYKEYPATH, false);
                return key?.GetValue(APPLICATIONNAME) as string;
            }
            catch (Exception ex)
            {
                LogLegacyRegistryReadFailed(this.logger, ex);
                return null;
            }
        }

        private void TryRemoveLegacyRegistryAutostart()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(REGISTRYKEYPATH, true);
                if (key?.GetValue(APPLICATIONNAME) != null)
                {
                    key.DeleteValue(APPLICATIONNAME, false);
                    LogLegacyRegistryEntryRemoved(this.logger);
                }
            }
            catch (Exception ex)
            {
                LogLegacyRegistryCleanupFailed(this.logger, ex);
            }
        }

        [LoggerMessage(EventId = 4200, Level = LogLevel.Debug, Message = "Autostart status initialization failed")]
        private static partial void LogAutostartInitializationFailed(ILogger logger, Exception ex);

        [LoggerMessage(EventId = 4201, Level = LogLevel.Error, Message = "Could not determine executable path for autostart")]
        private static partial void LogAutostartExecutablePathMissing(ILogger logger);

        [LoggerMessage(EventId = 4203, Level = LogLevel.Information, Message = "Autostart enabled: {Command}")]
        private static partial void LogAutostartEnabled(ILogger logger, string command);

        [LoggerMessage(EventId = 4204, Level = LogLevel.Error, Message = "Failed to enable autostart")]
        private static partial void LogEnableAutostartFailed(ILogger logger, Exception ex);

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

        [LoggerMessage(EventId = 4218, Level = LogLevel.Information, Message = "Autostart configuration requires elevation; requesting elevated restart")]
        private static partial void LogAutostartRequiresElevation(ILogger logger);

        [LoggerMessage(EventId = 4219, Level = LogLevel.Information, Message = "Autostart enable request delegated to elevated instance")]
        private static partial void LogAutostartDeferredToElevatedInstance(ILogger logger);

        [LoggerMessage(EventId = 4220, Level = LogLevel.Warning, Message = "Failed to register elevated autostart task")]
        private static partial void LogAutostartTaskRegistrationFailed(ILogger logger);

        [LoggerMessage(EventId = 4221, Level = LogLevel.Information, Message = "Autostart disable requires elevation; requesting elevated restart")]
        private static partial void LogAutostartDisableRequiresElevation(ILogger logger);

        [LoggerMessage(EventId = 4222, Level = LogLevel.Information, Message = "Autostart disable request delegated to elevated instance")]
        private static partial void LogAutostartDisableDeferredToElevatedInstance(ILogger logger);

        [LoggerMessage(EventId = 4223, Level = LogLevel.Warning, Message = "Failed to remove elevated autostart task")]
        private static partial void LogAutostartTaskRemovalFailed(ILogger logger);

        [LoggerMessage(EventId = 4224, Level = LogLevel.Debug, Message = "Legacy HKCU Run autostart value removed")]
        private static partial void LogLegacyRegistryEntryRemoved(ILogger logger);

        [LoggerMessage(EventId = 4225, Level = LogLevel.Debug, Message = "Failed to read legacy HKCU Run autostart value")]
        private static partial void LogLegacyRegistryReadFailed(ILogger logger, Exception ex);

        [LoggerMessage(EventId = 4226, Level = LogLevel.Debug, Message = "Failed to remove legacy HKCU Run autostart value")]
        private static partial void LogLegacyRegistryCleanupFailed(ILogger logger, Exception ex);
    }
}

