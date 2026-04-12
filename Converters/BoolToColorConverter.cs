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
namespace ThreadPilot
{
    using System;
    using System.Globalization;
    using System.Windows;
    using System.Windows.Data;
    using System.Windows.Media;

    public class BoolToColorConverter : IValueConverter
    {
        public static readonly BoolToColorConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue
                    ? ResolveBrush("TextFillColorPrimaryBrush", System.Windows.Media.Brushes.Black)
                    : ResolveBrush("TextFillColorSecondaryBrush", System.Windows.Media.Brushes.Gray);
            }

            return ResolveBrush("TextFillColorSecondaryBrush", System.Windows.Media.Brushes.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private static System.Windows.Media.Brush ResolveBrush(string key, System.Windows.Media.Brush fallback)
        {
            if (System.Windows.Application.Current?.TryFindResource(key) is System.Windows.Media.Brush brush)
            {
                return brush;
            }

            return fallback;
        }
    }
}

