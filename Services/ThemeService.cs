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
using System.Windows;
using Microsoft.Extensions.Logging;

namespace ThreadPilot.Services
{
    public class ThemeService : IThemeService
    {
        private const string LightThemeDictionaryPath = "Themes/FluentLight.xaml";
        private const string DarkThemeDictionaryPath = "Themes/FluentDark.xaml";

        private readonly ILogger<ThemeService> _logger;
        private ResourceDictionary? _activeThemeDictionary;

        public bool IsDarkTheme { get; private set; }

        public ThemeService(ILogger<ThemeService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
                for (int i = appResources.MergedDictionaries.Count - 1; i >= 0; i--)
                {
                    var dictionary = appResources.MergedDictionaries[i];
                    var source = dictionary.Source?.OriginalString;
                    if (string.Equals(source, LightThemeDictionaryPath, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(source, DarkThemeDictionaryPath, StringComparison.OrdinalIgnoreCase))
                    {
                        appResources.MergedDictionaries.RemoveAt(i);
                    }
                }

                _activeThemeDictionary = null;

                var nextDictionary = new ResourceDictionary { Source = targetUri };
                appResources.MergedDictionaries.Insert(0, nextDictionary);
                _activeThemeDictionary = nextDictionary;
                IsDarkTheme = useDarkTheme;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply theme {ThemeUri}", targetUri);
            }
        }
    }
}
