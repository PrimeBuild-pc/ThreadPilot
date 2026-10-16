namespace ThreadPilot.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    /// <summary>
    /// Deterministic lists Game Boost decides with. Audio and voice (Discord), capture (OBS) and
    /// peripheral software are deliberately in none of the built-in lists.
    /// </summary>
    public static class GameBoostProcessLists
    {
        /// <summary>Background moved off the game's cores in Automatic; the user can edit this list.</summary>
        public static readonly IReadOnlyList<string> DefaultBackgroundProcesses =
        [
            // Browsers
            "chrome", "msedge", "firefox", "opera", "brave", "vivaldi",

            // Sync clients
            "OneDrive", "Dropbox", "GoogleDriveFS", "iCloudDrive", "MEGAsync", "nextcloud",

            // Updaters
            "GoogleUpdate", "MicrosoftEdgeUpdate", "AdobeARM",

            // Launchers, once the game is running
            "steam", "steamwebhelper", "EpicGamesLauncher", "EpicWebHelper", "EADesktop", "GalaxyClient", "upc", "Battle.net",
        ];

        /// <summary>Added to the user's list in Isolation: processes that are certainly secondary while gaming.</summary>
        public static readonly IReadOnlyList<string> IsolationExtraProcesses =
        [
            "Widgets", "WidgetService", "PhoneExperienceHost",
            "Spotify", "vlc",
            "WINWORD", "EXCEL", "POWERPNT", "OUTLOOK", "olk", "ONENOTE", "Acrobat", "AcroRd32",
            "Code", "devenv", "Notion", "Obsidian",
        ];

        private static readonly string[] GameLibraryPathFragments =
        [
            @"\steamapps\common\",
            @"\Epic Games\",
            @"\XboxGames\",
            @"\GOG Games\",
            @"\GOG Galaxy\Games\",
            @"\EA Games\",
            @"\Ubisoft Game Launcher\games\",
            @"\Riot Games\",
        ];

        // Executables that live in game folders but are not the game.
        private static readonly string[] NonGameNameFragments =
        [
            "launcher", "update", "crash", "report", "setup", "install", "unins", "redist", "prereq",
            "easyanticheat", "battleye", "beservice", "vanguard", "riotclient", "helper", "overlay",
            "bootstrap", "cefsharp", "service",
        ];

        public static bool IsGame(
            string? processName,
            string? executablePath,
            IReadOnlyCollection<string>? manualGames,
            IReadOnlyCollection<string>? excludedGames)
        {
            var name = Normalize(processName);
            if (name.Length == 0)
            {
                return false;
            }

            if (Matches(excludedGames, name, executablePath))
            {
                return false;
            }

            if (Matches(manualGames, name, executablePath))
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(executablePath)
                && GameLibraryPathFragments.Any(fragment => executablePath.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                && !NonGameNameFragments.Any(fragment => name.Contains(fragment, StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsListedBackground(string? processName, string? executablePath, GameBoostMode mode, IReadOnlyCollection<string>? userBackground)
        {
            var name = Normalize(processName);
            return name.Length > 0
                && (Matches(userBackground, name, executablePath)
                    || (mode == GameBoostMode.Isolation && IsolationExtraProcesses.Any(entry => Normalize(entry).Equals(name, StringComparison.OrdinalIgnoreCase))));
        }

        /// <summary>Process name without directory and ".exe", so "C:\x\Game.exe", "Game.exe" and "game" all match.</summary>
        public static string Normalize(string? processName)
        {
            var name = Path.GetFileName((processName ?? string.Empty).Trim());
            return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
        }

        // An entry with a directory matches that exact executable; a bare name matches any path.
        private static bool Matches(IReadOnlyCollection<string>? entries, string normalizedName, string? executablePath) =>
            entries != null && entries.Any(entry =>
                !string.IsNullOrWhiteSpace(entry)
                && (entry.Contains('\\')
                    ? string.Equals(entry.Trim(), executablePath, StringComparison.OrdinalIgnoreCase)
                    : Normalize(entry).Equals(normalizedName, StringComparison.OrdinalIgnoreCase)));
    }
}
