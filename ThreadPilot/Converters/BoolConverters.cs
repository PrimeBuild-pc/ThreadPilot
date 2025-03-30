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
        /// Converts a boolean value to a visibility value
        /// </summary>
        /// <param name="value">The boolean value</param>
        /// <param name="targetType">The target type</param>
        /// <param name="parameter">The converter parameter</param>
        /// <param name="culture">The culture</param>
        /// <returns>Visible if true, Collapsed if false</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                if (parameter != null && parameter.ToString() == "Invert")
                {
                    return boolValue ? Visibility.Collapsed : Visibility.Visible;
                }
                
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            
            return Visibility.Collapsed;
        }
        
        /// <summary>
        /// Converts a visibility value to a boolean value
        /// </summary>
        /// <param name="value">The visibility value</param>
        /// <param name="targetType">The target type</param>
        /// <param name="parameter">The converter parameter</param>
        /// <param name="culture">The culture</param>
        /// <returns>True if Visible, false if Collapsed</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibilityValue)
            {
                if (parameter != null && parameter.ToString() == "Invert")
                {
                    return visibilityValue != Visibility.Visible;
                }
                
                return visibilityValue == Visibility.Visible;
            }
            
            return false;
        }
    }
    
    /// <summary>
    /// Converts a boolean value to a font weight value
    /// </summary>
    public class BoolToFontWeightConverter : IValueConverter
    {
        /// <summary>
        /// Converts a boolean value to a font weight value
        /// </summary>
        /// <param name="value">The boolean value</param>
        /// <param name="targetType">The target type</param>
        /// <param name="parameter">The converter parameter</param>
        /// <param name="culture">The culture</param>
        /// <returns>Bold if true, Normal if false</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? FontWeights.Bold : FontWeights.Normal;
            }
            
            return FontWeights.Normal;
        }
        
        /// <summary>
        /// Converts a font weight value to a boolean value
        /// </summary>
        /// <param name="value">The font weight value</param>
        /// <param name="targetType">The target type</param>
        /// <param name="parameter">The converter parameter</param>
        /// <param name="culture">The culture</param>
        /// <returns>True if Bold, false if Normal</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is FontWeight fontWeight)
            {
                return fontWeight == FontWeights.Bold;
            }
            
            return false;
        }
    }
    
    /// <summary>
    /// Converts a boolean value to a brush value
    /// </summary>
    public class BoolToBrushConverter : IValueConverter
    {
        /// <summary>
        /// Converts a boolean value to a brush value
        /// </summary>
        /// <param name="value">The boolean value</param>
        /// <param name="targetType">The target type</param>
        /// <param name="parameter">The converter parameter</param>
        /// <param name="culture">The culture</param>
        /// <returns>The brush value</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Red);
            }
            
            return new SolidColorBrush(Colors.Gray);
        }
        
        /// <summary>
        /// Converts a brush value to a boolean value
        /// </summary>
        /// <param name="value">The brush value</param>
        /// <param name="targetType">The target type</param>
        /// <param name="parameter">The converter parameter</param>
        /// <param name="culture">The culture</param>
        /// <returns>True if green, false if red</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SolidColorBrush brush)
            {
                return brush.Color == Colors.Green;
            }
            
            return false;
        }
    }
    
    /// <summary>
    /// Converts a boolean value to a monitoring text value
    /// </summary>
    public class BoolToMonitoringTextConverter : IValueConverter
    {
        /// <summary>
        /// Converts a boolean value to a monitoring text value
        /// </summary>
        /// <param name="value">The boolean value</param>
        /// <param name="targetType">The target type</param>
        /// <param name="parameter">The converter parameter</param>
        /// <param name="culture">The culture</param>
        /// <returns>Stop Monitoring if true, Start Monitoring if false</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? "Stop Monitoring" : "Start Monitoring";
            }
            
            return "Start Monitoring";
        }
        
        /// <summary>
        /// Converts a monitoring text value to a boolean value
        /// </summary>
        /// <param name="value">The monitoring text value</param>
        /// <param name="targetType">The target type</param>
        /// <param name="parameter">The converter parameter</param>
        /// <param name="culture">The culture</param>
        /// <returns>True if Stop Monitoring, false if Start Monitoring</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string textValue)
            {
                return textValue == "Stop Monitoring";
            }
            
            return false;
        }
    }
    
    /// <summary>
    /// Converts a boolean value to a device type text value
    /// </summary>
    public class BoolToDeviceTypeConverter : IValueConverter
    {
        /// <summary>
        /// Converts a boolean value to a device type text value
        /// </summary>
        /// <param name="value">The boolean value</param>
        /// <param name="targetType">The target type</param>
        /// <param name="parameter">The converter parameter</param>
        /// <param name="culture">The culture</param>
        /// <returns>Laptop if true, Desktop if false</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? "Laptop" : "Desktop";
            }
            
            return "Unknown";
        }
        
        /// <summary>
        /// Converts a device type text value to a boolean value
        /// </summary>
        /// <param name="value">The device type text value</param>
        /// <param name="targetType">The target type</param>
        /// <param name="parameter">The converter parameter</param>
        /// <param name="culture">The culture</param>
        /// <returns>True if Laptop, false if Desktop</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string textValue)
            {
                return textValue == "Laptop";
            }
            
            return false;
        }
    }
}