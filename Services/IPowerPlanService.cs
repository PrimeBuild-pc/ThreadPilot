namespace ThreadPilot.Services
{
    using System;
    using System.Collections.ObjectModel;
    using System.Threading.Tasks;
    using ThreadPilot.Models;

    public interface IPowerPlanService
    {
        event EventHandler<PowerPlanChangedEventArgs>? PowerPlanChanged;

        Task<ObservableCollection<PowerPlanModel>> GetPowerPlansAsync();

        Task<ObservableCollection<PowerPlanModel>> GetCustomPowerPlansAsync();

        Task<bool> SetActivePowerPlan(PowerPlanModel powerPlan);

        Task<PowerPlanModel?> GetActivePowerPlan();

        Task<bool> ImportCustomPowerPlan(string filePath);

        Task<bool> AddCustomPowerPlanFileAsync(string filePath);

        Task<bool> DeletePowerPlanAsync(string powerPlanGuid);

        Task<bool> SetActivePowerPlanByGuidAsync(string powerPlanGuid, bool preventDuplicateChanges = true);

        Task<string?> GetActivePowerPlanGuidAsync();

        Task<bool> PowerPlanExistsAsync(string powerPlanGuid);

        Task<PowerPlanModel?> GetPowerPlanByGuidAsync(string powerPlanGuid);

        Task<bool> IsPowerPlanChangeNeededAsync(string targetPowerPlanGuid);
    }

    public class PowerPlanChangedEventArgs : EventArgs
    {
        public PowerPlanModel? PreviousPowerPlan { get; }

        public PowerPlanModel? NewPowerPlan { get; }

        public DateTime Timestamp { get; }

        public string? Reason { get; }

        public PowerPlanChangedEventArgs(PowerPlanModel? previousPowerPlan, PowerPlanModel? newPowerPlan, string? reason = null)
        {
            this.PreviousPowerPlan = previousPowerPlan;
            this.NewPowerPlan = newPowerPlan;
            this.Timestamp = DateTime.Now;
            this.Reason = reason;
        }
    }
}
