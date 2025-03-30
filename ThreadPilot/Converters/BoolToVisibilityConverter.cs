using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ThreadPilot.Converters
{
    /// <summary>
    /// Converts a boolean value to a Visibility value, where true is Visible and false is Collapsed.
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                // Check if we need to invert the conversion
                bool invert = false;
                if (parameter is string paramString && paramString.ToLowerInvariant() == "invert")
                {
                    invert = true;
                }

                bool result = invert ? !boolValue : boolValue;
                return result ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                // Check if we need to invert the conversion
                bool invert = false;
                if (parameter is string paramString && paramString.ToLowerInvariant() == "invert")
                {
                    invert = true;
                }

                bool result = visibility == Visibility.Visible;
                return invert ? !result : result;
            }

            return false;
        }
    }
}