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
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Security.Principal;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Service for managing application elevation and administrator privileges.
    /// </summary>
    public partial class ElevationService : IElevationService
    {
        private readonly ILogger<ElevationService> logger;
        private readonly ISecurityService securityService;
        private readonly SemaphoreSlim elevationRequestSemaphore = new(1, 1);

        public ElevationService(ILogger<ElevationService> logger, ISecurityService securityService)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.securityService = securityService ?? throw new ArgumentNullException(nameof(securityService));
        }

        public bool IsRunningAsAdministrator()
        {
            try
            {
                var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                var isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);

                LogAdministratorPrivilegeCheck(this.logger, isAdmin);
                return isAdmin;
            }
            catch (Exception ex)
            {
                LogPrivilegeCheckFailed(this.logger, ex);
                return false;
            }
        }

        public async Task<bool> RequestElevationIfNeeded()
        {
            if (this.IsRunningAsAdministrator())
            {
                LogAlreadyElevated(this.logger);
                return true;
            }

            LogRequestingElevation(this.logger);

            // Show elevation prompt to user
            var result = System.Windows.MessageBox.Show(
                "ThreadPilot requires administrator privileges to manage process affinity and power plans.\n\n" +
                "Would you like to restart the application with administrator privileges?",
                "Administrator Privileges Required",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                LogUserDeclinedElevation(this.logger);
                return false;
            }

            return await this.RestartWithElevation();
        }

        public async Task<bool> RestartWithElevation(string[]? arguments = null)
        {
            await this.elevationRequestSemaphore.WaitAsync();
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                var executablePath = currentProcess.MainModule?.FileName;

                if (string.IsNullOrWhiteSpace(executablePath) || !Path.IsPathFullyQualified(executablePath) || !File.Exists(executablePath))
                {
                    LogMissingExecutablePath(this.logger);
                    return false;
                }

                // Combine current arguments with any additional arguments
                var currentArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();
                var allArgs = arguments != null ? currentArgs.Concat(arguments).ToArray() : currentArgs;
                var argumentString = string.Join(" ", allArgs.Select(EscapeCommandLineArgument));
                var workingDirectory = Path.GetDirectoryName(executablePath);

                var startInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = argumentString,
                    UseShellExecute = true,
                    Verb = "runas", // This triggers UAC elevation
                    WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? Environment.SystemDirectory : workingDirectory,
                };

                LogStartingElevatedProcess(this.logger, executablePath, argumentString);

                var elevatedProcess = Process.Start(startInfo);
                if (elevatedProcess != null)
                {
                    LogElevatedProcessStarted(this.logger);

                    // Audit the elevation request
                    await this.securityService.AuditElevatedAction("ApplicationRestart", "Self", true);

                    // Shutdown current instance
                    await Task.Delay(1000); // Give the new process time to start
                    System.Windows.Application.Current.Shutdown();
                    return true;
                }
                else
                {
                    LogElevatedProcessStartFailed(this.logger);
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogRestartWithElevationFailed(this.logger, ex);
                await this.securityService.AuditElevatedAction("ApplicationRestart", "Self", false);

                // Show user-friendly error message
                System.Windows.MessageBox.Show(
                    "Failed to restart with administrator privileges. Please manually run ThreadPilot as administrator.",
                    "Elevation Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return false;
            }
            finally
            {
                this.elevationRequestSemaphore.Release();
            }
        }

        public bool ValidateElevationForOperation(string operation)
        {
            var isElevated = this.IsRunningAsAdministrator();
            var isValidOperation = this.securityService.ValidateElevatedOperation(operation);

            var canPerform = isElevated && isValidOperation;

            LogElevationValidation(this.logger, operation, isElevated, isValidOperation, canPerform);

            return canPerform;
        }

        public string GetElevationStatus()
        {
            return this.IsRunningAsAdministrator()
                ? "Running with Administrator privileges"
                : "Running with limited privileges";
        }

        private static string EscapeCommandLineArgument(string argument)
        {
            if (string.IsNullOrEmpty(argument))
            {
                return "\"\"";
            }

            var escaped = new StringBuilder();
            escaped.Append('"');

            foreach (var c in argument)
            {
                if (c == '"')
                {
                    escaped.Append("\\\"");
                }
                else
                {
                    escaped.Append(c);
                }
            }

            escaped.Append('"');
            return escaped.ToString();
        }

        [LoggerMessage(EventId = 4100, Level = LogLevel.Debug, Message = "Administrator privilege check: {IsAdmin}")]
        private static partial void LogAdministratorPrivilegeCheck(ILogger logger, bool isAdmin);

        [LoggerMessage(EventId = 4101, Level = LogLevel.Error, Message = "Failed to check administrator privileges")]
        private static partial void LogPrivilegeCheckFailed(ILogger logger, Exception ex);

        [LoggerMessage(EventId = 4102, Level = LogLevel.Debug, Message = "Application is already running with administrator privileges")]
        private static partial void LogAlreadyElevated(ILogger logger);

        [LoggerMessage(EventId = 4103, Level = LogLevel.Information, Message = "Requesting elevation to administrator privileges")]
        private static partial void LogRequestingElevation(ILogger logger);

        [LoggerMessage(EventId = 4104, Level = LogLevel.Information, Message = "User declined elevation request")]
        private static partial void LogUserDeclinedElevation(ILogger logger);

        [LoggerMessage(EventId = 4105, Level = LogLevel.Error, Message = "Could not determine executable path for elevation")]
        private static partial void LogMissingExecutablePath(ILogger logger);

        [LoggerMessage(EventId = 4106, Level = LogLevel.Information, Message = "Starting elevated process: {FileName} {Arguments}")]
        private static partial void LogStartingElevatedProcess(ILogger logger, string fileName, string arguments);

        [LoggerMessage(EventId = 4107, Level = LogLevel.Information, Message = "Elevated process started successfully. Shutting down current instance.")]
        private static partial void LogElevatedProcessStarted(ILogger logger);

        [LoggerMessage(EventId = 4108, Level = LogLevel.Error, Message = "Failed to start elevated process")]
        private static partial void LogElevatedProcessStartFailed(ILogger logger);

        [LoggerMessage(EventId = 4109, Level = LogLevel.Error, Message = "Failed to restart with elevation")]
        private static partial void LogRestartWithElevationFailed(ILogger logger, Exception ex);

        [LoggerMessage(
            EventId = 4110,
            Level = LogLevel.Debug,
            Message = "Elevation validation for operation '{Operation}': Elevated={IsElevated}, Valid={IsValid}, CanPerform={CanPerform}")]
        private static partial void LogElevationValidation(ILogger logger, string operation, bool isElevated, bool isValid, bool canPerform);
    }
}

