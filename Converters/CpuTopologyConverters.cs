namespace ThreadPilot.Converters
{
    using System;
    using System.Globalization;
    using System.Windows;
    using System.Windows.Data;
    using System.Windows.Media;
    using ThreadPilot.Models;

    public class CoreTypeToColorConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2)
            {
                return ResolveBrush("TextFillColorPrimaryBrush", System.Windows.Media.Brushes.Black);
            }

            var coreType = values[0] as CpuCoreType? ?? CpuCoreType.Unknown;
            var isHyperThreaded = values[1] as bool? ?? false;

            return coreType switch
            {
                CpuCoreType.PerformanceCore => isHyperThreaded
                    ? ResolveBrush("SystemAccentColorSecondaryBrush", System.Windows.Media.Brushes.DodgerBlue)
                    : ResolveBrush("SystemAccentColorPrimaryBrush", System.Windows.Media.Brushes.Blue),
                CpuCoreType.EfficiencyCore => isHyperThreaded
                    ? ResolveBrush("TextFillColorSecondaryBrush", System.Windows.Media.Brushes.DarkGray)
                    : ResolveBrush("TextFillColorPrimaryBrush", System.Windows.Media.Brushes.Black),
                CpuCoreType.Zen or CpuCoreType.ZenPlus or CpuCoreType.Zen2 or CpuCoreType.Zen3 or CpuCoreType.Zen4 =>
                    isHyperThreaded
                        ? ResolveBrush("SystemAccentColorSecondaryBrush", System.Windows.Media.Brushes.DarkOrange)
                        : ResolveBrush("SystemAccentColorPrimaryBrush", System.Windows.Media.Brushes.Orange),
                _ => ResolveBrush("TextFillColorSecondaryBrush", System.Windows.Media.Brushes.Gray),
            };
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
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

    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool success)
            {
                return success
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

    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool visible)
            {
                return visible ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            }
            return System.Windows.Visibility.Collapsed;
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
                if (mask == 0)
                {
                    return "None";
                }

                var cores = new System.Collections.Generic.List<int>();
                for (int i = 0; i < 64; i++)
                {
                    if ((mask & (1L << i)) != 0)
                    {
                        cores.Add(i);
                    }
                }

                if (cores.Count == 0)
                {
                    return "None";
                }

                if (cores.Count == 1)
                {
                    return $"Core {cores[0]}";
                }

                if (cores.Count <= 4)
                {
                    return $"Cores {string.Join(", ", cores)}";
                }

                return $"Cores {cores[0]}-{cores[cores.Count - 1]} ({cores.Count} cores)";
            }
            return "Unknown";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BytesToMbConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long bytes)
            {
                return (bytes / (1024.0 * 1024.0)).ToString("F1");
            }
            return "0.0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StringToVisibilityConverter : IValueConverter
    {
        public static readonly StringToVisibilityConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.IsNullOrEmpty(value as string) ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

