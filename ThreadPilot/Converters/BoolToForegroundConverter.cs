using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ThreadPilot.Converters
{
    /// <summary>
    /// Converts a boolean value to a foreground brush.
    /// </summary>
    [ValueConversion(typeof(bool), typeof(Brush))]
    public class BoolToForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is bool))
                return DependencyProperty.UnsetValue;

            bool isSelected = (bool)value;

            if (parameter != null)
            {
                string param = parameter.ToString();
                
                if (param == "Selected")
                {
                    // Return accent foreground for selected, foreground for not selected
                    return isSelected 
                        ? Application.Current.Resources["AccentForegroundBrush"] 
                        : Application.Current.Resources["ForegroundBrush"];
                }
                
                if (param == "Active")
                {
                    // Return accent foreground for active, secondary foreground for not active
                    return isSelected 
                        ? Application.Current.Resources["AccentForegroundBrush"] 
                        : Application.Current.Resources["SecondaryForegroundBrush"];
                }
                
                if (param == "Success")
                {
                    // Return white for true, white for false (against colored backgrounds)
                    return Brushes.White;
                }
            }

            // Default: return foreground for true, secondary foreground for false
            return isSelected 
                ? Application.Current.Resources["ForegroundBrush"] 
                : Application.Current.Resources["SecondaryForegroundBrush"];
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }
}
