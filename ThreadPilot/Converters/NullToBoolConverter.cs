using System;
using System.Globalization;
using System.Windows.Data;

namespace ThreadPilot.Converters
{
    /// <summary>
    /// Converts null/non-null value to a boolean value.
    /// </summary>
    [ValueConversion(typeof(object), typeof(bool))]
    public class NullToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter != null && parameter.ToString() == "Inverse")
            {
                return value == null;
            }
            
            return value != null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
