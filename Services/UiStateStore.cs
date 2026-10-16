namespace ThreadPilot.Services
{
    using System;
    using System.IO;
    using System.Text.Json;

    // Layout choices the user makes by hand (menu, window, process list view). Kept out of settings.json
    // so they never mark the Settings page as unsaved and never travel in exported configurations.
    public sealed record UiState
    {
        public bool NavigationPaneOpen { get; init; } = true;

        public double? WindowLeft { get; init; }

        public double? WindowTop { get; init; }

        public double? WindowWidth { get; init; }

        public double? WindowHeight { get; init; }

        public bool WindowMaximized { get; init; }

        public bool ShowActiveApplicationsOnly { get; init; }

        public bool HideSystemProcesses { get; init; }

        public bool HideIdleProcesses { get; init; }

        public string SortMode { get; init; } = "CpuUsage";

        public bool ShowPidColumn { get; init; } = true;

        public bool ShowWindowTitleColumn { get; init; }

        public bool ShowHasWindowColumn { get; init; }
    }

    public static class UiStateStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
        private static readonly object Gate = new();
        private static UiState? current;

        internal static string FilePath { get; set; } = Path.Combine(StoragePaths.AppDataRoot, "ui-state.json");

        public static UiState Current
        {
            get
            {
                lock (Gate)
                {
                    return current ??= Load();
                }
            }
        }

        public static void Update(Func<UiState, UiState> change)
        {
            lock (Gate)
            {
                var updated = change(current ??= Load());
                if (updated == current)
                {
                    return;
                }

                current = updated;
                try
                {
                    // Synchronous on purpose: this also runs while the window closes, right before exit.
                    Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                    var temp = FilePath + ".tmp";
                    File.WriteAllText(temp, JsonSerializer.Serialize(updated, JsonOptions));
                    File.Move(temp, FilePath, overwrite: true);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Losing a layout preference is harmless; the next change tries again.
                }
            }
        }

        internal static void ResetForTests() => current = null;

        private static UiState Load()
        {
            try
            {
                return File.Exists(FilePath)
                    ? JsonSerializer.Deserialize<UiState>(File.ReadAllText(FilePath), JsonOptions) ?? new UiState()
                    : new UiState();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                return new UiState();
            }
        }
    }
}
