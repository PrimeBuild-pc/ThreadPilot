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
    using System.Reflection;
    using System.Security;
    using System.Security.Principal;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Manages Scheduled Tasks used for persistent elevated launch and elevated autostart.
    /// </summary>
    public partial class ElevatedTaskService : IElevatedTaskService
    {
        private static readonly string SchTasksExecutablePath = Path.Combine(Environment.SystemDirectory, "schtasks.exe");
        private static readonly TimeSpan ScheduledTaskTimeout = TimeSpan.FromSeconds(20);

        private readonly ILogger<ElevatedTaskService> logger;

        public ElevatedTaskService(ILogger<ElevatedTaskService> logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string LaunchTaskName => "ThreadPilot_Launch";

        public string AutostartTaskName => "ThreadPilot_Startup";

        public async Task<bool> EnsureLaunchTaskAsync()
        {
            try
            {
                var executablePath = this.GetExecutablePath();
                if (!IsValidExecutablePath(executablePath))
                {
                    LogSkipEnsureLaunchTaskInvalidPath(this.logger, executablePath ?? "(null)");
                    return false;
                }

                var taskXmlPath = Path.Combine(Path.GetTempPath(), $"threadpilot-launch-task-{Guid.NewGuid():N}.xml");
                try
                {
                    WriteLaunchTaskDefinition(taskXmlPath, executablePath);

                    var result = await RunSchTasksAsync(new List<string>
                    {
                        "/Create",
                        "/TN", this.LaunchTaskName,
                        "/XML", taskXmlPath,
                        "/F",
                    });

                    if (result.ExitCode == 0)
                    {
                        LogLaunchTaskEnsured(this.logger, this.LaunchTaskName, executablePath);
                        return true;
                    }

                    LogEnsureLaunchTaskFailed(this.logger, result.ExitCode, result.StandardError);
                    return false;
                }
                finally
                {
                    TryDeleteFile(taskXmlPath, this.logger);
                }
            }
            catch (Exception ex)
            {
                LogEnsureLaunchTaskException(this.logger, ex);
                return false;
            }
        }

        public async Task<bool> TryRunLaunchTaskAsync()
        {
            try
            {
                var result = await RunSchTasksAsync(new List<string>
                {
                    "/Run",
                    "/TN", this.LaunchTaskName,
                });

                if (result.ExitCode == 0)
                {
                    LogLaunchTaskStarted(this.logger, this.LaunchTaskName);
                    return true;
                }

                LogRunLaunchTaskFailed(this.logger, result.ExitCode, result.StandardError);
                return false;
            }
            catch (Exception ex)
            {
                LogRunLaunchTaskException(this.logger, ex);
                return false;
            }
        }

        public async Task<bool> EnsureAutostartTaskAsync(string executablePath, string arguments)
        {
            try
            {
                if (!IsValidExecutablePath(executablePath))
                {
                    LogSkipEnsureAutostartTaskInvalidPath(this.logger, executablePath);
                    return false;
                }

                var taskRunCommand = BuildCommand(executablePath, arguments);
                var result = await RunSchTasksAsync(new List<string>
                {
                    "/Create",
                    "/TN", this.AutostartTaskName,
                    "/TR", taskRunCommand,
                    "/SC", "ONLOGON",
                    "/RL", "HIGHEST",
                    "/F",
                    "/RU", Environment.UserName,
                });

                if (result.ExitCode == 0)
                {
                    LogAutostartTaskEnsured(this.logger, this.AutostartTaskName);
                    return true;
                }

                LogEnsureAutostartTaskFailed(this.logger, result.ExitCode, result.StandardError);
                return false;
            }
            catch (Exception ex)
            {
                LogEnsureAutostartTaskException(this.logger, ex);
                return false;
            }
        }

        public async Task<bool> RemoveAutostartTaskAsync()
        {
            try
            {
                var result = await RunSchTasksAsync(new List<string>
                {
                    "/Delete",
                    "/TN", this.AutostartTaskName,
                    "/F",
                });

                if (result.ExitCode == 0)
                {
                    LogAutostartTaskRemoved(this.logger, this.AutostartTaskName);
                    return true;
                }

                // Exit code 1 is expected when task doesn't exist; treat as already removed.
                if (result.ExitCode == 1)
                {
                    LogAutostartTaskAlreadyRemoved(this.logger, this.AutostartTaskName);
                    return true;
                }

                LogRemoveAutostartTaskFailed(this.logger, result.ExitCode, result.StandardError);
                return false;
            }
            catch (Exception ex)
            {
                LogRemoveAutostartTaskException(this.logger, ex);
                return false;
            }
        }

        public async Task<bool> IsAutostartTaskRegisteredAsync()
        {
            try
            {
                var result = await RunSchTasksAsync(new List<string>
                {
                    "/Query",
                    "/TN", this.AutostartTaskName,
                });

                var exists = result.ExitCode == 0;
                LogAutostartTaskQueryResult(this.logger, this.AutostartTaskName, exists, result.ExitCode);
                return exists;
            }
            catch (Exception ex)
            {
                LogAutostartTaskQueryException(this.logger, ex);
                return false;
            }
        }

        private string? GetExecutablePath()
        {
            try
            {
                var currentPath = Process.GetCurrentProcess().MainModule?.FileName;
                if (IsValidExecutablePath(currentPath))
                {
                    return currentPath;
                }

                var assemblyLocation = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrWhiteSpace(assemblyLocation) &&
                    assemblyLocation.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    var candidatePath = Path.ChangeExtension(assemblyLocation, ".exe");
                    if (IsValidExecutablePath(candidatePath))
                    {
                        return candidatePath;
                    }
                }

                return IsValidExecutablePath(assemblyLocation)
                    ? assemblyLocation
                    : null;
            }
            catch (Exception ex)
            {
                LogGetExecutablePathFailed(this.logger, ex);
                return null;
            }
        }

        private static string BuildCommand(string executablePath, string arguments)
        {
            var trimmedArguments = arguments?.Trim();
            return string.IsNullOrWhiteSpace(trimmedArguments)
                ? $"\"{executablePath}\""
                : $"\"{executablePath}\" {trimmedArguments}";
        }

        private static bool IsValidExecutablePath(string? executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath) || !Path.IsPathRooted(executablePath))
            {
                return false;
            }

            return File.Exists(executablePath) &&
                   string.Equals(Path.GetExtension(executablePath), ".exe", StringComparison.OrdinalIgnoreCase);
        }

        private static void WriteLaunchTaskDefinition(string taskXmlPath, string executablePath)
        {
            var userName = WindowsIdentity.GetCurrent().Name;
            if (string.IsNullOrWhiteSpace(userName))
            {
                throw new InvalidOperationException("Could not determine current user identity for launch task registration.");
            }

            var workingDirectory = Path.GetDirectoryName(executablePath);
            if (string.IsNullOrWhiteSpace(workingDirectory))
            {
                throw new InvalidOperationException("Could not determine working directory for launch task registration.");
            }

            var escapedUserName = SecurityElement.Escape(userName);
            var escapedExecutablePath = SecurityElement.Escape(executablePath);
            var escapedArguments = SecurityElement.Escape("--launched-via-task");
            var escapedWorkingDirectory = SecurityElement.Escape(workingDirectory);

            var taskXml = $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.4"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Author>ThreadPilot</Author>
    <Description>Launches ThreadPilot with highest available privileges on demand.</Description>
  </RegistrationInfo>
  <Principals>
    <Principal id=""Author"">
      <UserId>{escapedUserName}</UserId>
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>false</AllowHardTerminate>
    <StartWhenAvailable>false</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings>
      <StopOnIdleEnd>false</StopOnIdleEnd>
      <RestartOnIdle>false</RestartOnIdle>
    </IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>7</Priority>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>{escapedExecutablePath}</Command>
      <Arguments>{escapedArguments}</Arguments>
      <WorkingDirectory>{escapedWorkingDirectory}</WorkingDirectory>
    </Exec>
  </Actions>
</Task>";

            File.WriteAllText(taskXmlPath, taskXml, Encoding.Unicode);
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

        private static void TryDeleteFile(string path, ILogger logger)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                LogDeleteTemporaryFileFailed(logger, path, ex);
            }
        }

        [LoggerMessage(EventId = 4250, Level = LogLevel.Warning, Message = "Skipping launch task registration due to invalid executable path: {Path}")]
        private static partial void LogSkipEnsureLaunchTaskInvalidPath(ILogger logger, string path);

        [LoggerMessage(EventId = 4251, Level = LogLevel.Information, Message = "Ensured elevated launch task '{TaskName}' for executable '{ExecutablePath}'")]
        private static partial void LogLaunchTaskEnsured(ILogger logger, string taskName, string executablePath);

        [LoggerMessage(EventId = 4252, Level = LogLevel.Warning, Message = "Failed to ensure elevated launch task. Exit code: {ExitCode}, Error: {Error}")]
        private static partial void LogEnsureLaunchTaskFailed(ILogger logger, int exitCode, string error);

        [LoggerMessage(EventId = 4253, Level = LogLevel.Warning, Message = "Exception while ensuring elevated launch task")]
        private static partial void LogEnsureLaunchTaskException(ILogger logger, Exception ex);

        [LoggerMessage(EventId = 4254, Level = LogLevel.Information, Message = "Started elevated launch task '{TaskName}'")]
        private static partial void LogLaunchTaskStarted(ILogger logger, string taskName);

        [LoggerMessage(EventId = 4255, Level = LogLevel.Warning, Message = "Failed to run elevated launch task. Exit code: {ExitCode}, Error: {Error}")]
        private static partial void LogRunLaunchTaskFailed(ILogger logger, int exitCode, string error);

        [LoggerMessage(EventId = 4256, Level = LogLevel.Warning, Message = "Exception while running elevated launch task")]
        private static partial void LogRunLaunchTaskException(ILogger logger, Exception ex);

        [LoggerMessage(EventId = 4257, Level = LogLevel.Warning, Message = "Skipping autostart task registration due to invalid executable path: {Path}")]
        private static partial void LogSkipEnsureAutostartTaskInvalidPath(ILogger logger, string path);

        [LoggerMessage(EventId = 4258, Level = LogLevel.Information, Message = "Ensured elevated autostart task '{TaskName}'")]
        private static partial void LogAutostartTaskEnsured(ILogger logger, string taskName);

        [LoggerMessage(EventId = 4259, Level = LogLevel.Warning, Message = "Failed to ensure elevated autostart task. Exit code: {ExitCode}, Error: {Error}")]
        private static partial void LogEnsureAutostartTaskFailed(ILogger logger, int exitCode, string error);

        [LoggerMessage(EventId = 4260, Level = LogLevel.Warning, Message = "Exception while ensuring elevated autostart task")]
        private static partial void LogEnsureAutostartTaskException(ILogger logger, Exception ex);

        [LoggerMessage(EventId = 4261, Level = LogLevel.Information, Message = "Removed elevated autostart task '{TaskName}'")]
        private static partial void LogAutostartTaskRemoved(ILogger logger, string taskName);

        [LoggerMessage(EventId = 4262, Level = LogLevel.Debug, Message = "Elevated autostart task '{TaskName}' was already absent")]
        private static partial void LogAutostartTaskAlreadyRemoved(ILogger logger, string taskName);

        [LoggerMessage(EventId = 4263, Level = LogLevel.Warning, Message = "Failed to remove elevated autostart task. Exit code: {ExitCode}, Error: {Error}")]
        private static partial void LogRemoveAutostartTaskFailed(ILogger logger, int exitCode, string error);

        [LoggerMessage(EventId = 4264, Level = LogLevel.Warning, Message = "Exception while removing elevated autostart task")]
        private static partial void LogRemoveAutostartTaskException(ILogger logger, Exception ex);

        [LoggerMessage(EventId = 4265, Level = LogLevel.Debug, Message = "Autostart task query for '{TaskName}': Exists={Exists}, ExitCode={ExitCode}")]
        private static partial void LogAutostartTaskQueryResult(ILogger logger, string taskName, bool exists, int exitCode);

        [LoggerMessage(EventId = 4266, Level = LogLevel.Warning, Message = "Exception while querying elevated autostart task")]
        private static partial void LogAutostartTaskQueryException(ILogger logger, Exception ex);

        [LoggerMessage(EventId = 4267, Level = LogLevel.Warning, Message = "Failed to resolve executable path while ensuring elevated tasks")]
        private static partial void LogGetExecutablePathFailed(ILogger logger, Exception ex);

        [LoggerMessage(EventId = 4268, Level = LogLevel.Debug, Message = "Failed to delete temporary file '{Path}'")]
        private static partial void LogDeleteTemporaryFileFailed(ILogger logger, string path, Exception ex);

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
