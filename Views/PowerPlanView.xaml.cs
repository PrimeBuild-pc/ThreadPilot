namespace ThreadPilot.Views
{
    using System.Windows.Controls;
    using ThreadPilot.ViewModels;

    public partial class PowerPlanView : System.Windows.Controls.UserControl
    {
        public PowerPlanView()
        {
            this.InitializeComponent();
        }

        public PowerPlanView(PowerPlanViewModel viewModel)
            : this()
        {
            this.DataContext = viewModel;
        }
    }
}
