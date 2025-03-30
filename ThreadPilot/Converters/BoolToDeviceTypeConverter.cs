using System;
using System.Globalization;
using System.Windows.Data;

namespace ThreadPilot.Converters
{
    /// <summary>
    /// Converts a boolean value representing battery presence to a device type string (Laptop or Desktop).
    /// </summary>
    public class BoolToDeviceTypeConverter : IValueConverter
    {
        public string LaptopText { get; set; } = "Laptop";
        public string DesktopText { get; set; } = "Desktop";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool hasBattery)
            {
                return hasBattery ? LaptopText : DesktopText;
            }

            return DesktopText;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string deviceType)
            {
                return deviceType == LaptopText;
            }

            return false;
        }
    }
}