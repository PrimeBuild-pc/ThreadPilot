using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ThreadPilot.Converters
{
    /// <summary>
    /// Converts a boolean value to a FontWeight value, where true is Bold and false is Normal.
    /// </summary>
    public class BoolToFontWeightConverter : IValueConverter
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
                return result ? FontWeights.Bold : FontWeights.Normal;
            }

            return FontWeights.Normal;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is FontWeight fontWeight)
            {
                // Check if we need to invert the conversion
                bool invert = false;
                if (parameter is string paramString && paramString.ToLowerInvariant() == "invert")
                {
                    invert = true;
                }

                bool result = fontWeight == FontWeights.Bold;
                return invert ? !result : result;
            }

            return false;
        }
    }
}