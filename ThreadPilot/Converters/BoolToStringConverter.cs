using System;
using System.Globalization;
using System.Windows.Data;

namespace ThreadPilot.Converters
{
    /// <summary>
    /// Converts a boolean value to one of two strings based on the parameter
    /// Parameter format: "TrueString|FalseString"
    /// </summary>
    public class BoolToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && parameter is string paramString)
            {
                string[] options = paramString.Split('|');
                
                if (options.Length == 2)
                {
                    return boolValue ? options[0] : options[1];
                }
            }
            
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue && parameter is string paramString)
            {
                string[] options = paramString.Split('|');
                
                if (options.Length == 2)
                {
                    return stringValue == options[0];
                }
            }
            
            return false;
        }
    }
}