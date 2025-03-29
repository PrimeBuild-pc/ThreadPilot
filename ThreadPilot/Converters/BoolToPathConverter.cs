using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ThreadPilot.Converters
{
    /// <summary>
    /// Converts a boolean value to one of two Geometry paths
    /// Parameter format: "TrueResourceKey|FalseResourceKey"
    /// </summary>
    public class BoolToPathConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && parameter is string paramString)
            {
                string[] options = paramString.Split('|');
                
                if (options.Length == 2)
                {
                    string resourceKey = boolValue ? options[0] : options[1];
                    
                    // Try to find the named resource in the Application's resources
                    if (Application.Current.Resources.Contains(resourceKey))
                    {
                        return Application.Current.Resources[resourceKey];
                    }
                }
            }
            
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // This converter doesn't support two-way binding
            throw new NotImplementedException();
        }
    }
}