using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ThreadPilot.Converters
{
    /// <summary>
    /// Converts a boolean value indicating whether a core is parked to a text description and color
    /// </summary>
    public class BoolToParkedConverter : IValueConverter
    {
        /// <summary>
        /// Converts a boolean value to a text description and color
        /// </summary>
        /// <param name="value">The source boolean value</param>
        /// <param name="targetType">The target type</param>
        /// <param name="parameter">The converter parameter</param>
        /// <param name="culture">The culture information</param>
        /// <returns>
        /// "Parked" if true, "Active" if false.
        /// When returning a Brush, ParkedBrush if true, UnparkedBrush if false.
        /// </returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not bool isParked)
            {
                return "Unknown";
            }
            
            if (targetType == typeof(string))
            {
                return isParked ? "Parked" : "Active";
            }
            
            if (targetType == typeof(Brush))
            {
                return isParked 
                    ? new SolidColorBrush((Color)System.Windows.Application.Current.Resources["WarningColor"]) 
                    : new SolidColorBrush((Color)System.Windows.Application.Current.Resources["SuccessColor"]);
            }
            
            return value;
        }
        
        /// <summary>
        /// Converts back from text to boolean (not implemented)
        /// </summary>
        /// <param name="value">The value</param>
        /// <param name="targetType">The target type</param>
        /// <param name="parameter">The parameter</param>
        /// <param name="culture">The culture information</param>
        /// <returns>The original value (not implemented)</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}