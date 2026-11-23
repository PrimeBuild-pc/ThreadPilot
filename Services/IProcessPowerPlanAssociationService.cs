namespace ThreadPilot.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using ThreadPilot.Models;

    public interface IProcessPowerPlanAssociationService
    {
        event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;

        ProcessMonitorConfiguration Configuration { get; }

        Task<bool> LoadConfigurationAsync();

        /// <summary>Saves the power plan monitoring options.</summary>
        /// <param name="preventDuplicateChanges">Whether to suppress repeated power plan changes.</param>
        /// <param name="delayMs">The delay before reevaluating the active power plan, in milliseconds.</param>
        /// <returns>Whether the options were saved.</returns>
        Task<bool> SaveMonitoringSettingsAsync(bool preventDuplicateChanges, int delayMs);

        Task<IEnumerable<ProcessPowerPlanAssociation>> GetAssociationsAsync();

        Task<IEnumerable<ProcessPowerPlanAssociation>> GetEnabledAssociationsAsync();

        Task<bool> AddAssociationAsync(ProcessPowerPlanAssociation association);

        Task<bool> UpdateAssociationAsync(ProcessPowerPlanAssociation association);

        Task<bool> RemoveAssociationAsync(string associationId);

        Task<ProcessPowerPlanAssociation?> FindMatchingAssociationAsync(ProcessModel process);

        Task<ProcessPowerPlanAssociation?> FindAssociationByExecutableAsync(string executableName);

        Task<bool> SetDefaultPowerPlanAsync(string powerPlanGuid, string powerPlanName);

        Task<(string Guid, string Name)> GetDefaultPowerPlanAsync();

        Task<IEnumerable<string>> ValidateConfigurationAsync();

        Task<bool> ResetConfigurationAsync();

        Task<bool> ExportConfigurationAsync(string filePath);

        Task<bool> ImportConfigurationAsync(string filePath);

        Task<bool> ReplaceConfigurationAsync(ProcessMonitorConfiguration configuration);
    }

    public class ConfigurationChangedEventArgs : EventArgs
    {
        public string ChangeType { get; }

        public ProcessPowerPlanAssociation? Association { get; }

        public string? Details { get; }

        public ConfigurationChangedEventArgs(string changeType, ProcessPowerPlanAssociation? association = null, string? details = null)
        {
            this.ChangeType = changeType;
            this.Association = association;
            this.Details = details;
        }
    }
}

