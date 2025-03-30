using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ThreadPilot.Services;
using ThreadPilot.ViewModels;
using ThreadPilot.Views;

namespace ThreadPilot
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            // Set the initial view
            NavigateToPage("Dashboard");
        }

        private void NavigationMenu_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NavigationMenu.SelectedItem != null)
            {
                var selectedItem = (ListBoxItem)NavigationMenu.SelectedItem;
                string tag = selectedItem.Tag.ToString();
                
                NavigateToPage(tag);
            }
        }

        private void NavigateToPage(string pageName)
        {
            // Update the page title
            PageTitle.Text = pageName;
            
            // Clear the current content
            ContentFrame.Content = null;
            
            // Create and navigate to the selected page
            switch (pageName)
            {
                case "Dashboard":
                    ContentFrame.Content = new DashboardView();
                    break;
                case "Processes":
                    ContentFrame.Content = new ProcessesView();
                    break;
                case "PowerProfiles":
                    ContentFrame.Content = new PowerProfilesView();
                    break;
                case "ThreadOptimizer":
                    ContentFrame.Content = new ThreadOptimizerView();
                    break;
                case "Settings":
                    ContentFrame.Content = new SettingsView();
                    break;
                default:
                    ContentFrame.Content = new DashboardView();
                    break;
            }
        }

        private void ThemeToggle_Checked(object sender, RoutedEventArgs e)
        {
            ApplyTheme(true); // Dark theme
        }

        private void ThemeToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            ApplyTheme(false); // Light theme
        }

        private void ApplyTheme(bool isDarkTheme)
        {
            ResourceDictionary resources = Application.Current.Resources;
            
            // Update all brushes with the appropriate theme
            if (isDarkTheme)
            {
                // Dark theme
                resources["PrimaryBrush"] = resources["DarkPrimaryBrush"];
                resources["SecondaryBrush"] = resources["DarkSecondaryBrush"];
                resources["AccentBrush"] = resources["DarkAccentBrush"];
                resources["AppBackgroundBrush"] = resources["DarkBackgroundBrush"];
                resources["CardBackgroundBrush"] = resources["DarkCardBackgroundBrush"];
                resources["SidebarBackgroundBrush"] = resources["DarkSidebarBackgroundBrush"];
                resources["PrimaryTextBrush"] = resources["DarkPrimaryTextBrush"];
                resources["SecondaryTextBrush"] = resources["DarkSecondaryTextBrush"];
                resources["DisabledTextBrush"] = resources["DarkDisabledTextBrush"];
                resources["BorderBrush"] = resources["DarkBorderBrush"];
                resources["HoverBrush"] = resources["DarkHoverBrush"];
                resources["SeparatorBrush"] = resources["DarkSeparatorBrush"];
            }
            else
            {
                // Light theme
                resources["PrimaryBrush"] = resources["LightPrimaryBrush"];
                resources["SecondaryBrush"] = resources["LightSecondaryBrush"];
                resources["AccentBrush"] = resources["LightAccentBrush"];
                resources["AppBackgroundBrush"] = resources["LightBackgroundBrush"];
                resources["CardBackgroundBrush"] = resources["LightCardBackgroundBrush"];
                resources["SidebarBackgroundBrush"] = resources["LightSidebarBackgroundBrush"];
                resources["PrimaryTextBrush"] = resources["LightPrimaryTextBrush"];
                resources["SecondaryTextBrush"] = resources["LightSecondaryTextBrush"];
                resources["DisabledTextBrush"] = resources["LightDisabledTextBrush"];
                resources["BorderBrush"] = resources["LightBorderBrush"];
                resources["HoverBrush"] = resources["LightHoverBrush"];
                resources["SeparatorBrush"] = resources["LightSeparatorBrush"];
            }
            
            // Save the theme preference to settings
            // SettingsService.Current.SaveThemePreference(isDarkTheme);
        }
    }
}