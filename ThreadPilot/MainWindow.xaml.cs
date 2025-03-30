using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ThreadPilot.Services;
using ThreadPilot.Views;

namespace ThreadPilot
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private DispatcherTimer _notificationTimer;
        private NotificationService _notificationService;

        public MainWindow()
        {
            InitializeComponent();
            
            _notificationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _notificationTimer.Tick += NotificationTimer_Tick;
            
            // Register notification service
            _notificationService = new NotificationService(ShowNotification);
            ServiceLocator.RegisterService<INotificationService>(_notificationService);
            
            // Set initial page
            LoadPageContent("Dashboard");
        }

        private void NavigationButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radioButton)
            {
                // Extract the page name from RadioButton name (remove "Nav" suffix)
                string pageName = radioButton.Name.Replace("Nav", "");
                LoadPageContent(pageName);
            }
        }

        private void LoadPageContent(string pageName)
        {
            UserControl pageContent = null;
            
            // Update the page title
            PageTitle.Text = pageName;
            
            // Create the appropriate user control based on the page name
            switch (pageName)
            {
                case "Dashboard":
                    pageContent = new DashboardView();
                    break;
                case "Processes":
                    pageContent = new ProcessesView();
                    break;
                case "AffinityOptimization":
                    pageContent = new AffinityOptimizationView();
                    break;
                case "PowerProfiles":
                    pageContent = new PowerProfilesView();
                    break;
                case "AutomationRules":
                    pageContent = new AutomationRulesView();
                    break;
                case "Settings":
                    pageContent = new SettingsView();
                    break;
                default:
                    pageContent = new DashboardView();
                    break;
            }
            
            // Set the content
            PageContent.Content = pageContent;
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            // Show help window or dialog
            MessageBox.Show("ThreadPilot Help\n\nThis application allows you to manage and optimize CPU thread allocation, process affinity, and power profiles. For more detailed help on specific features, please refer to the documentation.", 
                "ThreadPilot Help", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
        // Called by the NotificationService
        public void ShowNotification(string message, bool isError = false)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Update notification text
                NotificationText.Text = message;
                
                // Set color based on notification type
                NotificationPanel.Background = isError 
                    ? System.Windows.Media.Brushes.Firebrick 
                    : System.Windows.Media.Brushes.ForestGreen;
                
                // Show the notification panel
                NotificationPanel.Visibility = Visibility.Visible;
                
                // Start the timer to hide notification
                _notificationTimer.Stop();
                _notificationTimer.Start();
                
                // Animate notification
                DoubleAnimation animation = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(300)
                };
                NotificationPanel.BeginAnimation(UIElement.OpacityProperty, animation);
            });
        }
        
        private void NotificationTimer_Tick(object sender, EventArgs e)
        {
            // Stop the timer
            _notificationTimer.Stop();
            
            // Fade out animation
            DoubleAnimation animation = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(300)
            };
            animation.Completed += (s, _) => NotificationPanel.Visibility = Visibility.Collapsed;
            NotificationPanel.BeginAnimation(UIElement.OpacityProperty, animation);
        }
        
        private void CloseNotification_Click(object sender, RoutedEventArgs e)
        {
            // Stop the timer if it's running
            _notificationTimer.Stop();
            
            // Immediately hide the notification panel
            NotificationPanel.Visibility = Visibility.Collapsed;
        }
    }
}