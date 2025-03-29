using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ThreadPilot.Controls
{
    /// <summary>
    /// A toggle switch control that mimics the modern switch UI element
    /// </summary>
    public class ToggleSwitch : CheckBox
    {
        static ToggleSwitch()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ToggleSwitch), new FrameworkPropertyMetadata(typeof(ToggleSwitch)));
        }

        public ToggleSwitch()
        {
            // Default values
            OnColor = new SolidColorBrush(Color.FromRgb(70, 128, 255)); // #4680FF
            OffColor = new SolidColorBrush(Color.FromRgb(64, 64, 64)); // #404040
            ThumbColor = new SolidColorBrush(Colors.White);
            OnText = "On";
            OffText = "Off";
        }

        #region DependencyProperties

        public static readonly DependencyProperty OnColorProperty =
            DependencyProperty.Register("OnColor", typeof(Brush), typeof(ToggleSwitch), 
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(70, 128, 255))));

        public static readonly DependencyProperty OffColorProperty =
            DependencyProperty.Register("OffColor", typeof(Brush), typeof(ToggleSwitch), 
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(64, 64, 64))));

        public static readonly DependencyProperty ThumbColorProperty =
            DependencyProperty.Register("ThumbColor", typeof(Brush), typeof(ToggleSwitch), 
                new PropertyMetadata(new SolidColorBrush(Colors.White)));

        public static readonly DependencyProperty OnTextProperty =
            DependencyProperty.Register("OnText", typeof(string), typeof(ToggleSwitch), 
                new PropertyMetadata("On"));

        public static readonly DependencyProperty OffTextProperty =
            DependencyProperty.Register("OffText", typeof(string), typeof(ToggleSwitch), 
                new PropertyMetadata("Off"));

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the background color when the switch is in the On state
        /// </summary>
        public Brush OnColor
        {
            get { return (Brush)GetValue(OnColorProperty); }
            set { SetValue(OnColorProperty, value); }
        }

        /// <summary>
        /// Gets or sets the background color when the switch is in the Off state
        /// </summary>
        public Brush OffColor
        {
            get { return (Brush)GetValue(OffColorProperty); }
            set { SetValue(OffColorProperty, value); }
        }

        /// <summary>
        /// Gets or sets the color of the thumb (the moving circle)
        /// </summary>
        public Brush ThumbColor
        {
            get { return (Brush)GetValue(ThumbColorProperty); }
            set { SetValue(ThumbColorProperty, value); }
        }

        /// <summary>
        /// Gets or sets the text displayed when the switch is in the On state
        /// </summary>
        public string OnText
        {
            get { return (string)GetValue(OnTextProperty); }
            set { SetValue(OnTextProperty, value); }
        }

        /// <summary>
        /// Gets or sets the text displayed when the switch is in the Off state
        /// </summary>
        public string OffText
        {
            get { return (string)GetValue(OffTextProperty); }
            set { SetValue(OffTextProperty, value); }
        }

        #endregion
    }
}
