namespace ThreadPilot.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using ThreadPilot.Models;

    public interface IProcessMonitorManagerService : IDisposable
    {
        event EventHandler<ProcessPowerPlanChangeEventArgs>? ProcessPowerPlanChanged;

        event EventHandler<ServiceStatusEventArgs>? ServiceStatusChanged;

        bool IsRunning { get; }

        string Status { get; }

        IEnumerable<ProcessModel> RunningAssociatedProcesses { get; }

        Task StartAsync();

        Task StopAsync();

        Task EvaluateCurrentProcessesAsync();

        Task ForceDefaultPowerPlanAsync();

        Task<PowerPlanModel?> GetCurrentActivePowerPlanAsync();

        Task RefreshConfigurationAsync();

        void UpdateSettings();
    }

    public class ProcessPowerPlanChangeEventArgs : EventArgs
    {
        public ProcessModel Process { get; }

        public ProcessPowerPlanAssociation Association { get; }

        public PowerPlanModel? PreviousPowerPlan { get; }

        public PowerPlanModel? NewPowerPlan { get; }

        public string Action { get; } // "ProcessStarted", "ProcessStopped", "DefaultRestored"

        public DateTime Timestamp { get; }

        public ProcessPowerPlanChangeEventArgs(
            ProcessModel process,
            ProcessPowerPlanAssociation association,
            PowerPlanModel? previousPowerPlan,
            PowerPlanModel? newPowerPlan,
            string action)
        {
            this.Process = process;
            this.Association = association;
            this.PreviousPowerPlan = previousPowerPlan;
            this.NewPowerPlan = newPowerPlan;
            this.Action = action;
            this.Timestamp = DateTime.Now;
        }
    }

    public class ServiceStatusEventArgs : EventArgs
    {
        public bool IsRunning { get; }

        public string Status { get; }

        public string? Details { get; }

        public Exception? Error { get; }

        public DateTime Timestamp { get; }

        public ServiceStatusEventArgs(bool isRunning, string status, string? details = null, Exception? error = null)
        {
            this.IsRunning = isRunning;
            this.Status = status;
            this.Details = details;
            this.Error = error;
            this.Timestamp = DateTime.Now;
        }
    }
}

