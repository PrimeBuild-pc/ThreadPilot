using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ThreadPilot.Converters
{
    /// <summary>
    /// Converts a boolean value to a Brush value, where true is Green and false is Red by default.
    /// Can be customized by specifying TrueBrush and FalseBrush as parameters.
    /// </summary>
    public class BoolToBrushConverter : IValueConverter
    {
        public Brush TrueBrush { get; set; } = Brushes.Green;
        public Brush FalseBrush { get; set; } = Brushes.Red;

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
                return result ? TrueBrush : FalseBrush;
            }

            return FalseBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Cannot convert back from Brush to bool in a meaningful way
            throw new NotImplementedException();
        }
    }
}