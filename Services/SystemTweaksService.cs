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
    using System.Management;
    using System.ServiceProcess;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Win32;

    /// <summary>
    /// Service for managing Windows system tweaks and optimizations.
    /// </summary>
    public class SystemTweaksService : ISystemTweaksService
    {
        private static readonly string BcdEditExecutablePath = Path.Combine(Environment.SystemDirectory, "bcdedit.exe");
        private static readonly string PowerCfgExecutablePath = Path.Combine(Environment.SystemDirectory, "powercfg.exe");
        private static readonly string ScExecutablePath = Path.Combine(Environment.SystemDirectory, "sc.exe");
        private static readonly HashSet<string> AllowedExecutablePaths = new(StringComparer.OrdinalIgnoreCase)
        {
            Path.GetFullPath(BcdEditExecutablePath),
            Path.GetFullPath(PowerCfgExecutablePath),
            Path.GetFullPath(ScExecutablePath),
        };

        private static readonly Regex HexValueRegex = new("0x([0-9a-fA-F]+)", RegexOptions.Compiled);
        private static readonly Regex ServiceNameRegex = new("^[A-Za-z0-9_.-]+$", RegexOptions.Compiled);
        private static readonly TimeSpan ExternalCommandTimeout = TimeSpan.FromSeconds(20);
        private const string ProcessorSubgroupAlias = "SUB_PROCESSOR";
        private const string CoreParkingSettingAlias = "CPMINCORES";
        private const string CStatesSettingAlias = "IDLEDISABLE";
        private const string CoreParkingVisibilityKeyPath = @"SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\0cc5b647-c1df-4637-891a-dec35c318583";
        private const string PriorityControlKeyPath = @"SYSTEM\CurrentControlSet\Control\PriorityControl";
        private const string PrioritySeparationValueName = "Win32PrioritySeparation";
        private readonly ILogger<SystemTweaksService> logger;
        private readonly IElevationService elevationService;

        public event EventHandler<TweakStatusChangedEventArgs>? TweakStatusChanged;

        public SystemTweaksService(
            ILogger<SystemTweaksService> logger,
            IElevationService elevationService)
        {
            this.logger = logger;
            this.elevationService = elevationService;
        }

        public async Task<TweakStatus> GetCoreParkingStatusAsync()
        {
            try
            {
                await this.EnsurePowerSettingVisibleAsync(ProcessorSubgroupAlias, CoreParkingSettingAlias);

                var acValue = await this.GetPowerCfgAcSettingValueAsync(ProcessorSubgroupAlias, CoreParkingSettingAlias);
                if (!acValue.HasValue)
                {
                    return new TweakStatus { IsAvailable = false, ErrorMessage = "Could not query Core Parking value via powercfg" };
                }

                // ON = disable parking (keep all cores unparked, typically 100)
                var isEnabled = acValue.Value >= 100;

                return new TweakStatus
                {
                    IsEnabled = isEnabled,
                    IsAvailable = true,
                    Description = "ON disables core parking (all cores unparked); OFF allows parking",
                };
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error getting Core Parking status");
                return new TweakStatus { IsAvailable = false, ErrorMessage = ex.Message };
            }
        }

        public async Task<bool> SetCoreParkingAsync(bool enabled)
        {
            try
            {
                if (!this.elevationService.IsRunningAsAdministrator())
                {
                    this.logger.LogWarning("Administrator privileges required to modify Core Parking");
                    return false;
                }

                await this.EnsurePowerSettingVisibleAsync(ProcessorSubgroupAlias, CoreParkingSettingAlias);

                var acValue = enabled ? 100 : 10;
                var setValueResult = await RunProcessAsync(
                    PowerCfgExecutablePath,
                    $"-setacvalueindex SCHEME_CURRENT {ProcessorSubgroupAlias} {CoreParkingSettingAlias} {acValue}");
                if (setValueResult.ExitCode != 0)
                {
                    this.logger.LogError(
                        "Failed setting Core Parking AC value. ExitCode={ExitCode}, Error={Error}",
                        setValueResult.ExitCode, setValueResult.StandardError);
                    return false;
                }

                var activateResult = await RunProcessAsync(PowerCfgExecutablePath, "/setactive SCHEME_CURRENT");
                if (activateResult.ExitCode != 0)
                {
                    this.logger.LogError(
                        "Failed activating current power scheme after Core Parking change. ExitCode={ExitCode}, Error={Error}",
                        activateResult.ExitCode, activateResult.StandardError);
                    return false;
                }

                // Keep setting visible in Windows advanced power UI if the key exists.
                using var visibilityKey = Registry.LocalMachine.OpenSubKey(CoreParkingVisibilityKeyPath, true);
                if (visibilityKey != null)
                {
                    visibilityKey.SetValue("Attributes", 2, RegistryValueKind.DWord);
                }

                var status = await this.GetCoreParkingStatusAsync();
                this.TweakStatusChanged?.Invoke(this, new TweakStatusChangedEventArgs("CoreParking", status));

                this.logger.LogInformation("Core Parking {Status}", enabled ? "enabled" : "disabled");
                return true;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error setting Core Parking to {Enabled}", enabled);
                return false;
            }
        }

        public async Task<TweakStatus> GetCStatesStatusAsync()
        {
            try
            {
                await this.EnsurePowerSettingVisibleAsync(ProcessorSubgroupAlias, CStatesSettingAlias);

                var acValue = await this.GetPowerCfgAcSettingValueAsync(ProcessorSubgroupAlias, CStatesSettingAlias);
                if (!acValue.HasValue)
                {
                    return new TweakStatus { IsAvailable = false, ErrorMessage = "Could not query C-States value via powercfg" };
                }

                // ON = enable C-States (IDLEDISABLE=0), OFF = disable C-States (IDLEDISABLE=1)
                var isEnabled = acValue.Value == 0;

                return new TweakStatus
                {
                    IsEnabled = isEnabled,
                    IsAvailable = true,
                    Description = "ON enables C-States; OFF disables C-States for lower latency",
                };
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error getting C-States status");
                return new TweakStatus { IsAvailable = false, ErrorMessage = ex.Message };
            }
        }

        public async Task<bool> SetCStatesAsync(bool enabled)
        {
            try
            {
                if (!this.elevationService.IsRunningAsAdministrator())
                {
                    this.logger.LogWarning("Administrator privileges required to modify C-States");
                    return false;
                }

                await this.EnsurePowerSettingVisibleAsync(ProcessorSubgroupAlias, CStatesSettingAlias);

                var value = enabled ? 0 : 1;
                var setValueResult = await RunProcessAsync(
                    PowerCfgExecutablePath,
                    $"-setacvalueindex SCHEME_CURRENT {ProcessorSubgroupAlias} {CStatesSettingAlias} {value}");
                if (setValueResult.ExitCode != 0)
                {
                    this.logger.LogError(
                        "Failed setting C-States AC value. ExitCode={ExitCode}, Error={Error}",
                        setValueResult.ExitCode, setValueResult.StandardError);
                    return false;
                }

                var activateResult = await RunProcessAsync(PowerCfgExecutablePath, "/setactive SCHEME_CURRENT");
                if (activateResult.ExitCode != 0)
                {
                    this.logger.LogError(
                        "Failed activating current power scheme after C-States change. ExitCode={ExitCode}, Error={Error}",
                        activateResult.ExitCode, activateResult.StandardError);
                    return false;
                }

                var status = await this.GetCStatesStatusAsync();
                this.TweakStatusChanged?.Invoke(this, new TweakStatusChangedEventArgs("CStates", status));

                this.logger.LogInformation("C-States {Status}", enabled ? "enabled" : "disabled");
                return true;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error setting C-States to {Enabled}", enabled);
                return false;
            }
        }

        public Task<TweakStatus> GetSysMainStatusAsync()
        {
            try
            {
                using var serviceController = new ServiceController("SysMain");
                serviceController.Refresh();
                var isEnabled = serviceController.StartType != ServiceStartMode.Disabled;
                var isAvailable = true;

                return Task.FromResult(new TweakStatus
                {
                    IsEnabled = isEnabled,
                    IsAvailable = isAvailable,
                    Description = "Windows Superfetch/SysMain service for memory management",
                });
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error getting SysMain status");
                return Task.FromResult(new TweakStatus { IsAvailable = false, ErrorMessage = ex.Message });
            }
        }

        public async Task<bool> SetSysMainAsync(bool enabled)
        {
            try
            {
                if (!this.elevationService.IsRunningAsAdministrator())
                {
                    this.logger.LogWarning("Administrator privileges required to modify SysMain service");
                    return false;
                }

                using var serviceController = new ServiceController("SysMain");
                if (!await this.SetServiceStartModeAsync("SysMain", enabled ? ServiceStartMode.Automatic : ServiceStartMode.Disabled))
                {
                    this.logger.LogError("Failed to set SysMain startup mode");
                    return false;
                }

                serviceController.Refresh();

                if (enabled && serviceController.Status == ServiceControllerStatus.Stopped)
                {
                    serviceController.Start();
                    serviceController.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                }
                else if (!enabled && (serviceController.Status == ServiceControllerStatus.Running || serviceController.Status == ServiceControllerStatus.Paused))
                {
                    serviceController.Stop();
                    serviceController.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                }

                var status = await this.GetSysMainStatusAsync();
                this.TweakStatusChanged?.Invoke(this, new TweakStatusChangedEventArgs("SysMain", status));

                this.logger.LogInformation("SysMain service {Status}", enabled ? "started" : "stopped");
                return true;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error setting SysMain service to {Enabled}", enabled);
                return false;
            }
        }

        public Task<TweakStatus> GetPrefetchStatusAsync()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters");
                if (key == null)
                {
                    return Task.FromResult(new TweakStatus { IsAvailable = false, ErrorMessage = "Prefetch registry key not found" });
                }

                var enablePrefetcher = key.GetValue("EnablePrefetcher");
                var isEnabled = enablePrefetcher?.ToString() != "0"; // 0 = disabled, 1-3 = enabled

                return Task.FromResult(new TweakStatus
                {
                    IsEnabled = isEnabled,
                    IsAvailable = true,
                    Description = "Windows Prefetch feature for faster application loading",
                });
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error getting Prefetch status");
                return Task.FromResult(new TweakStatus { IsAvailable = false, ErrorMessage = ex.Message });
            }
        }

        public async Task<bool> SetPrefetchAsync(bool enabled)
        {
            try
            {
                if (!this.elevationService.IsRunningAsAdministrator())
                {
                    this.logger.LogWarning("Administrator privileges required to modify Prefetch");
                    return false;
                }

                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters", true);
                if (key == null)
                {
                    this.logger.LogError("Prefetch registry key not found");
                    return false;
                }

                // Set EnablePrefetcher: 0 = disabled, 3 = enabled for both applications and boot
                key.SetValue("EnablePrefetcher", enabled ? 3 : 0, RegistryValueKind.DWord);

                var status = await this.GetPrefetchStatusAsync();
                this.TweakStatusChanged?.Invoke(this, new TweakStatusChangedEventArgs("Prefetch", status));

                this.logger.LogInformation("Prefetch {Status}", enabled ? "enabled" : "disabled");
                return true;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error setting Prefetch to {Enabled}", enabled);
                return false;
            }
        }

        public Task<TweakStatus> GetPowerThrottlingStatusAsync()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling");
                if (key == null)
                {
                    return Task.FromResult(new TweakStatus { IsAvailable = false, ErrorMessage = "Power Throttling not available on this system" });
                }

                var powerThrottlingOff = ReadRegistryIntValue(key, "PowerThrottlingOff");
                // ON = disable throttling (PowerThrottlingOff=1)
                var isEnabled = powerThrottlingOff.GetValueOrDefault(0) == 1;

                return Task.FromResult(new TweakStatus
                {
                    IsEnabled = isEnabled,
                    IsAvailable = true,
                    Description = "ON disables Windows Power Throttling for sustained performance",
                });
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error getting Power Throttling status");
                return Task.FromResult(new TweakStatus { IsAvailable = false, ErrorMessage = ex.Message });
            }
        }

        public async Task<bool> SetPowerThrottlingAsync(bool enabled)
        {
            try
            {
                if (!this.elevationService.IsRunningAsAdministrator())
                {
                    this.logger.LogWarning("Administrator privileges required to modify Power Throttling");
                    return false;
                }

                using var key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling");
                if (key == null)
                {
                    this.logger.LogError("Could not create Power Throttling registry key");
                    return false;
                }

                // Set PowerThrottlingOff: 1 = throttling disabled, 0 = throttling enabled
                key.SetValue("PowerThrottlingOff", enabled ? 1 : 0, RegistryValueKind.DWord);

                var status = await this.GetPowerThrottlingStatusAsync();
                this.TweakStatusChanged?.Invoke(this, new TweakStatusChangedEventArgs("PowerThrottling", status));

                this.logger.LogInformation("Power Throttling {Status}", enabled ? "enabled" : "disabled");
                return true;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error setting Power Throttling to {Enabled}", enabled);
                return false;
            }
        }

        public async Task<TweakStatus> GetHpetStatusAsync()
        {
            try
            {
                var result = await RunProcessAsync(BcdEditExecutablePath, "/enum");
                if (result.ExitCode != 0)
                {
                    return new TweakStatus
                    {
                        IsAvailable = false,
                        ErrorMessage = string.IsNullOrWhiteSpace(result.StandardError)
                            ? "Could not query bcdedit status"
                            : result.StandardError,
                    };
                }

                var output = result.StandardOutput;

                var platformClockLine = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault(l => l.TrimStart().StartsWith("useplatformclock", StringComparison.OrdinalIgnoreCase));

                // ON = disable HPET (useplatformclock removed/absent)
                var isEnabled = true;
                if (!string.IsNullOrWhiteSpace(platformClockLine))
                {
                    isEnabled = !platformClockLine.TrimEnd().EndsWith("Yes", StringComparison.OrdinalIgnoreCase);
                }

                return new TweakStatus
                {
                    IsEnabled = isEnabled,
                    IsAvailable = true,
                    Description = "High Precision Event Timer for system timing",
                };
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error getting HPET status");
                return new TweakStatus { IsAvailable = false, ErrorMessage = ex.Message };
            }
        }

        public async Task<bool> SetHpetAsync(bool enabled)
        {
            try
            {
                if (!this.elevationService.IsRunningAsAdministrator())
                {
                    this.logger.LogWarning("Administrator privileges required to modify HPET");
                    return false;
                }

                // ON = disable HPET (/deletevalue), OFF = force HPET (/set true)
                var arguments = enabled ? "/deletevalue useplatformclock" : "/set useplatformclock true";
                var commandResult = await RunProcessAsync(BcdEditExecutablePath, arguments);
                var success = commandResult.ExitCode == 0;

                if (success)
                {
                    var status = await this.GetHpetStatusAsync();
                    this.TweakStatusChanged?.Invoke(this, new TweakStatusChangedEventArgs("Hpet", status));
                    this.logger.LogInformation("HPET {Status}", enabled ? "enabled" : "disabled");
                }
                else
                {
                    this.logger.LogWarning(
                        "Failed to set HPET. ExitCode={ExitCode}, Error={Error}",
                        commandResult.ExitCode, commandResult.StandardError);
                }

                return success;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error setting HPET to {Enabled}", enabled);
                return false;
            }
        }

        public Task<TweakStatus> GetHighSchedulingCategoryStatusAsync()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(PriorityControlKeyPath);
                if (key == null)
                {
                    return Task.FromResult(new TweakStatus { IsAvailable = false, ErrorMessage = "PriorityControl registry key not found" });
                }

                var rawValue = ReadRegistryIntValue(key, PrioritySeparationValueName);
                var isEnabled = rawValue.GetValueOrDefault(2) == 38;

                return Task.FromResult(new TweakStatus
                {
                    IsEnabled = isEnabled,
                    IsAvailable = true,
                    Description = "ON applies high foreground boost (Win32PrioritySeparation=38)",
                });
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error getting High Scheduling Category status");
                return Task.FromResult(new TweakStatus { IsAvailable = false, ErrorMessage = ex.Message });
            }
        }

        public async Task<bool> SetHighSchedulingCategoryAsync(bool enabled)
        {
            try
            {
                if (!this.elevationService.IsRunningAsAdministrator())
                {
                    this.logger.LogWarning("Administrator privileges required to modify High Scheduling Category");
                    return false;
                }

                using var key = Registry.LocalMachine.OpenSubKey(PriorityControlKeyPath, true);
                if (key == null)
                {
                    this.logger.LogError("PriorityControl registry key not found");
                    return false;
                }

                // ON = 38 (Short, Variable, High), OFF = 2 (default/minimal boost)
                key.SetValue(PrioritySeparationValueName, enabled ? 38 : 2, RegistryValueKind.DWord);

                var status = await this.GetHighSchedulingCategoryStatusAsync();
                this.TweakStatusChanged?.Invoke(this, new TweakStatusChangedEventArgs("HighSchedulingCategory", status));

                this.logger.LogInformation("High Scheduling Category {Status}", enabled ? "enabled" : "disabled");
                return true;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error setting High Scheduling Category to {Enabled}", enabled);
                return false;
            }
        }

        private async Task<bool> SetServiceStartModeAsync(string serviceName, ServiceStartMode mode)
        {
            if (!ServiceNameRegex.IsMatch(serviceName))
            {
                this.logger.LogWarning("Rejected invalid service name format: {ServiceName}", serviceName);
                return false;
            }

            var startModeValue = mode switch
            {
                ServiceStartMode.Automatic => "auto",
                ServiceStartMode.Manual => "demand",
                ServiceStartMode.Disabled => "disabled",
                _ => "demand",
            };

            var result = await RunProcessAsync(ScExecutablePath, $"config \"{serviceName}\" start= {startModeValue}");
            if (result.ExitCode != 0)
            {
                this.logger.LogWarning(
                    "Failed to update service start mode for {ServiceName}. ExitCode={ExitCode}, Error={Error}",
                    serviceName, result.ExitCode, result.StandardError);
                return false;
            }

            return true;
        }

        private async Task EnsurePowerSettingVisibleAsync(string subgroupAlias, string settingAlias)
        {
            var attributesResult = await RunProcessAsync(
                PowerCfgExecutablePath,
                $"-attributes {subgroupAlias} {settingAlias} -ATTRIB_HIDE");

            if (attributesResult.ExitCode != 0)
            {
                this.logger.LogDebug(
                    "Could not unhide power setting {Subgroup}/{Setting}. ExitCode={ExitCode}, Error={Error}",
                    subgroupAlias, settingAlias, attributesResult.ExitCode, attributesResult.StandardError);
            }
        }

        private async Task<int?> GetPowerCfgAcSettingValueAsync(string subgroupAlias, string settingAlias)
        {
            var queryResult = await RunProcessAsync(
                PowerCfgExecutablePath,
                $"-query SCHEME_CURRENT {subgroupAlias} {settingAlias}");

            if (queryResult.ExitCode != 0)
            {
                this.logger.LogWarning(
                    "powercfg query failed for {Subgroup}/{Setting}. ExitCode={ExitCode}, Error={Error}",
                    subgroupAlias, settingAlias, queryResult.ExitCode, queryResult.StandardError);
                return null;
            }

            var line = queryResult.StandardOutput
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(l => l.Contains("Current AC Power Setting Index", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(line))
            {
                return null;
            }

            var match = HexValueRegex.Match(line);
            if (!match.Success)
            {
                return null;
            }

            return int.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
        }

        private static async Task<ProcessResult> RunProcessAsync(string fileName, string arguments)
        {
            if (!IsAllowedExecutable(fileName))
            {
                return new ProcessResult(-1, string.Empty, $"Executable not allowed: {fileName}");
            }

            var processInfo = new ProcessStartInfo
            {
                FileName = Path.GetFullPath(fileName),
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                return new ProcessResult(-1, string.Empty, "Could not start process");
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            var exitTask = process.WaitForExitAsync();
            var completedTask = await Task.WhenAny(exitTask, Task.Delay(ExternalCommandTimeout));
            if (completedTask != exitTask)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best-effort kill for stuck child processes.
                }

                return new ProcessResult(-1, await outputTask, $"Process timed out after {ExternalCommandTimeout.TotalSeconds} seconds");
            }

            await exitTask;

            return new ProcessResult(process.ExitCode, await outputTask, await errorTask);
        }

        private static bool IsAllowedExecutable(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || !Path.IsPathRooted(fileName))
            {
                return false;
            }

            var fullPath = Path.GetFullPath(fileName);
            return AllowedExecutablePaths.Contains(fullPath) && File.Exists(fullPath);
        }

        private static int? ReadRegistryIntValue(RegistryKey key, string valueName)
        {
            var raw = key.GetValue(valueName);
            return raw switch
            {
                int intValue => intValue,
                uint uintValue => unchecked((int)uintValue),
                long longValue when longValue >= int.MinValue && longValue <= int.MaxValue => (int)longValue,
                string stringValue when int.TryParse(stringValue, out var parsed) => parsed,
                _ => null,
            };
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

        public Task<TweakStatus> GetMenuShowDelayStatusAsync()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
                if (key == null)
                {
                    return Task.FromResult(new TweakStatus { IsAvailable = false, ErrorMessage = "Desktop registry key not found" });
                }

                var menuShowDelay = key.GetValue("MenuShowDelay");
                var isEnabled = menuShowDelay?.ToString() != "0"; // 0 = no delay, >0 = delay enabled

                return Task.FromResult(new TweakStatus
                {
                    IsEnabled = isEnabled,
                    IsAvailable = true,
                    Description = "Delay before showing context menus",
                });
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error getting Menu Show Delay status");
                return Task.FromResult(new TweakStatus { IsAvailable = false, ErrorMessage = ex.Message });
            }
        }

        public async Task<bool> SetMenuShowDelayAsync(bool enabled)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true);
                if (key == null)
                {
                    this.logger.LogError("Desktop registry key not found");
                    return false;
                }

                // Set MenuShowDelay: 0 = no delay, 400 = default delay
                key.SetValue("MenuShowDelay", enabled ? "400" : "0", RegistryValueKind.String);

                var status = await this.GetMenuShowDelayStatusAsync();
                this.TweakStatusChanged?.Invoke(this, new TweakStatusChangedEventArgs("MenuShowDelay", status));

                this.logger.LogInformation("Menu Show Delay {Status}", enabled ? "enabled" : "disabled");
                return true;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error setting Menu Show Delay to {Enabled}", enabled);
                return false;
            }
        }

        public async Task RefreshAllStatusesAsync()
        {
            try
            {
                this.logger.LogInformation("Refreshing all system tweak statuses");

                var tasks = new[]
                {
                    this.GetCoreParkingStatusAsync(),
                    this.GetCStatesStatusAsync(),
                    this.GetSysMainStatusAsync(),
                    this.GetPrefetchStatusAsync(),
                    this.GetPowerThrottlingStatusAsync(),
                    this.GetHpetStatusAsync(),
                    this.GetHighSchedulingCategoryStatusAsync(),
                    this.GetMenuShowDelayStatusAsync(),
                };

                await Task.WhenAll(tasks);
                this.logger.LogInformation("All system tweak statuses refreshed");
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error refreshing system tweak statuses");
            }
        }
    }
}

