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
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ThreadPilot.Helpers
{
    public class BytesToMbConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long bytes)
            {
                return Math.Round((double)bytes / (1024 * 1024), 1);
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class AffinityMaskConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long mask)
            {
                var selectedIndices = new System.Collections.Generic.List<int>();
                // Dynamic core count based on system, limited to 64 (long is 64-bit)
                int maxCores = Math.Min(64, Environment.ProcessorCount);

                for (int i = 0; i < maxCores; i++)
                {
                    if ((mask & (1L << i)) != 0)
                    {
                        selectedIndices.Add(i);
                    }
                }

                if (selectedIndices.Count == 0)
                    return "None";

                // Build the display string
                var indicesStr = string.Join(", ", selectedIndices);
                int selectedCount = selectedIndices.Count;

                // Detect if this is likely physical cores only (every other logical processor)
                // This heuristic checks if selected indices are evenly spaced by 2 (e.g., 0,2,4,6,8...)
                bool isProbablyPhysicalCoresOnly = false;
                if (selectedCount > 1 && selectedCount <= maxCores / 2)
                {
                    isProbablyPhysicalCoresOnly = true;
                    for (int i = 1; i < selectedIndices.Count; i++)
                    {
                        if (selectedIndices[i] - selectedIndices[i - 1] != 2)
                        {
                            isProbablyPhysicalCoresOnly = false;
                            break;
                        }
                    }
                }

                // Choose terminology based on what's selected
                string label;
                if (selectedCount == maxCores)
                {
                    label = $"All threads (0-{maxCores - 1})";
                }
                else if (isProbablyPhysicalCoresOnly && selectedIndices[0] == 0)
                {
                    label = $"Physical cores ({indicesStr}) - {selectedCount} cores";
                }
                else
                {
                    label = $"Threads ({indicesStr}) - {selectedCount} threads";
                }

                return label;
            }
            return "Unknown";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToFontWeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? FontWeights.Bold : FontWeights.Normal;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
