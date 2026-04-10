/*
 * ThreadPilot - Advanced Windows Process and Power Plan Manager
 * Copyright (C) 2025 Prime Build
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, version 3 only.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using System;
using System.IO;

namespace ThreadPilot.Services
{
    internal static class StoragePaths
    {
        public static string AppDataRoot { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ThreadPilot");

        public static string SettingsFilePath => Path.Combine(AppDataRoot, "settings.json");
        public static string ProfilesDirectory => Path.Combine(AppDataRoot, "Profiles");
        public static string ConfigurationDirectory => Path.Combine(AppDataRoot, "Configuration");
        public static string CoreMasksFilePath => Path.Combine(AppDataRoot, "core_masks.json");
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
