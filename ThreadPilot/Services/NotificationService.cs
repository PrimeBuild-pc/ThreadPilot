using System;
using System.Windows;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Implementation of notification operations
    /// </summary>
    public class NotificationService : INotificationService
    {
        /// <summary>
        /// Shows a success notification
        /// </summary>
        /// <param name="message">The notification message</param>
        /// <param name="title">The notification title</param>
        public void ShowSuccess(string message, string title = "Success")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
        /// <summary>
        /// Shows an error notification
        /// </summary>
        /// <param name="message">The notification message</param>
        /// <param name="title">The notification title</param>
        public void ShowError(string message, string title = "Error")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        
        /// <summary>
        /// Shows a warning notification
        /// </summary>
        /// <param name="message">The notification message</param>
        /// <param name="title">The notification title</param>
        public void ShowWarning(string message, string title = "Warning")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        
        /// <summary>
        /// Shows an information notification
        /// </summary>
        /// <param name="message">The notification message</param>
        /// <param name="title">The notification title</param>
        public void ShowInfo(string message, string title = "Information")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
        /// <summary>
        /// Shows a confirmation dialog
        /// </summary>
        /// <param name="message">The dialog message</param>
        /// <param name="title">The dialog title</param>
        /// <returns>True if confirmed, false otherwise</returns>
        public bool ShowConfirmation(string message, string title = "Confirmation")
        {
            var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            return result == MessageBoxResult.Yes;
        }
        
        /// <summary>
        /// Shows a custom dialog
        /// </summary>
        /// <param name="content">The dialog content</param>
        /// <param name="title">The dialog title</param>
        /// <returns>True if confirmed, false otherwise</returns>
        public bool ShowDialog(object content, string title)
        {
            if (content is Window window)
            {
                window.Title = title;
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.Owner = Application.Current.MainWindow;
                var result = window.ShowDialog();
                return result.HasValue && result.Value;
            }
            
            // Create a new dialog window
            var dialog = new Window
            {
                Title = title,
                Content = content,
                Width = 400,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Application.Current.MainWindow,
                SizeToContent = SizeToContent.WidthAndHeight,
                ResizeMode = ResizeMode.NoResize
            };
            
            var dialogResult = dialog.ShowDialog();
            return dialogResult.HasValue && dialogResult.Value;
        }
        
        /// <summary>
        /// Shows a toast notification
        /// </summary>
        /// <param name="message">The toast message</param>
        /// <param name="duration">The duration in milliseconds</param>
        public void ShowToast(string message, int duration = 3000)
        {
            // For simplicity, use a message box for now
            // In a real implementation, this would show a non-intrusive toast notification
            MessageBox.Show(message, "Notification", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}