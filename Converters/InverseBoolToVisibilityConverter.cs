namespace ThreadPilot.Converters
{
    using System;
    using System.Globalization;
    using System.Windows;
    using System.Windows.Data;

    public sealed class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is bool enabled && enabled ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is Visibility visibility && visibility != Visibility.Visible;
    }
}
