using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ThreadPilot.Converters
{
    /// <summary>
    /// Converts a boolean value to a background brush.
    /// </summary>
    [ValueConversion(typeof(bool), typeof(Brush))]
    public class BoolToBackgroundConverter : IValueConverter
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
                    // Return accent brush for selected, control background for not selected
                    return isSelected 
                        ? Application.Current.Resources["AccentBrush"] 
                        : Application.Current.Resources["ControlBackgroundBrush"];
                }
                
                if (param == "Active")
                {
                    // Return accent brush for active, border brush for not active
                    return isSelected 
                        ? Application.Current.Resources["AccentBrush"] 
                        : Application.Current.Resources["BorderBrush"];
                }
                
                if (param == "Success")
                {
                    // Return success brush for true, error brush for false
                    return isSelected 
                        ? Application.Current.Resources["SuccessBrush"] 
                        : Application.Current.Resources["ErrorBrush"];
                }
            }

            // Default: return control background for true, transparent for false
            return isSelected 
                ? Application.Current.Resources["ControlBackgroundBrush"] 
                : Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }
}
