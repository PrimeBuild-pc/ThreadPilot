namespace ThreadPilot.Models
{
    using System;
    using System.Collections.Generic;
    using CommunityToolkit.Mvvm.ComponentModel;

    public partial class ProcessMonitorConfiguration : ObservableObject
    {
        [ObservableProperty]
        private string defaultPowerPlanGuid = string.Empty;

        [ObservableProperty]
        private string defaultPowerPlanName = string.Empty;

        [ObservableProperty]
        private bool isEventBasedMonitoringEnabled = true;

        [ObservableProperty]
        private bool isFallbackPollingEnabled = true;

        [ObservableProperty]
        private int pollingIntervalSeconds = 5;

        [ObservableProperty]
        private bool preventDuplicatePowerPlanChanges = true;

        [ObservableProperty]
        private int powerPlanChangeDelayMs = 250;

        [ObservableProperty]
        private bool enableLogging = true;

        [ObservableProperty]
        private List<ProcessPowerPlanAssociation> associations = new();

        [ObservableProperty]
        private DateTime lastSavedDate = DateTime.Now;

        [ObservableProperty]
        private string configurationVersion = "1.0";

        public ProcessMonitorConfiguration()
        {
            this.Associations = new List<ProcessPowerPlanAssociation>();
        }

        public IEnumerable<ProcessPowerPlanAssociation> GetEnabledAssociations()
        {
            return this.Associations
                .Where(a => a.IsEnabled)
                .OrderByDescending(a => a.Priority)
                .ThenBy(a => a.ExecutableName);
        }

        public ProcessPowerPlanAssociation? FindMatchingAssociation(ProcessModel process)
        {
            return this.GetEnabledAssociations()
                .FirstOrDefault(a => a.MatchesProcess(process));
        }

        public ProcessPowerPlanAssociation? FindAssociationByExecutable(string executableName)
        {
            return this.Associations
                .FirstOrDefault(a => a.MatchesExecutable(executableName));
        }

        public void AddOrUpdateAssociation(ProcessPowerPlanAssociation association)
        {
            var existing = this.Associations.FirstOrDefault(a => a.Id == association.Id);
            if (existing != null)
            {
                var index = this.Associations.IndexOf(existing);
                this.Associations[index] = association;
            }
            else
            {
                this.Associations.Add(association);
            }
            this.LastSavedDate = DateTime.Now;
        }

        public bool RemoveAssociation(string associationId)
        {
            var association = this.Associations.FirstOrDefault(a => a.Id == associationId);
            if (association != null)
            {
                this.Associations.Remove(association);
                this.LastSavedDate = DateTime.Now;
                return true;
            }
            return false;
        }

        public List<string> Validate()
        {
            var errors = new List<string>();

            if (this.PollingIntervalSeconds < 1)
            {
                errors.Add("Polling interval must be at least 1 second");
            }

            if (this.PowerPlanChangeDelayMs < 0)
            {
                errors.Add("Power plan change delay cannot be negative");
            }

            // Check for duplicate associations
            var duplicates = this.Associations
                .GroupBy(a => new { a.ExecutableName, a.MatchByPath })
                .Where(g => g.Count() > 1)
                .Select(g => g.Key.ExecutableName);

            foreach (var duplicate in duplicates)
            {
                errors.Add($"Duplicate association found for executable: {duplicate}");
            }

            return errors;
        }
    }
}

