using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ThreadPilot.Converters
{
    /// <summary>
    /// Converts an integer value to a boolean value by comparing it to a parameter value.
    /// </summary>
    [ValueConversion(typeof(int), typeof(bool))]
    public class IntToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is int))
                return DependencyProperty.UnsetValue;

            int intValue = (int)value;
            
            if (parameter == null)
                return intValue != 0;

            if (int.TryParse(parameter.ToString(), out int paramValue))
            {
                return intValue == paramValue;
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is bool))
                return DependencyProperty.UnsetValue;

            bool boolValue = (bool)value;
            
            if (!boolValue)
                return 0;

            if (parameter == null)
                return 1;

            if (int.TryParse(parameter.ToString(), out int paramValue))
            {
                return paramValue;
            }

            return 0;
        }
    }
}
