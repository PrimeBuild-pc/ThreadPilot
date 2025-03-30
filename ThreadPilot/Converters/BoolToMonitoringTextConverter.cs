using System;
using System.Globalization;
using System.Windows.Data;

namespace ThreadPilot.Converters
{
    /// <summary>
    /// Converts a boolean value to a text string indicating whether monitoring is active or inactive.
    /// </summary>
    public class BoolToMonitoringTextConverter : IValueConverter
    {
        public string ActiveText { get; set; } = "Monitoring Active";
        public string InactiveText { get; set; } = "Monitoring Inactive";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? ActiveText : InactiveText;
            }

            return InactiveText;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue)
            {
                return stringValue == ActiveText;
            }

            return false;
        }
    }
}