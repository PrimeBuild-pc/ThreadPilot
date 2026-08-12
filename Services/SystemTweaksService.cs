namespace ThreadPilot.Services
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Management;
    using System.Runtime.InteropServices;
    using System.ServiceProcess;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Win32;

    public class SystemTweaksService : ISystemTweaksService
    {
        private static readonly string ScExecutablePath = Path.Combine(Environment.SystemDirectory, "sc.exe");
        private static readonly Regex ServiceNameRegex = new("^[A-Za-z0-9_.-]+$", RegexOptions.Compiled);
        private static readonly TimeSpan ExternalCommandTimeout = TimeSpan.FromSeconds(20);
        private static readonly Guid ProcessorSettingsSubgroupGuid = new("54533251-82be-4824-96c1-47b60b740d00");
        private static readonly Guid CoreParkingSettingGuid = new("0cc5b647-c1df-4637-891a-dec35c318583");
        private static readonly Guid ProcessorIdleDisableSettingGuid = new("5d76a2ca-e8c0-402f-a133-2158492d58ad");
        private static readonly Guid UsbSettingsSubgroupGuid = new("2a737441-1930-4402-8d77-b2bebba308a3");
        private static readonly Guid UsbSelectiveSuspendSettingGuid = new("48e6b7a6-50f5-4782-a5d4-53bb8f07e226");
        private static readonly string[] EthernetPowerSavingValueNames = ["*EEE", "AdvancedEEE", "EnableGreenEthernet", "PowerSavingMode"];
        private static readonly string[] InterruptModerationValueNames = ["*InterruptModeration"];
        private const string NetworkAdapterClassPath = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}";
        private const string MsiPropertiesSuffix = @"Device Parameters\Interrupt Management\MessageSignaledInterruptProperties";
        private const string GamesSchedulingKeyPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games";
        private const string SchedulingCategoryValueName = "Scheduling Category";
        internal const string HighSchedulingCategoryEnabledValue = "High";
        private const string HighSchedulingCategoryDisabledValue = "Medium";
        private readonly ILogger<SystemTweaksService> logger;
        private readonly IElevationService elevationService;
        private readonly IGameModeService gameModeService;

        public event EventHandler<TweakStatusChangedEventArgs>? TweakStatusChanged;

        public SystemTweaksService(
            ILogger<SystemTweaksService> logger,
            IElevationService elevationService,
            IGameModeService gameModeService)
        {
            this.logger = logger;
            this.elevationService = elevationService;
            this.gameModeService = gameModeService;
        }

        public async Task<TweakStatus> GetGameModeStatusAsync() => new()
        {
            IsEnabled = await this.gameModeService.IsGameModeEnabledAsync(),
            IsAvailable = true,
        };

        public async Task<bool> SetGameModeAsync(bool enabled)
        {
            var success = await this.gameModeService.SetGameModeAsync(enabled);
            if (success)
            {
                this.RaiseStatusChanged(nameof(SystemTweak.GameMode), await this.GetGameModeStatusAsync());
            }

            return success;
        }

        public Task<TweakStatus> GetUsbSelectiveSuspendStatusAsync()
        {
            var available = TryReadAcPowerSetting(
                UsbSettingsSubgroupGuid,
                UsbSelectiveSuspendSettingGuid,
                out var value,
                out var error);
            return Task.FromResult(new TweakStatus
            {
                IsEnabled = value != 0,
                IsAvailable = available,
                ErrorMessage = error,
            });
        }

        public async Task<bool> SetUsbSelectiveSuspendAsync(bool enabled)
        {
            string? error = "Administrator privileges are required.";
            if (!this.elevationService.IsRunningAsAdministrator() ||
                !TryWriteAcPowerSetting(
                    UsbSettingsSubgroupGuid,
                    UsbSelectiveSuspendSettingGuid,
                    enabled ? 1u : 0u,
                    out error))
            {
                this.logger.LogWarning("Could not update USB selective suspend: {Error}", error);
                return false;
            }

            var status = await this.GetUsbSelectiveSuspendStatusAsync();
            this.RaiseStatusChanged(nameof(SystemTweak.UsbSelectiveSuspend), status);
            return status.IsAvailable && status.IsEnabled == enabled;
        }

        public Task<TweakStatus> GetPointerPrecisionStatusAsync()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Mouse");
                var mouseSpeed = key?.GetValue("MouseSpeed")?.ToString();
                if (int.TryParse(mouseSpeed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var speed))
                {
                    return Task.FromResult(new TweakStatus { IsEnabled = speed != 0, IsAvailable = true });
                }

                var values = new int[3];
                var available = NativeMethods.SystemParametersInfo(NativeMethods.SpiGetMouse, 0, values, 0);
                return Task.FromResult(new TweakStatus
                {
                    IsEnabled = available && values[2] != 0,
                    IsAvailable = available,
                    ErrorMessage = available ? null : new Win32Exception(Marshal.GetLastWin32Error()).Message,
                });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new TweakStatus { IsAvailable = false, ErrorMessage = ex.Message });
            }
        }

        public async Task<bool> SetPointerPrecisionAsync(bool enabled)
        {
            var values = new int[3];
            if (!NativeMethods.SystemParametersInfo(NativeMethods.SpiGetMouse, 0, values, 0))
            {
                return false;
            }

            values = GetPointerPrecisionValues(enabled, values);
            var notified = NativeMethods.SystemParametersInfo(
                NativeMethods.SpiSetMouse,
                0,
                values,
                NativeMethods.SpifUpdateIniFile | NativeMethods.SpifSendChange);
            if (!notified)
            {
                this.logger.LogWarning(
                    "Windows did not apply pointer precision immediately: {Error}",
                    new Win32Exception(Marshal.GetLastWin32Error()).Message);
            }

            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"Control Panel\Mouse", writable: true);
                key.SetValue("MouseThreshold1", values[0].ToString(CultureInfo.InvariantCulture), RegistryValueKind.String);
                key.SetValue("MouseThreshold2", values[1].ToString(CultureInfo.InvariantCulture), RegistryValueKind.String);
                key.SetValue("MouseSpeed", values[2].ToString(CultureInfo.InvariantCulture), RegistryValueKind.String);
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Could not persist pointer precision settings.");
                return false;
            }

            var status = await this.GetPointerPrecisionStatusAsync();
            this.RaiseStatusChanged(nameof(SystemTweak.PointerPrecision), status);
            return status.IsAvailable && status.IsEnabled == enabled;
        }

        internal static int[] GetPointerPrecisionValues(bool enabled, IReadOnlyList<int> current)
        {
            if (current.Count < 3)
            {
                throw new ArgumentException("Three mouse parameters are required.", nameof(current));
            }

            if (!enabled)
            {
                // Windows may normalize acceleration back to enabled when non-zero
                // thresholds are submitted with acceleration disabled.
                return [0, 0, 0];
            }

            return
            [
                current[0] > 0 ? current[0] : 6,
                current[1] > 0 ? current[1] : 10,
                current[2] > 0 ? current[2] : 1,
            ];
        }

        public Task<TweakStatus> GetEthernetPowerSavingStatusAsync() =>
            Task.FromResult(GetRegistryTargetsStatus(FindEthernetTargets(EthernetPowerSavingValueNames), enabledValue: 0));

        public Task<bool> SetEthernetPowerSavingAsync(bool enabled) =>
            this.SetRegistryTargetsAsync(
                SystemTweak.EthernetPowerSaving,
                FindEthernetTargets(EthernetPowerSavingValueNames),
                enabled,
                enabledValue: 0,
                disabledValue: 1);

        public Task<TweakStatus> GetInterruptModerationStatusAsync() =>
            Task.FromResult(GetRegistryTargetsStatus(FindEthernetTargets(InterruptModerationValueNames), enabledValue: 0));

        public Task<bool> SetInterruptModerationAsync(bool enabled) =>
            this.SetRegistryTargetsAsync(
                SystemTweak.InterruptModeration,
                FindEthernetTargets(InterruptModerationValueNames),
                enabled,
                enabledValue: 0,
                disabledValue: 1);

        public Task<TweakStatus> GetGpuMsiModeStatusAsync() =>
            Task.FromResult(GetRegistryTargetsStatus(FindGpuMsiTargets(), enabledValue: 1));

        public Task<bool> SetGpuMsiModeAsync(bool enabled) =>
            this.SetRegistryTargetsAsync(
                SystemTweak.GpuMsiMode,
                FindGpuMsiTargets(),
                enabled,
                enabledValue: 1,
                disabledValue: 0,
                restoreOriginalWhenDisabled: false);

        public Task<TweakStatus> GetMemoryIntegrityStatusAsync()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\CI\State");
                var value = key == null ? null : ReadRegistryIntValue(key, "HVCIEnabled");
                return Task.FromResult(new TweakStatus
                {
                    IsEnabled = value == 1,
                    IsAvailable = value.HasValue,
                    ErrorMessage = value.HasValue ? null : "Memory Integrity state is not exposed by Windows.",
                });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new TweakStatus { IsAvailable = false, ErrorMessage = ex.Message });
            }
        }

        public Task<bool> OpenSettingsAsync(SystemTweak tweak)
        {
            var uri = tweak switch
            {
                SystemTweak.MemoryIntegrity => "windowsdefender://coreisolation",
                SystemTweak.Hags or SystemTweak.WindowedOptimizations => "ms-settings:display-advancedgraphics-default",
                _ => null,
            };

            if (uri == null)
            {
                return Task.FromResult(false);
            }

            try
            {
                Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Could not open Windows settings for {Tweak}", tweak);
                return Task.FromResult(false);
            }
        }

        public Task<TweakStatus> GetCoreParkingStatusAsync()
        {
            try
            {
                if (!TryReadAcPowerSetting(CoreParkingSettingGuid, out var acValue, out var error))
                {
                    return Task.FromResult(new TweakStatus { IsAvailable = false, ErrorMessage = error });
                }

                // ON = disable parking (keep all cores unparked, typically 100)
                return Task.FromResult(new TweakStatus
                {
                    IsEnabled = acValue >= 100,
                    IsAvailable = true,
                });
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error getting Core Parking status");
                return Task.FromResult(new TweakStatus { IsAvailable = false, ErrorMessage = ex.Message });
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

                var acValue = enabled ? 100u : 10u;
                if (!TryWriteAcPowerSetting(CoreParkingSettingGuid, acValue, out var error))
                {
                    this.logger.LogError("Failed setting Core Parking AC value: {Error}", error);
                    return false;
                }

                var status = await this.GetCoreParkingStatusAsync();
                this.TweakStatusChanged?.Invoke(this, new TweakStatusChangedEventArgs("CoreParking", status));

                var persisted = status.IsAvailable && status.IsEnabled == enabled;
                this.logger.LogInformation("Core Parking {Status}; persisted={Persisted}", enabled ? "enabled" : "disabled", persisted);
                return persisted;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error setting Core Parking to {Enabled}", enabled);
                return false;
            }
        }

        public Task<TweakStatus> GetCStatesStatusAsync()
        {
            try
            {
                if (!TryReadAcPowerSetting(ProcessorIdleDisableSettingGuid, out var acValue, out var error))
                {
                    return Task.FromResult(new TweakStatus { IsAvailable = false, ErrorMessage = error });
                }

                // ON = enable C-States (IDLEDISABLE=0), OFF = disable C-States (IDLEDISABLE=1)
                return Task.FromResult(new TweakStatus
                {
                    IsEnabled = acValue == 0,
                    IsAvailable = true,
                });
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error getting C-States status");
                return Task.FromResult(new TweakStatus { IsAvailable = false, ErrorMessage = ex.Message });
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

                var value = enabled ? 0u : 1u;
                if (!TryWriteAcPowerSetting(ProcessorIdleDisableSettingGuid, value, out var error))
                {
                    this.logger.LogError("Failed setting C-States AC value: {Error}", error);
                    return false;
                }

                var status = await this.GetCStatesStatusAsync();
                this.TweakStatusChanged?.Invoke(this, new TweakStatusChangedEventArgs("CStates", status));

                var persisted = status.IsAvailable && status.IsEnabled == enabled;
                this.logger.LogInformation("C-States {Status}; persisted={Persisted}", enabled ? "enabled" : "disabled", persisted);
                return persisted;
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

                var persisted = status.IsAvailable && status.IsEnabled == enabled;
                this.logger.LogInformation("SysMain service {Status}; persisted={Persisted}", enabled ? "started" : "stopped", persisted);
                return persisted;
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

                var enablePrefetcher = ReadRegistryIntValue(key, "EnablePrefetcher");
                if (enablePrefetcher is < 0 or > 3 || !enablePrefetcher.HasValue)
                {
                    return Task.FromResult(new TweakStatus { IsAvailable = false, ErrorMessage = "Prefetch registry value is missing or invalid" });
                }

                return Task.FromResult(new TweakStatus
                {
                    IsEnabled = enablePrefetcher.Value != 0,
                    IsAvailable = true,
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

                var persisted = status.IsAvailable && status.IsEnabled == enabled;
                this.logger.LogInformation("Prefetch {Status}; persisted={Persisted}", enabled ? "enabled" : "disabled", persisted);
                return persisted;
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
                var powerThrottlingOff = key == null ? null : ReadRegistryIntValue(key, "PowerThrottlingOff");
                // ON = disable throttling (PowerThrottlingOff=1)
                return Task.FromResult(new TweakStatus
                {
                    IsEnabled = powerThrottlingOff.GetValueOrDefault(0) == 1,
                    IsAvailable = true,
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

                var persisted = status.IsAvailable && status.IsEnabled == enabled;
                this.logger.LogInformation("Power Throttling {Status}; persisted={Persisted}", enabled ? "enabled" : "disabled", persisted);
                return persisted;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error setting Power Throttling to {Enabled}", enabled);
                return false;
            }
        }

        public Task<TweakStatus> GetHighSchedulingCategoryStatusAsync()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(GamesSchedulingKeyPath);
                if (key == null)
                {
                    return Task.FromResult(new TweakStatus { IsAvailable = false, ErrorMessage = "MMCSS Games registry key not found" });
                }

                var category = key.GetValue(SchedulingCategoryValueName) as string;
                if (string.IsNullOrWhiteSpace(category))
                {
                    return Task.FromResult(new TweakStatus { IsAvailable = false, ErrorMessage = "MMCSS Games scheduling category is missing" });
                }

                return Task.FromResult(new TweakStatus
                {
                    IsEnabled = string.Equals(category, HighSchedulingCategoryEnabledValue, StringComparison.OrdinalIgnoreCase),
                    IsAvailable = true,
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

                using var key = Registry.LocalMachine.OpenSubKey(GamesSchedulingKeyPath, true);
                if (key == null)
                {
                    this.logger.LogError("MMCSS Games registry key not found");
                    return false;
                }

                key.SetValue(SchedulingCategoryValueName, GetHighSchedulingCategoryRegistryValue(enabled), RegistryValueKind.String);

                var status = await this.GetHighSchedulingCategoryStatusAsync();
                this.TweakStatusChanged?.Invoke(this, new TweakStatusChangedEventArgs("HighSchedulingCategory", status));

                var persisted = status.IsAvailable && status.IsEnabled == enabled;
                this.logger.LogInformation("High Scheduling Category {Status}; persisted={Persisted}", enabled ? "enabled" : "disabled", persisted);
                return persisted;
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

        internal static string GetHighSchedulingCategoryRegistryValue(bool enabled) =>
            enabled ? HighSchedulingCategoryEnabledValue : HighSchedulingCategoryDisabledValue;

        private void RaiseStatusChanged(string tweakName, TweakStatus status) =>
            this.TweakStatusChanged?.Invoke(this, new TweakStatusChangedEventArgs(tweakName, status));

        private static List<RegistryTarget> FindEthernetTargets(IEnumerable<string> valueNames)
        {
            var targets = new List<RegistryTarget>();
            using var adapters = Registry.LocalMachine.OpenSubKey(NetworkAdapterClassPath);
            if (adapters == null)
            {
                return targets;
            }

            foreach (var subKeyName in adapters.GetSubKeyNames())
            {
                var path = $@"{NetworkAdapterClassPath}\{subKeyName}";
                using var adapter = Registry.LocalMachine.OpenSubKey(path);
                var characteristics = adapter == null ? null : ReadRegistryIntValue(adapter, "Characteristics");
                if (adapter == null ||
                    ReadRegistryIntValue(adapter, "*IfType") != 6 ||
                    !characteristics.HasValue ||
                    (characteristics.Value & 0x4) == 0)
                {
                    continue;
                }

                foreach (var valueName in valueNames)
                {
                    AddRegistryTarget(targets, adapter, path, valueName);
                }
            }

            return targets;
        }

        private static List<RegistryTarget> FindGpuMsiTargets()
        {
            var targets = new List<RegistryTarget>();
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT PNPDeviceID FROM Win32_VideoController");
                using var results = searcher.Get();
                foreach (ManagementObject device in results)
                {
                    using (device)
                    {
                        var pnpDeviceId = device["PNPDeviceID"]?.ToString();
                        if (string.IsNullOrWhiteSpace(pnpDeviceId))
                        {
                            continue;
                        }

                        var path = $@"SYSTEM\CurrentControlSet\Enum\{pnpDeviceId}\{MsiPropertiesSuffix}";
                        using var key = Registry.LocalMachine.OpenSubKey(path);
                        if (key != null)
                        {
                            AddRegistryTarget(targets, key, path, "MSISupported");
                        }
                    }
                }
            }
            catch (ManagementException)
            {
                // No supported display adapter information is available.
            }

            return targets;
        }

        private static void AddRegistryTarget(
            ICollection<RegistryTarget> targets,
            RegistryKey key,
            string path,
            string valueName)
        {
            var value = ReadRegistryIntValue(key, valueName);
            if (!value.HasValue)
            {
                return;
            }

            var kind = key.GetValueKind(valueName);
            if (kind is RegistryValueKind.DWord or RegistryValueKind.QWord or RegistryValueKind.String)
            {
                targets.Add(new RegistryTarget(path, valueName, value.Value, kind));
            }
        }

        private static TweakStatus GetRegistryTargetsStatus(IReadOnlyCollection<RegistryTarget> targets, int enabledValue) => new()
        {
            IsAvailable = targets.Count > 0,
            IsEnabled = targets.Count > 0 && targets.All(target => target.Value == enabledValue),
            ErrorMessage = targets.Count == 0 ? "No supported driver property was found." : null,
        };

        private async Task<bool> SetRegistryTargetsAsync(
            SystemTweak tweak,
            IReadOnlyCollection<RegistryTarget> targets,
            bool enabled,
            int enabledValue,
            int disabledValue,
            bool restoreOriginalWhenDisabled = true)
        {
            if (!this.elevationService.IsRunningAsAdministrator() || targets.Count == 0)
            {
                return false;
            }

            var backups = this.LoadRegistryBackups();
            var changedBackups = false;
            var changedTargets = new List<RegistryTarget>();
            try
            {
                foreach (var target in targets)
                {
                    var backupKey = target.BackupKey;
                    if (!backups.ContainsKey(backupKey))
                    {
                        backups[backupKey] = new RegistryValueBackup(target.Value, target.Kind);
                        changedBackups = true;
                    }
                }

                if (changedBackups)
                {
                    this.SaveRegistryBackups(backups);
                }

                foreach (var target in targets)
                {
                    var backupKey = target.BackupKey;
                    var desiredValue = enabled
                        ? enabledValue
                        : restoreOriginalWhenDisabled && backups.TryGetValue(backupKey, out var backup)
                            ? backup.Value
                            : disabledValue;
                    using var key = Registry.LocalMachine.OpenSubKey(target.Path, writable: true);
                    if (key == null)
                    {
                        return false;
                    }

                    key.SetValue(
                        target.ValueName,
                        target.Kind == RegistryValueKind.String
                            ? desiredValue.ToString(CultureInfo.InvariantCulture)
                            : desiredValue,
                        target.Kind);
                    changedTargets.Add(target);
                }

                var status = tweak switch
                {
                    SystemTweak.EthernetPowerSaving => await this.GetEthernetPowerSavingStatusAsync(),
                    SystemTweak.InterruptModeration => await this.GetInterruptModerationStatusAsync(),
                    SystemTweak.GpuMsiMode => await this.GetGpuMsiModeStatusAsync(),
                    _ => new TweakStatus { IsAvailable = false },
                };
                this.RaiseStatusChanged(tweak.ToString(), status);
                return status.IsAvailable && status.IsEnabled == enabled;
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Could not update driver tweak {Tweak}", tweak);
                foreach (var target in changedTargets)
                {
                    try
                    {
                        using var key = Registry.LocalMachine.OpenSubKey(target.Path, writable: true);
                        key?.SetValue(
                            target.ValueName,
                            target.Kind == RegistryValueKind.String
                                ? target.Value.ToString(CultureInfo.InvariantCulture)
                                : target.Value,
                            target.Kind);
                    }
                    catch (Exception rollbackException)
                    {
                        this.logger.LogWarning(
                            rollbackException,
                            "Could not roll back {Path}\\{ValueName}",
                            target.Path,
                            target.ValueName);
                    }
                }

                return false;
            }
        }

        private Dictionary<string, RegistryValueBackup> LoadRegistryBackups()
        {
            try
            {
                if (!File.Exists(StoragePaths.DeviceTweakBackupsFilePath))
                {
                    return new Dictionary<string, RegistryValueBackup>(StringComparer.OrdinalIgnoreCase);
                }

                var stored = JsonSerializer.Deserialize<Dictionary<string, RegistryValueBackup>>(
                    File.ReadAllText(StoragePaths.DeviceTweakBackupsFilePath));
                return stored == null
                    ? new Dictionary<string, RegistryValueBackup>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, RegistryValueBackup>(stored, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Could not load device tweak backups");
                return new Dictionary<string, RegistryValueBackup>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private void SaveRegistryBackups(Dictionary<string, RegistryValueBackup> backups)
        {
            StoragePaths.EnsureAppDataDirectories();
            File.WriteAllText(
                StoragePaths.DeviceTweakBackupsFilePath,
                JsonSerializer.Serialize(backups, new JsonSerializerOptions { WriteIndented = true }));
        }

        private static bool TryReadAcPowerSetting(Guid settingGuid, out uint value, out string? error)
            => TryReadAcPowerSetting(ProcessorSettingsSubgroupGuid, settingGuid, out value, out error);

        private static bool TryReadAcPowerSetting(
            Guid subgroupGuid,
            Guid settingGuid,
            out uint value,
            out string? error)
        {
            value = 0;
            if (!TryGetActivePowerScheme(out var schemeGuid, out error))
            {
                return false;
            }

            var result = NativeMethods.PowerReadAcValueIndex(
                IntPtr.Zero,
                ref schemeGuid,
                ref subgroupGuid,
                ref settingGuid,
                out value);
            if (result != 0)
            {
                error = new Win32Exception((int)result).Message;
                return false;
            }

            error = null;
            return true;
        }

        private static bool TryWriteAcPowerSetting(Guid settingGuid, uint value, out string? error)
            => TryWriteAcPowerSetting(ProcessorSettingsSubgroupGuid, settingGuid, value, out error);

        private static bool TryWriteAcPowerSetting(
            Guid subgroupGuid,
            Guid settingGuid,
            uint value,
            out string? error)
        {
            if (!TryGetActivePowerScheme(out var schemeGuid, out error))
            {
                return false;
            }

            var writeResult = NativeMethods.PowerWriteAcValueIndex(
                IntPtr.Zero,
                ref schemeGuid,
                ref subgroupGuid,
                ref settingGuid,
                value);
            if (writeResult != 0)
            {
                error = new Win32Exception((int)writeResult).Message;
                return false;
            }

            var activateResult = NativeMethods.PowerSetActiveScheme(IntPtr.Zero, ref schemeGuid);
            if (activateResult != 0)
            {
                error = new Win32Exception((int)activateResult).Message;
                return false;
            }

            error = null;
            return true;
        }

        private static bool TryGetActivePowerScheme(out Guid schemeGuid, out string? error)
        {
            schemeGuid = Guid.Empty;
            var result = NativeMethods.PowerGetActiveScheme(IntPtr.Zero, out var schemePointer);
            if (result != 0 || schemePointer == IntPtr.Zero)
            {
                error = new Win32Exception((int)result).Message;
                return false;
            }

            try
            {
                schemeGuid = Marshal.PtrToStructure<Guid>(schemePointer);
                error = null;
                return true;
            }
            finally
            {
                _ = NativeMethods.LocalFree(schemePointer);
            }
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
            return string.Equals(fullPath, Path.GetFullPath(ScExecutablePath), StringComparison.OrdinalIgnoreCase)
                && File.Exists(fullPath);
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

        private sealed record RegistryTarget(
            string Path,
            string ValueName,
            int Value,
            RegistryValueKind Kind)
        {
            public string BackupKey => $"{this.Path}|{this.ValueName}";
        }

        private sealed record RegistryValueBackup(int Value, RegistryValueKind Kind);

        public Task<TweakStatus> GetMenuShowDelayStatusAsync()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
                if (key == null)
                {
                    return Task.FromResult(new TweakStatus { IsAvailable = false, ErrorMessage = "Desktop registry key not found" });
                }

                var rawDelay = key.GetValue("MenuShowDelay")?.ToString();
                var parsedDelay = 400;
                if (rawDelay != null && (!int.TryParse(rawDelay, out parsedDelay) || parsedDelay < 0))
                {
                    return Task.FromResult(new TweakStatus { IsAvailable = false, ErrorMessage = "MenuShowDelay registry value is invalid" });
                }

                return Task.FromResult(new TweakStatus
                {
                    IsEnabled = parsedDelay > 0,
                    IsAvailable = true,
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

                var persisted = status.IsAvailable && status.IsEnabled == enabled;
                this.logger.LogInformation("Menu Show Delay {Status}; persisted={Persisted}", enabled ? "enabled" : "disabled", persisted);
                return persisted;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error setting Menu Show Delay to {Enabled}", enabled);
                return false;
            }
        }

        private static class NativeMethods
        {
            internal const uint SpiGetMouse = 0x0003;
            internal const uint SpiSetMouse = 0x0004;
            internal const uint SpifUpdateIniFile = 0x0001;
            internal const uint SpifSendChange = 0x0002;

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool SystemParametersInfo(uint action, uint parameter, [In, Out] int[] values, uint flags);

            [DllImport("PowrProf.dll", EntryPoint = "PowerGetActiveScheme")]
            internal static extern uint PowerGetActiveScheme(IntPtr userRootPowerKey, out IntPtr activePolicyGuid);

            [DllImport("PowrProf.dll", EntryPoint = "PowerReadACValueIndex")]
            internal static extern uint PowerReadAcValueIndex(
                IntPtr rootPowerKey,
                ref Guid schemeGuid,
                ref Guid subgroupGuid,
                ref Guid settingGuid,
                out uint acValueIndex);

            [DllImport("PowrProf.dll", EntryPoint = "PowerWriteACValueIndex")]
            internal static extern uint PowerWriteAcValueIndex(
                IntPtr rootPowerKey,
                ref Guid schemeGuid,
                ref Guid subgroupGuid,
                ref Guid settingGuid,
                uint acValueIndex);

            [DllImport("PowrProf.dll", EntryPoint = "PowerSetActiveScheme")]
            internal static extern uint PowerSetActiveScheme(IntPtr userRootPowerKey, ref Guid schemeGuid);

            [DllImport("kernel32.dll")]
            internal static extern IntPtr LocalFree(IntPtr memory);
        }
    }
}

