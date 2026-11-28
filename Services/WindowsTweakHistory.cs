namespace ThreadPilot.Services;

using System.IO;
using System.Security.Cryptography;
using System.ServiceProcess;
using System.Text.Json;
using Microsoft.Win32;
using ThreadPilot.Services.Abstractions;

internal static class WindowsTweakHistory
{
    private static readonly SystemProcessRunner Runner = new();
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(25);
    private static readonly Guid Balanced = new("381b4222-f694-41f0-9685-ff5bb260df2e");
    private const string PowerSchemes = @"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes";
    private const string SysMainPath = @"SYSTEM\CurrentControlSet\Services\SysMain";

    internal static RegistryKey Open(string section) =>
        Registry.LocalMachine.CreateSubKey($@"SOFTWARE\ThreadPilot\{section}\{RegistryTweakHistory.CurrentUserSid}");

    internal static void RecordPowerSetting(Guid scheme, Guid subgroup, Guid setting, uint current, uint desired)
    {
        if (current == desired)
        {
            return;
        }

        using var history = Open("PowerSettings");
        var id = $"{scheme:D}/{subgroup:D}/{setting:D}";
        var previous = Read<PowerSettingChange>(history, id);
        var original = previous?.Written == current || previous?.PreviousWritten == current ? previous.Original : current;
        var entry = new PowerSettingChange(original, desired, original == current ? null : current);
        Store(history, id, entry);
    }

    internal static void ConfirmPowerSetting(Guid scheme, Guid subgroup, Guid setting, uint written)
    {
        using var history = Open("PowerSettings");
        var id = $"{scheme:D}/{subgroup:D}/{setting:D}";
        var change = Read<PowerSettingChange>(history, id);
        if (change?.Written == written && change.PreviousWritten.HasValue)
        {
            Store(history, id, change with { PreviousWritten = null });
        }
    }

    internal static async Task<bool> ImportPlanAsync(string file, IProcessRunner runner)
    {
        var id = Guid.NewGuid().ToString("D");
        using var history = Open("ImportedPlans");
        Store(history, id, new ImportedPlan(null));
        var import = await PowerCfg(runner, "/import", file, id).ConfigureAwait(false);
        if (import.ExitCode != 0)
        {
            if (!PowerPlanExists(id))
            {
                history.DeleteValue(id);
            }

            return false;
        }

        var fingerprint = await FingerprintAsync(id, runner).ConfigureAwait(false);
        Store(history, id, new ImportedPlan(fingerprint));
        return true;
    }

    internal static async Task SetSysMainAsync(bool enabled)
    {
        var current = ReadSysMain();
        var desired = new SysMainState(enabled ? ServiceStartMode.Automatic : ServiceStartMode.Disabled, false, enabled);
        if (current == desired)
        {
            return;
        }

        using var history = Open("ServiceSettings");
        var previous = Read<SysMainChange>(history, "SysMain");
        var original = previous?.Written == current || previous?.PreviousWritten == current ? previous.Original : current;
        Store(history, "SysMain", new SysMainChange(original, desired, current));
        await WriteSysMainAsync(desired).ConfigureAwait(false);
        Store(history, "SysMain", new SysMainChange(original, desired, null));
    }

    internal static void Restore(List<string> residuals)
    {
        RestoreSection("PowerSettings", (history, id) =>
        {
            var parts = id.Split('/');
            if (parts.Length != 3 || !Guid.TryParse(parts[0], out var scheme)
                || !Guid.TryParse(parts[1], out var subgroup) || !Guid.TryParse(parts[2], out var setting))
            {
                throw new InvalidDataException("Invalid recorded power setting.");
            }

            if (!PowerPlanExists(parts[0]))
            {
                return true;
            }

            var entry = Read<PowerSettingChange>(history, id) ?? throw new InvalidDataException(id);
            return SystemTweaksService.RestorePowerSetting(scheme, subgroup, setting, entry.Original, entry.Written, entry.PreviousWritten);
        });
        RestoreSection("ServiceSettings", (history, id) =>
        {
            if (id != "SysMain")
            {
                throw new InvalidDataException("Unrecognized service in recovery history.");
            }

            var entry = Read<SysMainChange>(history, id) ?? throw new InvalidDataException(id);
            var current = ReadSysMain();
            if (current == entry.Original)
            {
                return true;
            }

            if (current != entry.Written && current != entry.PreviousWritten)
            {
                if (entry.PreviousWritten.HasValue)
                {
                    throw new InvalidOperationException("SysMain has an unverified startup or running state after an incomplete write.");
                }

                return false;
            }

            WriteSysMainAsync(entry.Original).GetAwaiter().GetResult();
            return true;
        });
        RestoreSection("ImportedPlans", (history, id) =>
        {
            if (!Guid.TryParseExact(id, "D", out var scheme))
            {
                throw new InvalidDataException("Invalid recorded power plan identifier.");
            }

            if (!PowerPlanExists(id))
            {
                return true;
            }

            var entry = Read<ImportedPlan>(history, id) ?? throw new InvalidDataException(id);
            if (entry.Fingerprint == null)
            {
                throw new InvalidDataException("The imported plan fingerprint is missing.");
            }

            if (FingerprintAsync(id, Runner).GetAwaiter().GetResult() != entry.Fingerprint)
            {
                return false;
            }

            if (!SystemTweaksService.TryGetActivePowerScheme(out var active, out var error))
            {
                throw new InvalidOperationException(error);
            }

            if (active == scheme)
            {
                var fallback = Balanced.ToString("D");
                if (!PowerPlanExists(fallback)
                    || PowerCfg(Runner, "/setactive", fallback).GetAwaiter().GetResult().ExitCode != 0
                    || !SystemTweaksService.TryGetActivePowerScheme(out active, out _) || active != Balanced)
                {
                    throw new InvalidOperationException("Could not activate the Balanced plan before removal.");
                }
            }

            if (PowerCfg(Runner, "/delete", id).GetAwaiter().GetResult().ExitCode != 0 || PowerPlanExists(id))
            {
                throw new InvalidOperationException("Windows did not remove the imported power plan.");
            }

            return true;
        });

        void RestoreSection(string section, Func<RegistryKey, string, bool> restore)
        {
            var path = $@"SOFTWARE\ThreadPilot\{section}\{RegistryTweakHistory.CurrentUserSid}";
            using (var history = Registry.LocalMachine.OpenSubKey(path, writable: true))
            {
                if (history == null)
                {
                    return;
                }

                foreach (var id in history.GetValueNames())
                {
                    try
                    {
                        if (!restore(history, id))
                        {
                            residuals.Add($"{id}: a later change was preserved.");
                        }

                        history.DeleteValue(id);
                    }
                    catch (Exception ex)
                    {
                        residuals.Add($"{id}: restore failed ({ex.Message}). Recovery entry retained.");
                    }
                }

                if (history.ValueCount != 0)
                {
                    return;
                }
            }

            Registry.LocalMachine.DeleteSubKey(path, throwOnMissingSubKey: false);
            var sectionPath = $@"SOFTWARE\ThreadPilot\{section}";
            using var parent = Registry.LocalMachine.OpenSubKey(sectionPath);
            if (parent is { SubKeyCount: 0, ValueCount: 0 })
            {
                Registry.LocalMachine.DeleteSubKey(sectionPath, throwOnMissingSubKey: false);
            }
        }
    }

