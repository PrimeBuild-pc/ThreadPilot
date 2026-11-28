namespace ThreadPilot.Services;

using System.Globalization;
using System.Text.Json;
using Microsoft.Win32;

internal static class RegistryTweakHistory
{
    private const string HistoryPath = @"SOFTWARE\ThreadPilot\TweakHistory";
    private static readonly object Gate = new();

    internal static void Write(RegistryKey root, string path, string name, object? value, RegistryValueKind kind)
    {
        lock (Gate)
        {
            var written = RegistryValue.From(value, kind);
            var current = Read(root, path, name);
            if (current == written)
            {
                return;
            }

            using var history = Registry.LocalMachine.CreateSubKey(HistoryPath + "\\" + CurrentUserSid);
            var id = $"{root.Name}|{path}|{name}";
            var previous = ReadChange(history, id);
            // A later external change becomes the baseline if the user explicitly sets the tweak again.
            var ownsCurrent = previous != null && (previous.Written == current || previous.PreviousWritten == current);
            var original = ownsCurrent ? previous!.Original : current;
            history.SetValue(id, JsonSerializer.Serialize(new RegistryChange(root.Name, path, name, original, written,
                ownsCurrent ? current : null)));
            history.Flush();

            Apply(root, path, name, written);
            if (Read(root, path, name) != written)
            {
                throw new InvalidOperationException($"Could not verify {path}\\{name}.");
            }
            if (ownsCurrent)
            {
                history.SetValue(id, JsonSerializer.Serialize(new RegistryChange(root.Name, path, name, original, written, null)));
            }
        }
    }

    internal static RegistryValue? GetTrackedOriginal(RegistryKey root, string path, string name)
    {
        lock (Gate)
        {
            using var history = Registry.LocalMachine.OpenSubKey(HistoryPath + "\\" + CurrentUserSid);
            var change = history == null ? null : ReadChange(history, $"{root.Name}|{path}|{name}");
            return change != null && Read(root, path, name) == change.Written ? change.Original : null;
        }
    }

    internal static void Restore(List<string> residuals, List<string> failures)
    {
        lock (Gate)
        {
            var userPath = HistoryPath + "\\" + CurrentUserSid;
            using (var history = Registry.LocalMachine.OpenSubKey(userPath, writable: true))
            {
                if (history == null)
                {
                    return;
                }

                foreach (var id in history.GetValueNames())
                {
                    try
                    {
                        var change = ReadChange(history, id) ?? throw new InvalidOperationException();
                        var root = change.Root switch
                        {
                            "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
                            "HKEY_CURRENT_USER" => Registry.CurrentUser,
                            _ => throw new InvalidOperationException(),
                        };
                        var current = Read(root, change.Path, change.Name);
                        if (ShouldRestore(current, change.Original, change.Written)
                            || (current != change.Original && change.PreviousWritten == current))
                        {
                            Apply(root, change.Path, change.Name, change.Original);
                            if (Read(root, change.Path, change.Name) != change.Original)
                            {
                                throw new InvalidOperationException();
                            }
                        }
                        else if (current != change.Original)
                        {
                            residuals.Add($"{id}: a later change was preserved.");
                        }

                        history.DeleteValue(id);
                    }
                    catch (Exception ex)
                    {
                        failures.Add($"{id}: restore failed ({ex.Message}). Recovery entry retained.");
                    }
                }

                if (history.ValueCount != 0)
                {
                    return;
                }
            }

            Registry.LocalMachine.DeleteSubKey(userPath, throwOnMissingSubKey: false);
            using var parent = Registry.LocalMachine.OpenSubKey(HistoryPath);
            if (parent is { SubKeyCount: 0, ValueCount: 0 })
            {
                Registry.LocalMachine.DeleteSubKey(HistoryPath, throwOnMissingSubKey: false);
            }
        }
    }

    internal static bool ShouldRestore(RegistryValue current, RegistryValue original, RegistryValue written) =>
        current != original && current == written;

    internal static string CurrentUserSid
    {
        get
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            return identity.User?.Value ?? throw new InvalidOperationException("Windows account SID is unavailable.");
        }
    }

    private static RegistryChange? ReadChange(RegistryKey history, string name) =>
        history.GetValue(name) is string json ? JsonSerializer.Deserialize<RegistryChange>(json) : null;

    private static RegistryValue Read(RegistryKey root, string path, string name)
    {
        using var key = root.OpenSubKey(path);
        var value = key?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return value == null ? default : RegistryValue.From(value, key!.GetValueKind(name));
    }

    private static void Apply(RegistryKey root, string path, string name, RegistryValue value)
    {
        if (value.Text == null)
        {
            using var existing = root.OpenSubKey(path, writable: true);
            existing?.DeleteValue(name, throwOnMissingValue: false);
            return;
        }

        using var key = root.CreateSubKey(path);
        object data = value.Kind switch
        {
            RegistryValueKind.DWord => int.Parse(value.Text, CultureInfo.InvariantCulture),
            RegistryValueKind.QWord => long.Parse(value.Text, CultureInfo.InvariantCulture),
            RegistryValueKind.String or RegistryValueKind.ExpandString => value.Text,
            _ => throw new InvalidOperationException("Unsupported registry value type."),
        };
        key.SetValue(name, data, value.Kind);
    }

    internal readonly record struct RegistryValue(string? Text, RegistryValueKind Kind)
    {
        internal static RegistryValue From(object? value, RegistryValueKind kind) => value switch
        {
            null => default,
            int dword when kind == RegistryValueKind.DWord => new(dword.ToString(CultureInfo.InvariantCulture), kind),
            long qword when kind == RegistryValueKind.QWord => new(qword.ToString(CultureInfo.InvariantCulture), kind),
            string text when kind is RegistryValueKind.String or RegistryValueKind.ExpandString => new(text, kind),
            _ => throw new InvalidOperationException("Unsupported registry value type."),
        };
    }

    private sealed record RegistryChange(string Root, string Path, string Name, RegistryValue Original, RegistryValue Written, RegistryValue? PreviousWritten);
}
