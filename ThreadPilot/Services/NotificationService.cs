using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Implementation of INotificationService for Windows desktop application
    /// </summary>
    public class NotificationService : INotificationService
    {
        private readonly Window _mainWindow;
        private readonly Panel _notificationContainer;
        private const int DEFAULT_NOTIFICATION_DURATION = 5000; // 5 seconds

        /// <summary>
        /// Constructs a new instance of the NotificationService
        /// </summary>
        /// <param name="mainWindow">The main window to display notifications in</param>
        /// <param name="notificationContainer">The panel to host notifications</param>
        public NotificationService(Window mainWindow, Panel notificationContainer)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _notificationContainer = notificationContainer ?? throw new ArgumentNullException(nameof(notificationContainer));
        }

        /// <summary>
        /// Shows a notification
        /// </summary>
        /// <param name="message">The message to display</param>
        /// <param name="title">The title of the notification</param>
        /// <param name="duration">Duration in milliseconds, or null for default</param>
        public void ShowNotification(string message, string title = "ThreadPilot", int? duration = null)
        {
            ShowNotificationInternal(message, title, Colors.DodgerBlue, duration);
        }

        /// <summary>
        /// Shows a success notification
        /// </summary>
        /// <param name="message">The message to display</param>
        /// <param name="title">The title of the notification</param>
        /// <param name="duration">Duration in milliseconds, or null for default</param>
        public void ShowSuccess(string message, string title = "Success", int? duration = null)
        {
            ShowNotificationInternal(message, title, Colors.Green, duration);
        }

        /// <summary>
        /// Shows an error notification
        /// </summary>
        /// <param name="message">The message to display</param>
        /// <param name="title">The title of the notification</param>
        /// <param name="duration">Duration in milliseconds, or null for default</param>
        public void ShowError(string message, string title = "Error", int? duration = null)
        {
            ShowNotificationInternal(message, title, Colors.Red, duration);
        }

        /// <summary>
        /// Shows a warning notification
        /// </summary>
        /// <param name="message">The message to display</param>
        /// <param name="title">The title of the notification</param>
        /// <param name="duration">Duration in milliseconds, or null for default</param>
        public void ShowWarning(string message, string title = "Warning", int? duration = null)
        {
            ShowNotificationInternal(message, title, Colors.Orange, duration);
        }

        /// <summary>
        /// Shows a confirmation dialog
        /// </summary>
        /// <param name="message">The message to display</param>
        /// <param name="title">The title of the dialog</param>
        /// <returns>True if confirmed, otherwise false</returns>
        public async Task<bool> ShowConfirmation(string message, string title = "Confirm")
        {
            // Run on UI thread
            return await _mainWindow.Dispatcher.InvokeAsync(() =>
            {
                var result = MessageBox.Show(
                    _mainWindow,
                    message,
                    title,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                return result == MessageBoxResult.Yes;
            });
        }

        /// <summary>
        /// Shows an input dialog
        /// </summary>
        /// <param name="message">The message to display</param>
        /// <param name="title">The title of the dialog</param>
        /// <param name="defaultValue">The default value</param>
        /// <returns>The input value, or null if cancelled</returns>
        public async Task<string> ShowInputDialog(string message, string title = "Input", string defaultValue = "")
        {
            // Create an input dialog
            return await _mainWindow.Dispatcher.InvokeAsync(() =>
            {
                var dialog = new Window
                {
                    Title = title,
                    Width = 400,
                    Height = 180,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = _mainWindow,
                    ResizeMode = ResizeMode.NoResize,
                    WindowStyle = WindowStyle.ToolWindow
                };

                var panel = new StackPanel { Margin = new Thickness(10) };
                panel.Children.Add(new TextBlock
                {
                    Text = message,
                    Margin = new Thickness(0, 0, 0, 10)
                });

                var textBox = new TextBox
                {
                    Text = defaultValue,
                    Margin = new Thickness(0, 0, 0, 20)
                };
                panel.Children.Add(textBox);

                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right
                };

                string result = null;
                var okButton = new Button
                {
                    Content = "OK",
                    Width = 80,
                    Margin = new Thickness(0, 0, 10, 0),
                    IsDefault = true
                };
                okButton.Click += (s, e) =>
                {
                    result = textBox.Text;
                    dialog.Close();
                };

                var cancelButton = new Button
                {
                    Content = "Cancel",
                    Width = 80,
                    IsCancel = true
                };
                cancelButton.Click += (s, e) => dialog.Close();

                buttonPanel.Children.Add(okButton);
                buttonPanel.Children.Add(cancelButton);
                panel.Children.Add(buttonPanel);

                dialog.Content = panel;
                dialog.ShowDialog();

                return result;
            });
        }

        /// <summary>
        /// Internal method to show a notification with the specified color
        /// </summary>
        private void ShowNotificationInternal(string message, string title, Color color, int? duration = null)
        {
            _mainWindow.Dispatcher.Invoke(() =>
            {
                int actualDuration = duration ?? DEFAULT_NOTIFICATION_DURATION;

                // Create notification panel
                var notification = new Border
                {
                    Background = new SolidColorBrush(color),
                    CornerRadius = new CornerRadius(4),
                    Margin = new Thickness(10),
                    Padding = new Thickness(10),
                    MaxWidth = 300,
                    Opacity = 0
                };

                // Add shadow effect (if available in the framework)
                try
                {
                    notification.Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        ShadowDepth = 3,
                        BlurRadius = 5,
                        Opacity = 0.3,
                        Color = Colors.Black
                    };
                }
                catch (Exception)
                {
                    // Ignore if effect is not available
                }

                // Create notification content
                var panel = new StackPanel();
                panel.Children.Add(new TextBlock
                {
                    Text = title,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White
                });
                panel.Children.Add(new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Brushes.White,
                    Margin = new Thickness(0, 5, 0, 0)
                });

                notification.Child = panel;
                _notificationContainer.Children.Insert(0, notification);

                // Create entrance animation
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
                notification.BeginAnimation(UIElement.OpacityProperty, fadeIn);

                // Create exit animation and removal
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(actualDuration)
                };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
                    fadeOut.Completed += (s2, e2) =>
                    {
                        _notificationContainer.Children.Remove(notification);
                    };
                    notification.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                };
                timer.Start();
            });
        }
    }
}