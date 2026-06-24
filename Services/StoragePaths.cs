namespace ThreadPilot.Services
{
    using System;
    using System.IO;

    internal static class StoragePaths
    {
        public static string AppDataRoot { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ThreadPilot");

        public static string SettingsFilePath => Path.Combine(AppDataRoot, "settings.json");

        public static string ProfilesDirectory => Path.Combine(AppDataRoot, "Profiles");

        public static string ConfigurationDirectory => Path.Combine(AppDataRoot, "Configuration");

        public static string CoreMasksFilePath => Path.Combine(AppDataRoot, "core_masks.json");

        public static string PersistentRulesFilePath => Path.Combine(AppDataRoot, "persistent_rules.json");

        public static string PowerPlansDirectory => Path.Combine(AppDataRoot, "Powerplans");

        public static void EnsureAppDataDirectories()
        {
            Directory.CreateDirectory(AppDataRoot);
            Directory.CreateDirectory(ProfilesDirectory);
            Directory.CreateDirectory(ConfigurationDirectory);
            Directory.CreateDirectory(PowerPlansDirectory);
        }
    }
}
