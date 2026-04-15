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
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using Microsoft.Win32.SafeHandles;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Service for security validation and auditing of elevated operations.
    /// </summary>
    public class SecurityService : ISecurityService
    {
        private readonly ILogger<SecurityService> logger;
        private readonly IEnhancedLoggingService enhancedLogger;

        // List of operations that are allowed to be performed with elevated privileges
        private static readonly string[] AllowedElevatedOperations = new[]
        {
            "SetProcessAffinity",
            "SetProcessPriority",
            "ChangePowerPlan",
            "ImportPowerPlan",
            "CreatePowerPlan",
            "DeletePowerPlan",
            "ModifyPowerPlanSettings",
            "ApplicationRestart",
            "CreateScheduledTask",
            "DeleteScheduledTask",
            "ModifyRegistryAutostart",
        };

        // List of critical system processes that should not be modified
        private static readonly string[] ProtectedProcesses = new[]
        {
            "System",
            "csrss",
            "winlogon",
            "services",
            "lsass",
            "svchost",
            "dwm",
            "explorer",
            "wininit",
            "smss",
            "WmiPrvSE",
            "MsMpEng",
            "SecurityHealthService",
            "audiodg",
        };

        private const uint ProcessQueryLimitedInformation = 0x1000;
        private const int ProcessProtectionInformationClass = 61;

        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessProtectionInformation
        {
            public byte ProtectionLevel;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern SafeProcessHandle OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

        [DllImport("ntdll.dll")]
        private static extern int NtQueryInformationProcess(
            IntPtr processHandle,
            int processInformationClass,
            out ProcessProtectionInformation processInformation,
            int processInformationLength,
            out int returnLength);

        public SecurityService(ILogger<SecurityService> logger, IEnhancedLoggingService enhancedLogger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.enhancedLogger = enhancedLogger ?? throw new ArgumentNullException(nameof(enhancedLogger));
        }

        public bool ValidateElevatedOperation(string operation)
        {
            if (string.IsNullOrWhiteSpace(operation))
            {
                this.logger.LogWarning("Attempted to validate null or empty operation");
                return false;
            }

            var normalizedOperation = SanitizeForLog(operation);

            var isAllowed = AllowedElevatedOperations.Contains(normalizedOperation, StringComparer.OrdinalIgnoreCase);

            if (!isAllowed)
            {
                this.logger.LogWarning("Attempted to perform unauthorized elevated operation: {Operation}", normalizedOperation);
            }
            else
            {
                this.logger.LogDebug("Validated elevated operation: {Operation}", normalizedOperation);
            }

            return isAllowed;
        }

        public async Task AuditElevatedAction(string action, string target, bool success)
        {
            var normalizedAction = SanitizeForLog(action);
            var normalizedTarget = SanitizeForLog(target);
            var logLevel = success ? LogLevel.Information : LogLevel.Warning;
            var message = $"Elevated action performed: {normalizedAction} on {normalizedTarget} - Success: {success}";

            this.logger.Log(logLevel, message);

            // Use enhanced logging for structured audit trail
            await this.enhancedLogger.LogSystemEventAsync(
                success ? "ElevatedActionSuccess" : "ElevatedActionFailure",
                $"Security audit: {normalizedAction} on {normalizedTarget} - Success: {success}",
                logLevel).ConfigureAwait(false);
        }

        public bool ValidateProcessOperation(string processName, string operation)
        {
            if (string.IsNullOrWhiteSpace(processName) || string.IsNullOrWhiteSpace(operation))
            {
                this.logger.LogWarning("Attempted to validate process operation with null or empty parameters");
                return false;
            }

            var normalizedProcessName = NormalizeProcessName(processName);
            var normalizedOperation = SanitizeForLog(operation);

            // Check if the process is in the protected list
            var isProtected = ProtectedProcesses.Contains(normalizedProcessName, StringComparer.OrdinalIgnoreCase);

            if (isProtected)
            {
                this.logger.LogWarning(
                    "Attempted to perform operation '{Operation}' on protected process '{ProcessName}'",
                    normalizedOperation, normalizedProcessName);
                return false;
            }

            // Validate the operation itself
            var isValidOperation = normalizedOperation switch
            {
                "SetProcessAffinity" => true,
                "SetProcessPriority" => true,
                "TerminateProcess" => false, // We don't allow process termination
                _ => false,
            };

            if (!isValidOperation)
            {
                this.logger.LogWarning(
                    "Attempted to perform invalid process operation '{Operation}' on '{ProcessName}'",
                    normalizedOperation, normalizedProcessName);
            }

            return isValidOperation;
        }

        public bool IsProtected(Process process)
        {
            ArgumentNullException.ThrowIfNull(process);

            var normalizedName = NormalizeProcessName(process.ProcessName);
            if (ProtectedProcesses.Contains(normalizedName, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }

            try
            {
                using var handle = OpenProcess(ProcessQueryLimitedInformation, false, process.Id);
                if (handle.IsInvalid)
                {
                    var error = Marshal.GetLastWin32Error();
                    this.logger.LogDebug(
                        "OpenProcess failed for PID {ProcessId} with Win32 error {ErrorCode}",
                        process.Id,
                        error);
                    return false;
                }

                var status = NtQueryInformationProcess(
                    handle.DangerousGetHandle(),
                    ProcessProtectionInformationClass,
                    out var protectionInfo,
                    Marshal.SizeOf<ProcessProtectionInformation>(),
                    out _);

                if (status != 0)
                {
                    this.logger.LogDebug(
                        "NtQueryInformationProcess returned status {Status} for PID {ProcessId}",
                        status,
                        process.Id);
                    return false;
                }

                return protectionInfo.ProtectionLevel != 0;
            }
            catch (Win32Exception ex)
            {
                this.logger.LogDebug(ex, "Win32 error while checking protected process state for PID {ProcessId}", process.Id);
                return false;
            }
            catch (Exception ex)
            {
                this.logger.LogDebug(ex, "Failed dynamic protected-process check for PID {ProcessId}", process.Id);
                return false;
            }
        }

        public bool ValidatePowerPlanOperation(string powerPlanId, string operation)
        {
            if (string.IsNullOrWhiteSpace(powerPlanId) || string.IsNullOrWhiteSpace(operation))
            {
                this.logger.LogWarning("Attempted to validate power plan operation with null or empty parameters");
                return false;
            }

            // Validate the operation
            var isValidOperation = operation switch
            {
                "ChangePowerPlan" => true,
                "ImportPowerPlan" => true,
                "CreatePowerPlan" => true,
                "DeletePowerPlan" => !IsSystemPowerPlan(powerPlanId), // Don't allow deletion of system power plans
                "ModifyPowerPlanSettings" => true,
                _ => false,
            };

            if (!isValidOperation)
            {
                this.logger.LogWarning(
                    "Attempted to perform invalid power plan operation '{Operation}' on '{PowerPlanId}'",
                    operation, powerPlanId);
            }

            return isValidOperation;
        }

        public string[] GetAllowedElevatedOperations()
        {
            return AllowedElevatedOperations.ToArray();
        }

        private static bool IsSystemPowerPlan(string powerPlanId)
        {
            // Common system power plan GUIDs
            var systemPowerPlans = new[]
            {
                "381b4222-f694-41f0-9685-ff5bb260df2e", // Balanced
                "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", // High performance
                "a1841308-3541-4fab-bc81-f71556f20b4a",  // Power saver
            };

            return systemPowerPlans.Contains(powerPlanId, StringComparer.OrdinalIgnoreCase);
        }

        private static string NormalizeProcessName(string processName)
        {
            var sanitized = SanitizeForLog(processName);
            return Path.GetFileNameWithoutExtension(sanitized);
        }

        private static string SanitizeForLog(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var sanitized = value
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Replace('\t', ' ')
                .Trim();

            return sanitized.Length > 200
                ? sanitized[..200]
                : sanitized;
        }
    }
}

