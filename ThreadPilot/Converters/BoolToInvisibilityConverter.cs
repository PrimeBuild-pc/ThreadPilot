using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ThreadPilot.Converters
{
    /// <summary>
    /// Converts a boolean value to Visibility.Collapsed if false, Visibility.Visible if true
    /// </summary>
    public class BoolToInvisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Converts a boolean value to Visibility
        /// </summary>
        /// <param name="value">The boolean value</param>
        /// <param name="targetType">The target type</param>
        /// <param name="parameter">If "Inverse" is provided, the conversion will be reversed</param>
        /// <param name="culture">The culture</param>
        /// <returns>Visibility.Visible if true, Visibility.Collapsed if false (or vice versa if parameter is "Inverse")</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool input = (bool)value;
            bool inverse = parameter as string == "Inverse";
            
            if (inverse)
            {
                input = !input;
            }
            
            return input ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Converts a Visibility value back to boolean
        /// </summary>
        /// <param name="value">The Visibility value</param>
        /// <param name="targetType">The target type</param>
        /// <param name="parameter">If "Inverse" is provided, the conversion will be reversed</param>
        /// <param name="culture">The culture</param>
        /// <returns>True if Visibility.Visible, false otherwise (or vice versa if parameter is "Inverse")</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Visibility visibility = (Visibility)value;
            bool inverse = parameter as string == "Inverse";
            bool result = visibility == Visibility.Visible;
            
            if (inverse)
            {
                result = !result;
            }
            
            return result;
        }
    }
}