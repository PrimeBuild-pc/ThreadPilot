namespace ThreadPilot.Views
{
    using System.Windows.Controls;
    using ThreadPilot.Services;
    using ThreadPilot.ViewModels;

    public partial class SystemTweaksView : System.Windows.Controls.UserControl
    {
        public SystemTweaksView()
        {
            this.InitializeComponent();
        }

        private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            TaskSafety.FireAndForget(this.UserControl_LoadedAsync(), _ =>
            {
                // Ignore non-fatal loading errors to keep the view responsive.
            });
        }

        private async Task UserControl_LoadedAsync()
        {
            if (this.DataContext is SystemTweaksViewModel viewModel)
            {
                await viewModel.LoadCommand.ExecuteAsync(null);
            }
        }
    }
}

