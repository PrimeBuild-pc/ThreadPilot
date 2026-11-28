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
            this.StoreCleanupRow.Visibility = StoreDistribution.IsStoreBuild ? Visibility.Visible : Visibility.Collapsed;
            this.Loaded += this.SettingsView_Loaded;
        }

        public SettingsView(SettingsViewModel viewModel)
            : this()
        {
            this.DataContext = viewModel;
        }

        private async void StoreCleanup_Click(object sender, RoutedEventArgs e)
        {
            if (System.Windows.Application.Current is not App app
                || Window.GetWindow(this) is not MainWindow window
                || !UninstallCleanup.TryChooseOptions("Clean up ThreadPilot before uninstalling", out var purge, out var restore))
            {
                return;
            }

            app.ScheduleStoreCleanup(purge, restore);
            await window.ShutdownForUpdateAsync();
        }

        internal void ShowUpdates()
        {
            this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,
                new Action(() =>
                {
                    var position = this.UpdatesHeading.TranslatePoint(new System.Windows.Point(0, 0), this.SettingsScroll);
                    this.SettingsScroll.ScrollToVerticalOffset(this.SettingsScroll.VerticalOffset + position.Y);
                }));
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

