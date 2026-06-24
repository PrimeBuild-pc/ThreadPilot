namespace ThreadPilot.Views
{
    using System;
    using System.ComponentModel;
    using System.Windows;
    using ThreadPilot.ViewModels;

    public partial class SettingsWindow : Window
    {
        private readonly SettingsViewModel viewModel;
        private bool isClosingAfterUnsavedPrompt;

        public SettingsWindow(SettingsViewModel viewModel)
        {
            this.InitializeComponent();

            this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            this.SettingsViewControl.DataContext = this.viewModel;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // Check for unsaved changes
            if (!this.isClosingAfterUnsavedPrompt && !this.viewModel.CanClose())
            {
                e.Cancel = true;
                this.UnsavedSettingsOverlay.Visibility = Visibility.Visible;
                return;
            }

            base.OnClosing(e);
        }

        private async void UnsavedSettingsSave_Click(object sender, RoutedEventArgs e)
        {
            var saved = await this.viewModel.SaveIfDirtyAsync();
            if (saved)
            {
                this.CloseAfterUnsavedPrompt();
            }
        }

        private async void UnsavedSettingsDiscard_Click(object sender, RoutedEventArgs e)
        {
            await this.viewModel.DiscardPendingChangesAsync();
            this.CloseAfterUnsavedPrompt();
        }

        private void UnsavedSettingsCancel_Click(object sender, RoutedEventArgs e)
        {
            this.UnsavedSettingsOverlay.Visibility = Visibility.Collapsed;
        }

        private void CloseAfterUnsavedPrompt()
        {
            this.isClosingAfterUnsavedPrompt = true;
            this.UnsavedSettingsOverlay.Visibility = Visibility.Collapsed;
            this.Close();
        }
    }
}