    private static SysMainState ReadSysMain()
    {
        using var controller = new ServiceController("SysMain");
        if (controller.Status is not (ServiceControllerStatus.Stopped or ServiceControllerStatus.Running)
            || controller.StartType is not (ServiceStartMode.Automatic or ServiceStartMode.Manual or ServiceStartMode.Disabled))
        {
            throw new InvalidOperationException("SysMain is in a state that cannot be safely restored.");
        }

        using var key = Registry.LocalMachine.OpenSubKey(SysMainPath);
        return new(controller.StartType, controller.StartType == ServiceStartMode.Automatic && Equals(key?.GetValue("DelayedAutoStart"), 1),
            controller.Status == ServiceControllerStatus.Running);
    }

    private static async Task WriteSysMainAsync(SysMainState state)
    {
        var start = state.Start switch
        {
            ServiceStartMode.Automatic => state.Delayed ? "delayed-auto" : "auto",
            ServiceStartMode.Manual => "demand",
            ServiceStartMode.Disabled => "disabled",
            _ => throw new InvalidOperationException(),
        };
        using var controller = new ServiceController("SysMain");
        // A disabled service can be running if its startup type was changed after it started.
        if (state.Start == ServiceStartMode.Disabled && state.Running && controller.Status != ServiceControllerStatus.Running)
        {
            await Configure("demand").ConfigureAwait(false);
            controller.Start();
            controller.WaitForStatus(ServiceControllerStatus.Running, CommandTimeout);
        }

        await Configure(start).ConfigureAwait(false);
        controller.Refresh();
        if (state.Running && controller.Status == ServiceControllerStatus.Stopped)
        {
            controller.Start();
            controller.WaitForStatus(ServiceControllerStatus.Running, CommandTimeout);
        }
        else if (!state.Running && controller.Status == ServiceControllerStatus.Running)
        {
            controller.Stop();
            controller.WaitForStatus(ServiceControllerStatus.Stopped, CommandTimeout);
        }

        if (ReadSysMain() != state)
        {
            throw new InvalidOperationException("SysMain startup configuration or running status could not be verified.");
        }

        static async Task Configure(string start)
        {
            var result = await Runner.RunAsync(Path.Combine(Environment.SystemDirectory, "sc.exe"),
                ["config", "SysMain", "start=", start], CommandTimeout).ConfigureAwait(false);
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException("Windows did not change the SysMain startup configuration.");
            }
        }
    }

    private static bool PowerPlanExists(string id)
    {
        using var key = Registry.LocalMachine.OpenSubKey(PowerSchemes + "\\" + id);
        return key != null;
    }

    private static async Task<string> FingerprintAsync(string id, IProcessRunner runner)
    {
        var result = await PowerCfg(runner, "/qh", id).ConfigureAwait(false);
        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            throw new InvalidOperationException("Windows could not read back the imported power plan.");
        }

        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(result.StandardOutput)));
    }

    private static Task<ProcessRunResult> PowerCfg(IProcessRunner runner, params string[] arguments) =>
        runner.RunAsync(Path.Combine(Environment.SystemDirectory, "powercfg.exe"), arguments, CommandTimeout);

    private static T? Read<T>(RegistryKey key, string name) where T : class =>
        key.GetValue(name) is string json ? JsonSerializer.Deserialize<T>(json) : null;

    private static void Store<T>(RegistryKey key, string name, T entry)
    {
        key.SetValue(name, JsonSerializer.Serialize(entry));
        key.Flush();
    }

    private sealed record PowerSettingChange(uint Original, uint Written, uint? PreviousWritten);

    private sealed record ImportedPlan(string? Fingerprint);

    private sealed record SysMainChange(SysMainState Original, SysMainState Written, SysMainState? PreviousWritten);

    private readonly record struct SysMainState(ServiceStartMode Start, bool Delayed, bool Running);
}
