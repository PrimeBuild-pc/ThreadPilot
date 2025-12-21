using System.Windows;
using System.Windows.Controls;
using ThreadPilot.ViewModels;

namespace ThreadPilot.Views
{
    /// <summary>
    /// Interaction logic for SettingsView.xaml
    /// </summary>
    public partial class SettingsView : System.Windows.Controls.UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
            Loaded += SettingsView_Loaded;
        }

        public SettingsView(SettingsViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }

        private async void SettingsView_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel viewModel)
            {
                await viewModel.RefreshSettingsAsync();
            }
        }
    }
}
