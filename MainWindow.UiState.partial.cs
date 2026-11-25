namespace ThreadPilot
{
    using System.Windows;
    using ThreadPilot.Services;

    public partial class MainWindow
    {
        // Puts the menu and the window back the way the user left them. A position that is now
        // off-screen (unplugged monitor) is pulled back by EnsureDashboardVisibleOnScreen at startup.
        private void RestoreUiState()
        {
            var state = UiStateStore.Current;
            this.RootNavigation.IsPaneOpen = state.NavigationPaneOpen;
            this.RootNavigation.PaneOpened += (_, _) => UiStateStore.Update(s => s with { NavigationPaneOpen = true });
            this.RootNavigation.PaneClosed += (_, _) => UiStateStore.Update(s => s with { NavigationPaneOpen = false });

            if (state.WindowWidth is > 0 && state.WindowHeight is > 0 && state.WindowLeft.HasValue && state.WindowTop.HasValue)
            {
                this.WindowStartupLocation = WindowStartupLocation.Manual;
                this.Left = state.WindowLeft.Value;
                this.Top = state.WindowTop.Value;
                this.Width = state.WindowWidth.Value;
                this.Height = UpgradeLegacyDefaultHeight(state.WindowWidth.Value, state.WindowHeight.Value);
            }

            if (state.WindowMaximized)
            {
                this.WindowState = WindowState.Maximized;
            }
        }

        // 1280 x 864 was the default size before the editors on the Rules page got taller: a saved size that is
        // exactly that was never resized by the user, so it gets the new default height. Placement correction still
        // shrinks it to the screen's working area.
        internal static double UpgradeLegacyDefaultHeight(double width, double height) =>
            width == 1280 && height == 864 ? 960 : height;

        private void SaveWindowPlacement()
        {
            // RestoreBounds is the normal-size rectangle even while maximized or minimized.
            var bounds = this.WindowState == WindowState.Normal
                ? new Rect(this.Left, this.Top, this.ActualWidth, this.ActualHeight)
                : this.RestoreBounds;
            if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0 || double.IsNaN(bounds.Left))
            {
                return;
            }

            UiStateStore.Update(s => s with
            {
                WindowLeft = bounds.Left,
                WindowTop = bounds.Top,
                WindowWidth = bounds.Width,
                WindowHeight = bounds.Height,
                WindowMaximized = this.WindowState == WindowState.Maximized,
            });
        }
    }
}
