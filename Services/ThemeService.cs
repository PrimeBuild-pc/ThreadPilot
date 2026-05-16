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
    using System.Windows;
    using Microsoft.Extensions.Logging;
    using Microsoft.Win32;
    using Wpf.Ui.Appearance;
    using Wpf.Ui.Controls;

    public class ThemeService : IThemeService, IDisposable
    {
        private const string LightThemeDictionaryPath = "Themes/FluentLight.xaml";
        private const string DarkThemeDictionaryPath = "Themes/FluentDark.xaml";

        private readonly ILogger<ThemeService> logger;
        private ResourceDictionary? activeThemeDictionary;

        public bool IsDarkTheme { get; private set; }

        public ThemeService(ILogger<ThemeService> logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            SystemEvents.UserPreferenceChanged += this.OnUserPreferenceChanged;
        }

        public void ApplyTheme(bool useDarkTheme)
        {
            var targetUri = new Uri(useDarkTheme ? DarkThemeDictionaryPath : LightThemeDictionaryPath, UriKind.Relative);

            var appResources = System.Windows.Application.Current?.Resources;
            if (appResources == null)
            {
                return;
            }

            try
            {
                // Keep Wpf.Ui controls aligned with app theme (NavigationView, TitleBar, etc.).
                var applicationTheme = useDarkTheme ? ApplicationTheme.Dark : ApplicationTheme.Light;
                ApplicationThemeManager.Apply(applicationTheme, WindowBackdropType.Mica, updateAccent: true);

                // ThreadPilot overrides depend on the active Wpf.Ui theme and must remain last
                // because later merged dictionaries have precedence in WPF resource lookup.
                this.activeThemeDictionary = ThemeDictionaryPolicy.ReplaceThreadPilotThemeDictionary(appResources, targetUri);

                this.IsDarkTheme = useDarkTheme;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to apply theme {ThemeUri}", targetUri);
            }
        }

        public bool GetSystemUsesDarkTheme()
        {
            try
            {
                const string personalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

                using var key = Registry.CurrentUser.OpenSubKey(personalizeKey, writable: false);
                if (key != null)
                {
                    var appsThemeValue = key.GetValue("AppsUseLightTheme");
                    if (TryResolveDarkPreference(appsThemeValue, out var useDarkTheme))
                    {
                        return useDarkTheme;
                    }

                    // Fallback key used on some Windows configurations.
                    var systemThemeValue = key.GetValue("SystemUsesLightTheme");
                    if (TryResolveDarkPreference(systemThemeValue, out useDarkTheme))
                    {
                        return useDarkTheme;
                    }
                }

                var detectedTheme = ApplicationThemeManager.GetSystemTheme();
                if (detectedTheme == SystemTheme.Dark)
                {
                    return true;
                }

                if (detectedTheme == SystemTheme.Light)
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Failed to read system theme preference, falling back to light theme");
            }

            return false;
        }

        private static bool TryResolveDarkPreference(object? value, out bool useDarkTheme)
        {
            useDarkTheme = false;

            switch (value)
            {
                case int intValue:
                    useDarkTheme = intValue == 0;
                    return true;
                case long longValue:
                    useDarkTheme = longValue == 0;
                    return true;
                case string stringValue when int.TryParse(stringValue, out var parsed):
                    useDarkTheme = parsed == 0;
                    return true;
                default:
                    return false;
            }
        }

        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category != UserPreferenceCategory.Color &&
                e.Category != UserPreferenceCategory.General &&
                e.Category != UserPreferenceCategory.VisualStyle)
            {
                return;
            }

            try
            {
                this.ApplyTheme(this.GetSystemUsesDarkTheme());
            }
            catch (Exception ex)
            {
                this.logger.LogDebug(ex, "Failed to apply theme after system preference change");
            }
        }

        public void Dispose()
        {
            SystemEvents.UserPreferenceChanged -= this.OnUserPreferenceChanged;
        }
    }

    internal static class ThemeDictionaryPolicy
    {
        public static bool IsThreadPilotThemeDictionary(string? source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            var normalized = source.Replace('\\', '/');
            return normalized.EndsWith("Themes/FluentLight.xaml", StringComparison.OrdinalIgnoreCase) ||
                normalized.EndsWith("Themes/FluentDark.xaml", StringComparison.OrdinalIgnoreCase);
        }

        public static int GetInsertionIndex(int mergedDictionaryCount)
        {
            return Math.Max(0, mergedDictionaryCount);
        }

        public static ResourceDictionary ReplaceThreadPilotThemeDictionary(ResourceDictionary appResources, Uri targetUri)
        {
            return ReplaceThreadPilotThemeDictionary(
                appResources,
                targetUri,
                uri => new ResourceDictionary { Source = uri });
        }

        internal static ResourceDictionary ReplaceThreadPilotThemeDictionary(
            ResourceDictionary appResources,
            Uri targetUri,
            Func<Uri, ResourceDictionary> dictionaryFactory)
        {
            ArgumentNullException.ThrowIfNull(appResources);
            ArgumentNullException.ThrowIfNull(targetUri);
            ArgumentNullException.ThrowIfNull(dictionaryFactory);

            for (int i = appResources.MergedDictionaries.Count - 1; i >= 0; i--)
            {
                var dictionary = appResources.MergedDictionaries[i];
                if (IsThreadPilotThemeDictionary(dictionary.Source?.OriginalString))
                {
                    appResources.MergedDictionaries.RemoveAt(i);
                }
            }

            var nextDictionary = dictionaryFactory(targetUri);
            appResources.MergedDictionaries.Insert(
                GetInsertionIndex(appResources.MergedDictionaries.Count),
                nextDictionary);

            return nextDictionary;
        }
    }
}
