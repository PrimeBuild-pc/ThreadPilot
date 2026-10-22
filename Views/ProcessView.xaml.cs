namespace ThreadPilot.Views
{
    using System.Windows.Controls;
    using System.Windows.Input;
    using ThreadPilot.Helpers;
    using ThreadPilot.ViewModels;

    public partial class ProcessView : System.Windows.Controls.UserControl
    {
        public ProcessView()
        {
            this.InitializeComponent();
            this.DataContext = ServiceProviderExtensions.GetService<ProcessViewModel>();

            var state = Services.UiStateStore.Current;
            this.ShowPidColumn.IsChecked = state.ShowPidColumn;
            this.ShowWindowColumn.IsChecked = state.ShowWindowTitleColumn;
            this.ShowHasWindowColumn.IsChecked = state.ShowHasWindowColumn;
            System.Windows.RoutedEventHandler save = (_, _) => Services.UiStateStore.Update(s => s with
            {
                ShowPidColumn = this.ShowPidColumn.IsChecked,
                ShowWindowTitleColumn = this.ShowWindowColumn.IsChecked,
                ShowHasWindowColumn = this.ShowHasWindowColumn.IsChecked,
            });
            foreach (var item in new[] { this.ShowPidColumn, this.ShowWindowColumn, this.ShowHasWindowColumn })
            {
                item.Checked += save;
                item.Unchecked += save;
            }
        }

        private const double DetailMinWidth = 400;

        private void ProcessWorkspace_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
        {
            var wide = e.NewSize.Width >= 960;

            // Wide: the table is only as wide as its visible columns and the details panel takes the rest.
            // Narrow: the table fills the width (Priority stretches) and the details go below it.
            var star = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star);
            this.TableColumn.Width = wide ? System.Windows.GridLength.Auto : star;
            this.DetailColumn.Width = wide ? star : new System.Windows.GridLength(0);
            this.ProcessTable.MaxWidth = wide ? e.NewSize.Width - DetailMinWidth : double.PositiveInfinity;
            this.PriorityColumn.Width = wide ? new DataGridLength(120) : new DataGridLength(1, DataGridLengthUnitType.Star);
            this.SelectProcessHint.HorizontalAlignment = wide ? System.Windows.HorizontalAlignment.Center : System.Windows.HorizontalAlignment.Stretch;
            this.SelectProcessHint.VerticalAlignment = wide ? System.Windows.VerticalAlignment.Center : System.Windows.VerticalAlignment.Top;
            Grid.SetRow(this.ProcessDetails, wide ? 1 : 2);
            Grid.SetColumn(this.ProcessDetails, wide ? 1 : 0);
            this.ProcessDetails.MaxHeight = wide ? double.PositiveInfinity : Math.Max(160, e.NewSize.Height * 0.48);
            this.ProcessDetails.Padding = wide ? new System.Windows.Thickness(12, 0, 12, 0) : new System.Windows.Thickness(0, 12, 12, 0);

            // Below ~820 px the buttons would squeeze the search box to a few letters: give it its own row.
            var stacked = e.NewSize.Width < 820;
            Grid.SetRow(this.ToolbarActions, stacked ? 1 : 0);
            Grid.SetColumn(this.ToolbarActions, stacked ? 0 : 1);
            this.ToolbarActions.Margin = stacked ? new System.Windows.Thickness(-8, 8, 0, 0) : default;
        }

        private void ProcessDetails_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
        {
            // A wide details panel puts Priority and CPU cores side by side instead of stretching every picker.
            var twoColumns = this.DetailColumn.Width.IsStar && e.NewSize.Width >= 880;
            this.DetailSecondColumn.Width = twoColumns ? new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) : new System.Windows.GridLength(0);
            Grid.SetRow(this.CpuCoresCard, twoColumns ? 1 : 2);
            Grid.SetColumn(this.CpuCoresCard, twoColumns ? 1 : 0);
            this.PriorityCard.Margin = new System.Windows.Thickness(0, 0, twoColumns ? 10 : 0, 10);
        }

        private void ProcessRow_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DataGridRow row)
            {
                return;
            }

            row.IsSelected = true;
            row.Focus();
        }
    }
}
