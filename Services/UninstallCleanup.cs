namespace ThreadPilot.Services;

using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Windows;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

internal static partial class UninstallCleanup
{
    internal const string Argument = "--prepare-uninstall";
    private const string InstanceMutex = @"Global\ThreadPilot_SingleInstance";
    private const string PackageFamily = "LorenzoMassafra.ThreadPilot_2j208vxf88tt6";
    private const string InstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{E8F7A3B2-5C4D-4E6F-8A9B-1C2D3E4F5A6B}_is1";
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    internal static EventWaitHandle RegisterShutdown(Action request)
    {
        var security = new EventWaitHandleSecurity();
        using var identity = WindowsIdentity.GetCurrent();
        var administrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var elevated = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        var allowed = elevated ? administrators : identity.User!;
        security.SetOwner(allowed);
        security.AddAccessRule(new EventWaitHandleAccessRule(allowed, EventWaitHandleRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new EventWaitHandleAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), EventWaitHandleRights.FullControl, AccessControlType.Allow));
        var signal = EventWaitHandleAcl.Create(false, EventResetMode.AutoReset, ShutdownEventName(Environment.ProcessId), out _, security);
        ThreadPool.RegisterWaitForSingleObject(signal, (_, _) => request(), null, Timeout.Infinite, executeOnlyOnce: true);
        return signal;
    }

    internal static bool TryChooseOptions(string title, out bool purge, out bool restore)
    {
        var data = new System.Windows.Controls.CheckBox
        {
            Content = "Remove ThreadPilot settings, profiles, rules, logs and temporary files",
            IsChecked = true,
            Margin = new Thickness(0, 0, 0, 12),
        };
        var windows = new System.Windows.Controls.CheckBox
        {
            Content = "Restore Windows changes tracked by ThreadPilot",
            IsChecked = true,
            Margin = new Thickness(0, 0, 0, 18),
        };
        var buttons = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
        };
        var proceed = new System.Windows.Controls.Button
        {
            Content = "Continue",
            IsDefault = true,
            MinWidth = 94,
            Padding = new Thickness(12, 5, 12, 5),
            Margin = new Thickness(0, 0, 8, 0),
        };
        buttons.Children.Add(proceed);
        buttons.Children.Add(new System.Windows.Controls.Button
        {
            Content = "Cancel",
            IsCancel = true,
            MinWidth = 94,
            Padding = new Thickness(12, 5, 12, 5),
        });
        var panel = new System.Windows.Controls.StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(data);
        panel.Children.Add(windows);
        panel.Children.Add(buttons);
        var window = new Window
        {
            Title = title,
            Content = panel,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
        };
        proceed.Click += (_, _) => window.DialogResult = true;
        if (System.Windows.Application.Current.MainWindow is { IsVisible: true } owner)
        {
            window.Owner = owner;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        var confirmed = window.ShowDialog() == true;
        purge = data.IsChecked == true;
        restore = windows.IsChecked == true;
        return confirmed;
    }

    internal static int Run(string[] args, Func<Task>? recoverGameBoost = null, Action? closeServices = null, bool fromStoreWindow = false)
    {
        var silent = args.Contains("--silent");
        var purge = args.Contains("--purge-data");
        var restore = !args.Contains("--keep-windows-changes");
        if (!silent && !fromStoreWindow && !TryChooseOptions("Uninstall ThreadPilot", out purge, out restore))
        {
            return 2;
        }

        var residuals = new List<string>();
        string? legacyNote = null;
        try
        {
            if (!fromStoreWindow)
            {
                StopMatchingInstance();
            }
        }
        catch (Exception ex)
        {
            Report([ex.Message], silent, fromStoreWindow);
            return 2;
        }

        try
        {
            var executable = Environment.ProcessPath ?? throw new InvalidOperationException("ThreadPilot executable path is unavailable.");
            using var install = Registry.LocalMachine.OpenSubKey(InstallKey);
            var installedPath = install?.GetValue("InstallLocation") is string installDir
                ? Path.Combine(installDir, "ThreadPilot.exe")
                : null;
            var sameUser = IsSessionUser();
            var shared = StoreDistribution.IsStoreBuild
                ? installedPath != null
                : IsStoreInstalled() || (installedPath != null && !SamePath(installedPath, executable));

            RemoveStartup(executable, sameUser, residuals);
            if (!sameUser)
            {
                residuals.Add("User data and Windows recovery were retained because the elevated account differs from the signed-in account, or no interactive account is available.");
            }
            else if (shared)
            {
                residuals.Add("Shared data and Windows settings were retained because another ThreadPilot distribution is installed.");
            }
            else
            {
                var journal = Path.Combine(StoragePaths.AppDataRoot, "game_boost_journal.json");
                if (restore)
                {
                    if (recoverGameBoost != null && File.Exists(journal) && !HasReparsePoint(journal))
                    {
                        Attempt("Game Boost recovery", residuals, () => Task.Run(recoverGameBoost).GetAwaiter().GetResult());
                        closeServices?.Invoke();
                    }

                    Attempt("Windows registry recovery", residuals, () => RegistryTweakHistory.Restore(residuals));
                    Attempt("Power plan and SysMain recovery", residuals, () => WindowsTweakHistory.Restore(residuals));
                }

                if (File.Exists(journal))
                {
                    residuals.Add("Game Boost recovery journal retained.");
                }

                var legacyBackup = StoragePaths.DeviceTweakBackupsFilePath;
                if (File.Exists(legacyBackup))
                {
                    residuals.Add("device_tweak_backups.json retained for older driver changes without a recorded last-written value.");
                }

                var hadSettings = File.Exists(StoragePaths.SettingsFilePath);
                if (purge)
                {
                    RemoveData(residuals);
                }

                if (hadSettings)
                {
                    legacyNote = "If you applied tweaks with an older ThreadPilot version, verify Game Mode, pointer precision, SysMain, scheduler, prefetch, power throttling, power plans and IFEO priorities. Older versions could change these without recording a verifiable baseline.";
                }
            }
        }
        catch (Exception ex)
        {
            residuals.Add("Cleanup could not finish: " + ex.Message);
        }

        Report(residuals, silent, fromStoreWindow, legacyNote);
        return residuals.Count == 0 ? 0 : 1;
    }

    private static void StopMatchingInstance()
    {
        foreach (var process in Process.GetProcessesByName("ThreadPilot"))
        {
            using (process)
            {
                if (process.Id == Environment.ProcessId || process.HasExited)
                {
                    continue;
                }

                try
                {
                    if (process.MainModule?.FileName is not string path || !SamePath(path, Environment.ProcessPath!))
                    {
                        continue;
                    }

                    if (!EventWaitHandle.TryOpenExisting(ShutdownEventName(process.Id), out var signal))
                    {
                        throw new InvalidOperationException("Close the running copy of ThreadPilot from its tray menu and retry uninstall.");
                    }

                    using (signal)
                    {
                        if (!signal.Set())
                        {
                            throw new InvalidOperationException("Could not request a graceful ThreadPilot shutdown.");
                        }
                    }

                    if (!process.WaitForExit(30000))
                    {
                        throw new InvalidOperationException("ThreadPilot did not finish shutting down. Retry when cleanup has completed.");
                    }
                }
                catch (InvalidOperationException) when (process.HasExited)
                {
                }
            }
        }

        if (Mutex.TryOpenExisting(InstanceMutex, out var running))
        {
            running.Dispose();
            throw new InvalidOperationException("Another ThreadPilot distribution is running. Close it from its tray menu before uninstalling.");
        }
    }

    private static void RemoveStartup(string executable, bool sameUser, List<string> residuals)
    {
        Attempt("Scheduled tasks", residuals, () =>
        {
            var schedulerType = Type.GetTypeFromProgID("Schedule.Service", throwOnError: true)!;
            dynamic scheduler = Activator.CreateInstance(schedulerType)!;
            scheduler.Connect();
            dynamic folder = scheduler.GetFolder("\\");
            var names = StoreDistribution.IsStoreBuild
                ? new[] { "ThreadPilot_Store_Startup", "ThreadPilot_Store_Launch" }
                : new[] { "ThreadPilot_Startup", "ThreadPilot_Launch" };

            foreach (var name in names)
            {
                Attempt(name, residuals, () =>
                {
                    dynamic task;
                    try
                    {
                        task = folder.GetTask(name);
                    }
                    catch (Exception ex) when (ex.HResult == unchecked((int)0x80070002))
                    {
                        return;
                    }

                    var definition = System.Xml.Linq.XDocument.Parse((string)task.Xml);
                    var actions = definition.Descendants().Where(e => e.Name.LocalName == "Exec").ToArray();
                    if (actions.Length != 1)
                    {
                        residuals.Add($"{name}: unrecognized action retained.");
                        return;
                    }

                    var action = actions[0];
                    var command = action.Elements().FirstOrDefault(e => e.Name.LocalName == "Command")?.Value;
                    var arguments = action.Elements().FirstOrDefault(e => e.Name.LocalName == "Arguments")?.Value;
                    var packageLaunch = StoreDistribution.IsStoreBuild
                        && SamePath(Environment.ExpandEnvironmentVariables(command ?? string.Empty),
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"))
                        && string.Equals(arguments, $"shell:AppsFolder\\{PackageFamily}!App", StringComparison.OrdinalIgnoreCase);
                    var windowsApps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps") + Path.DirectorySeparatorChar;
                    var legacyStorePath = command != null && command.StartsWith(windowsApps, StringComparison.OrdinalIgnoreCase)
                        ? command[windowsApps.Length..].Split(Path.DirectorySeparatorChar)
                        : [];
                    var legacyStoreLaunch = StoreDistribution.IsStoreBuild && legacyStorePath.Length == 3
                        && StoreTaskPackage().IsMatch(legacyStorePath[0])
                        && string.Equals(legacyStorePath[1], "App", StringComparison.OrdinalIgnoreCase)
                        && string.Equals(legacyStorePath[2], "ThreadPilot.exe", StringComparison.OrdinalIgnoreCase);
                    if (packageLaunch || legacyStoreLaunch || SamePath(command, executable))
                    {
                        folder.DeleteTask(name, 0);
                    }
                    else
                    {
                        residuals.Add($"{name}: task points to another copy or an unrecognized command.");
                    }
                });
            }
        });

        if (sameUser && !StoreDistribution.IsStoreBuild)
        {
            Attempt("Legacy startup entry", residuals, () =>
            {
                using var run = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
                if (run?.GetValue("ThreadPilot") is string command
                    && (SamePath(command, executable)
                        || command.StartsWith($"\"{executable}\" ", StringComparison.OrdinalIgnoreCase)
                        || command.StartsWith(executable + " ", StringComparison.OrdinalIgnoreCase)))
                {
                    run.DeleteValue("ThreadPilot", throwOnMissingValue: false);
                }
            });
        }
    }

    private static void RemoveData(List<string> residuals)
    {
        var roaming = StoragePaths.AppDataRoot;
        var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ThreadPilot");
        var temp = Path.GetTempPath();
        Attempt("User data", residuals, () =>
        {
            using var identity = WindowsIdentity.GetCurrent();
            // Use the interactive user's shell token. A linked UAC token can be limited to identification.
            _ = GetWindowThreadProcessId(GetShellWindow(), out var shellPid);
            if (shellPid == 0)
            {
                throw new InvalidOperationException("No interactive shell token is available.");
            }

            using var shell = Process.GetProcessById((int)shellPid);
            const uint tokenDuplicateAndQuery = 0x0002 | 0x0008;
            if (!OpenProcessToken(shell.SafeHandle, tokenDuplicateAndQuery, out var token))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            using (token)
            using (var shellIdentity = new WindowsIdentity(token.DangerousGetHandle()))
            {
                if (shellIdentity.User != identity.User
                    || new WindowsPrincipal(shellIdentity).IsInRole(WindowsBuiltInRole.Administrator))
                {
                    throw new InvalidOperationException("The shell does not have a limited token for this user.");
                }

                WindowsIdentity.RunImpersonated(token, () => RemoveUserData(roaming, local, temp, residuals));
            }
        });
        Attempt("Update and task temporary folders", residuals, () =>
        {
            var administrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            foreach (var directory in new DirectoryInfo(AdminOnlyDirectory.DefaultRoot).EnumerateDirectories("ThreadPilot-*"))
            {
                if (!TempFolderName().IsMatch(directory.Name))
                {
                    continue;
                }

                Attempt(directory.FullName, residuals, () =>
                {
                    if (HasReparsePoint(directory.FullName)
                        || !administrators.Equals(directory.GetAccessControl().GetOwner(typeof(SecurityIdentifier)))
                        || directory.EnumerateFileSystemInfos().Any(item =>
                            (item.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0))
                    {
                        residuals.Add($"{directory.FullName}: ownership or contents could not be verified.");
                        return;
                    }

                    directory.Delete(recursive: true);
                });
            }
        });

    }

    private static void RemoveUserData(string root, string local, string temp, List<string> residuals)
    {
        RemoveFrom(root, name => name is "settings.json" or "core_masks.json" or "persistent_rules.json" or "ui-state.json");
        RemoveFrom(Path.Combine(root, "Configuration"), name => name.Equals("ProcessPowerPlanAssociations.json", StringComparison.OrdinalIgnoreCase));
        RemoveFrom(Path.Combine(root, "Profiles"), name => name.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
        RemoveFrom(Path.Combine(root, "Powerplans"), name => name.EndsWith(".pow", StringComparison.OrdinalIgnoreCase));
        RemoveFrom(Path.Combine(local, "Logs"), name => AppLogName().IsMatch(name));
        RemoveFrom(temp, name => name.Equals("ThreadPilot_Debug.log", StringComparison.OrdinalIgnoreCase), removeDirectory: false);

        foreach (var path in new[] { root, local })
        {
            Attempt(path, residuals, () =>
            {
                if (!DirectoryExistsForCleanup(path))
                {
                    return;
                }

                if (HasReparsePoint(path) || Directory.EnumerateFileSystemEntries(path).Any())
                {
                    residuals.Add($"{path}: remaining files were retained.");
                    return;
                }

                Directory.Delete(path);
            });
        }

        void RemoveFrom(string path, Func<string, bool> isOwnedFile, bool removeDirectory = true)
        {
            Attempt(path, residuals, () =>
            {
                if (!DirectoryExistsForCleanup(path))
                {
                    return;
                }

                if (HasReparsePoint(path))
                {
                    residuals.Add($"{path}: linked folder retained.");
                    return;
                }

                foreach (var file in new DirectoryInfo(path).EnumerateFiles().Where(file => isOwnedFile(file.Name)))
                {
                    if (file.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    {
                        residuals.Add($"{file.FullName}: linked file retained.");
                        continue;
                    }

                    Attempt(file.FullName, residuals, file.Delete);
                }

                if (removeDirectory && !Directory.EnumerateFileSystemEntries(path).Any())
                {
                    Directory.Delete(path);
                }
            });
        }
    }

    private static bool DirectoryExistsForCleanup(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.Directory) != 0;
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException or FileNotFoundException)
        {
            return false;
        }
    }

    private static bool IsStoreInstalled()
    {
        uint count = 0;
        uint size = 0;
        var status = GetPackagesByPackageFamily(PackageFamily, ref count, IntPtr.Zero, ref size, IntPtr.Zero);
        return status switch
        {
            0 => count > 0,
            122 => true,
            _ => throw new Win32Exception(status, "Could not determine whether ThreadPilot from Microsoft Store is installed."),
        };
    }

    private static bool IsSessionUser()
    {
        using var process = Process.GetCurrentProcess();
        using var identity = WindowsIdentity.GetCurrent();
        var user = ReadSessionInfo(process.SessionId, 5);
        var domain = ReadSessionInfo(process.SessionId, 7);
        return !string.IsNullOrEmpty(user)
            && new NTAccount(domain ?? string.Empty, user).Translate(typeof(SecurityIdentifier)).Equals(identity.User);
    }

    private static string? ReadSessionInfo(int sessionId, int field)
    {
        if (!WTSQuerySessionInformation(IntPtr.Zero, sessionId, field, out var value, out _))
        {
            return null;
        }

        try
        {
            return Marshal.PtrToStringUni(value);
        }
        finally
        {
            WTSFreeMemory(value);
        }
    }

    internal static bool HasReparsePoint(string path)
    {
        for (string? current = Path.GetFullPath(path); current != null; current = Path.GetDirectoryName(current))
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool SamePath(string? left, string right) =>
        string.Equals(left?.Trim().Trim('"'), right, StringComparison.OrdinalIgnoreCase);

    private static string ShutdownEventName(int pid) => $@"Global\ThreadPilot_Uninstall_{pid}";

    private static void Attempt(string operation, List<string> residuals, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            residuals.Add($"{operation}: {ex.Message}");
        }
    }

    private static void Report(IReadOnlyList<string> residuals, bool silent, bool fromStoreWindow, string? legacyNote = null)
    {
        var summary = residuals.Count == 0
            ? "ThreadPilot cleanup completed."
            : "Items requiring attention:" + Environment.NewLine + string.Join(Environment.NewLine, residuals);
        if (legacyNote != null)
        {
            summary += Environment.NewLine + Environment.NewLine + legacyNote;
        }

        if (fromStoreWindow)
        {
            summary += Environment.NewLine + Environment.NewLine + "To remove the package, open Windows Settings → Apps → Installed apps → ThreadPilot.";
        }

        if (!silent)
        {
            System.Windows.MessageBox.Show(summary, "ThreadPilot cleanup", MessageBoxButton.OK,
                residuals.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }

        if (silent && (residuals.Count != 0 || legacyNote != null))
        {
            // Create a new admin-owned directory before writing from the elevated cleanup process.
            try
            {
                var directory = AdminOnlyDirectory.Create(Path.Combine(AdminOnlyDirectory.DefaultRoot, $"ThreadPilot-uninstall-{Guid.NewGuid():N}"));
                File.WriteAllText(Path.Combine(directory, "cleanup.log"), summary);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Trace.TraceWarning("Could not write ThreadPilot uninstall report: {0}", ex.Message);
            }
        }
    }

    [GeneratedRegex(@"^LorenzoMassafra[.]ThreadPilot_[0-9.]+_x64__2j208vxf88tt6$", RegexOptions.IgnoreCase)]
    private static partial Regex StoreTaskPackage();

    [GeneratedRegex(@"^ThreadPilot(?:_\d{8}_\d{6})?\.log$", RegexOptions.IgnoreCase)]
    private static partial Regex AppLogName();

    [GeneratedRegex(@"^ThreadPilot-(?:task-|update-[0-9A-Za-z.+-]+-)[0-9a-f]{32}$", RegexOptions.IgnoreCase)]
    private static partial Regex TempFolderName();

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(SafeProcessHandle process, uint access, out SafeAccessTokenHandle token);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int GetPackagesByPackageFamily(string family, ref uint count, IntPtr fullNames, ref uint bufferLength, IntPtr buffer);

    [DllImport("wtsapi32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSQuerySessionInformation(IntPtr server, int session, int infoClass, out IntPtr buffer, out int bytes);

    [DllImport("wtsapi32.dll")]
    private static extern void WTSFreeMemory(IntPtr memory);
}
