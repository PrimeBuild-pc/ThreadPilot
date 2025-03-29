using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ThreadPilot.Services;
using ThreadPilot.ViewModels;

namespace ThreadPilot
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly SettingsService _settingsService;

        public MainWindow()
        {
            InitializeComponent();

            // Get services from DI
            _viewModel = App.GetService<MainViewModel>();
            _settingsService = App.GetService<SettingsService>();

            // Set data context
            DataContext = _viewModel;

            // Setup profiles menu in tray context menu
            SetupProfilesMenu();

            // Update theme icon based on current theme
            UpdateThemeIcon();

            // Load saved position and size
            if (_settingsService.WindowSettings != null)
            {
                if (_settingsService.WindowSettings.Width > 0)
                    Width = _settingsService.WindowSettings.Width;
                if (_settingsService.WindowSettings.Height > 0)
                    Height = _settingsService.WindowSettings.Height;
                if (_settingsService.WindowSettings.Left >= 0)
                    Left = _settingsService.WindowSettings.Left;
                if (_settingsService.WindowSettings.Top >= 0)
                    Top = _settingsService.WindowSettings.Top;
            }

            // Start minimized if configured
            if (_settingsService.StartMinimized)
            {
                WindowState = WindowState.Minimized;
                Hide();
            }
        }

        private void SetupProfilesMenu()
        {
            // Clear existing items
            ProfilesMenuItem.Items.Clear();

            // Add profiles from view model
            foreach (var profile in _viewModel.SavedProfiles)
            {
                var menuItem = new MenuItem
                {
                    Header = profile.Name,
                    Tag = profile
                };
                menuItem.Click += ProfileMenuItem_Click;
                ProfilesMenuItem.Items.Add(menuItem);
            }

            // Show "No profiles" if none exist
            if (ProfilesMenuItem.Items.Count == 0)
            {
                var noProfilesItem = new MenuItem
                {
                    Header = "No profiles available",
                    IsEnabled = false
                };
                ProfilesMenuItem.Items.Add(noProfilesItem);
            }
        }

        private void ProfileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is Models.Profile profile)
            {
                _viewModel.ApplyProfile(profile);
            }
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized && _settingsService.MinimizeToTray)
            {
                Hide();
            }
        }

        private void MinimizeToTrayButton_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            _settingsService.ToggleTheme();
            UpdateThemeIcon();
        }

        private void UpdateThemeIcon()
        {
            if (_settingsService.IsDarkTheme)
            {
                ThemeIcon.Data = (Geometry)FindResource("LightThemeIconGeometry");
                ThemeToggleButton.ToolTip = "Switch to light theme";
            }
            else
            {
                ThemeIcon.Data = (Geometry)FindResource("DarkThemeIconGeometry");
                ThemeToggleButton.ToolTip = "Switch to dark theme";
            }
        }

        private void TrayIcon_TrayLeftMouseDown(object sender, RoutedEventArgs e)
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void OpenMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Save window position and size
            _settingsService.WindowSettings = new Models.WindowSettings
            {
                Width = Width,
                Height = Height,
                Left = Left,
                Top = Top
            };
            _settingsService.SaveSettings();

            // If close to tray is enabled, just hide the window instead of closing the app
            if (_settingsService.CloseToTray)
            {
                e.Cancel = true;
                Hide();
            }
        }
    }
}
