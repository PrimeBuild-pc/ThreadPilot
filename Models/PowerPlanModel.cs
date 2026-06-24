namespace ThreadPilot.Models
{
    using CommunityToolkit.Mvvm.ComponentModel;

    public partial class PowerPlanModel : ObservableObject
    {
        [ObservableProperty]
        private string guid = string.Empty;

        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private string description = string.Empty;

        [ObservableProperty]
        private bool isActive;

        [ObservableProperty]
        private bool isCustomPlan;

        [ObservableProperty]
        private string filePath = string.Empty;
    }
}
