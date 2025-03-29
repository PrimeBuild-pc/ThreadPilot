using System;
using System.Globalization;
using System.Windows.Data;

namespace ThreadPilot.Converters
{
    /// <summary>
    /// Converts a boolean value to its inverse.
    /// </summary>
    [ValueConversion(typeof(bool), typeof(bool))]
    public class BoolToInverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            
            return false;
        }
    }
}
