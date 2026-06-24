namespace ThreadPilot.Views
{
    using System.Windows;
    using System.Windows.Controls;
    using ThreadPilot.Services;
    using ThreadPilot.ViewModels;

    public partial class SettingsView : System.Windows.Controls.UserControl
    {
        public SettingsView()
        {
            this.InitializeComponent();
            this.Loaded += this.SettingsView_Loaded;
        }

        public SettingsView(SettingsViewModel viewModel)
            : this()
        {
            this.DataContext = viewModel;
        }

        private void SettingsView_Loaded(object sender, RoutedEventArgs e)
        {
            TaskSafety.FireAndForget(this.SettingsView_LoadedAsync(), _ =>
            {
                // Non-critical load refresh failures are handled by the view model.
            });
        }

        private async Task SettingsView_LoadedAsync()
        {
            if (this.DataContext is SettingsViewModel viewModel)
            {
                await viewModel.RefreshSettingsAsync();
            }
        }
    }
}

