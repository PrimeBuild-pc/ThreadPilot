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

