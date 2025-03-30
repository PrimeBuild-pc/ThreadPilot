using System;
using System.Globalization;
using System.Windows.Data;

namespace ThreadPilot.Converters
{
    /// <summary>
    /// Converts a relative value (0-1) to an absolute value based on a parameter
    /// </summary>
    public class RelativeToAbsoluteValueConverter : IValueConverter
    {
        /// <summary>
        /// Converts a relative value to an absolute value
        /// </summary>
        /// <param name="value">The source value (0-1)</param>
        /// <param name="targetType">The target type</param>
        /// <param name="parameter">The maximum value (optional)</param>
        /// <param name="culture">The culture information</param>
        /// <returns>The absolute value</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not double relativeValue)
            {
                return 0;
            }
            
            // Default maximum value
            double maxValue = 100;
            
            // If a parameter is provided, use it as the maximum value
            if (parameter is string maxValueString && double.TryParse(maxValueString, out double parsedMaxValue))
            {
                maxValue = parsedMaxValue;
            }
            
            // Convert the relative value to an absolute value
            return relativeValue * maxValue;
        }
        
        /// <summary>
        /// Converts an absolute value back to a relative value
        /// </summary>
        /// <param name="value">The absolute value</param>
        /// <param name="targetType">The target type</param>
        /// <param name="parameter">The maximum value (optional)</param>
        /// <param name="culture">The culture information</param>
        /// <returns>The relative value (0-1)</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not double absoluteValue)
            {
                return 0;
            }
            
            // Default maximum value
            double maxValue = 100;
            
            // If a parameter is provided, use it as the maximum value
            if (parameter is string maxValueString && double.TryParse(maxValueString, out double parsedMaxValue))
            {
                maxValue = parsedMaxValue;
            }
            
            // Prevent division by zero
            if (Math.Abs(maxValue) < double.Epsilon)
            {
                return 0;
            }
            
            // Convert the absolute value back to a relative value
            return absoluteValue / maxValue;
        }
    }
}