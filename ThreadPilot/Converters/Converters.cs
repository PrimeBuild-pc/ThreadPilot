using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ThreadPilot.Converters
{
    /// <summary>
    /// Converts a boolean value to a visibility value
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Converts a boolean to a visibility
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                // If parameter is specified and true, invert the logic
                if (parameter is string paramString && paramString == "Invert")
                {
                    return boolValue ? Visibility.Collapsed : Visibility.Visible;
                }

                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Collapsed;
        }

        /// <summary>
        /// Converts visibility back to a boolean
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                if (parameter is string paramString && paramString == "Invert")
                {
                    return visibility != Visibility.Visible;
                }

                return visibility == Visibility.Visible;
            }

            return false;
        }
    }

    /// <summary>
    /// Converts a boolean value to a font weight
    /// </summary>
    public class BoolToFontWeightConverter : IValueConverter
    {
        /// <summary>
        /// Converts a boolean to a font weight
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? FontWeights.Bold : FontWeights.Normal;
            }

            return FontWeights.Normal;
        }

        /// <summary>
        /// Not implemented
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts a progress value between 0-100 to a pixel width for a progress bar
    /// </summary>
    public class ProgressConverter : IValueConverter
    {
        /// <summary>
        /// Converts a progress value to a width
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double progressValue)
            {
                if (parameter is FrameworkElement element)
                {
                    return element.ActualWidth * (progressValue / 100.0);
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Not implemented
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts a numeric value to a color based on a threshold
    /// </summary>
    public class ThresholdToColorConverter : IValueConverter
    {
        /// <summary>
        /// Converts a value to a color based on thresholds
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double doubleValue)
            {
                if (parameter is string paramString)
                {
                    var parts = paramString.Split(',');
                    if (parts.Length >= 3 && 
                        double.TryParse(parts[0], out double threshold1) && 
                        double.TryParse(parts[1], out double threshold2))
                    {
                        if (doubleValue < threshold1)
                        {
                            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(parts[2]));
                        }
                        else if (doubleValue < threshold2 && parts.Length >= 4)
                        {
                            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(parts[3]));
                        }
                        else if (parts.Length >= 5)
                        {
                            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(parts[4]));
                        }
                    }
                }

                // Default gradient from green to red
                if (doubleValue < 30)
                {
                    return new SolidColorBrush(Colors.Green);
                }
                else if (doubleValue < 70)
                {
                    return new SolidColorBrush(Colors.Orange);
                }
                else
                {
                    return new SolidColorBrush(Colors.Red);
                }
            }

            return new SolidColorBrush(Colors.Gray);
        }

        /// <summary>
        /// Not implemented
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts a string to a boolean based on whether it's null or empty
    /// </summary>
    public class StringToBoolConverter : IValueConverter
    {
        /// <summary>
        /// Converts a string to a boolean
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue)
            {
                bool isEmpty = string.IsNullOrWhiteSpace(stringValue);
                
                if (parameter is string paramString && paramString == "Invert")
                {
                    return isEmpty;
                }

                return !isEmpty;
            }

            return parameter is string paramString && paramString == "Invert";
        }

        /// <summary>
        /// Not implemented
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}