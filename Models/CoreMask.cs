namespace ThreadPilot.Models
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using CommunityToolkit.Mvvm.ComponentModel;

    public partial class CoreMask : ObservableObject
    {
        [ObservableProperty]
        private string id = Guid.NewGuid().ToString();

        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private string description = string.Empty;

        public ObservableCollection<bool> BoolMask { get; set; } = new();

        public int ProfileSchemaVersion { get; set; } = CpuAffinityProfileSchemaVersions.Legacy;

        public CpuSelection? CpuSelection { get; set; }

        public CpuSelectionMigrationMetadata? CpuSelectionMigration { get; set; }

        [ObservableProperty]
        private bool isDefault = false;

        [ObservableProperty]
        private bool isEnabled = true;

        [ObservableProperty]
        private DateTime createdAt = DateTime.UtcNow;

        [ObservableProperty]
        private DateTime updatedAt = DateTime.UtcNow;

        public bool IsNoMask => this.BoolMask.All(b => b);

        public int SelectedCoreCount => this.BoolMask.Count(b => b);

        public long ToProcessorAffinity()
        {
            long affinity = 0;
            for (int i = 0; i < this.BoolMask.Count; i++)
            {
                if (this.BoolMask[i])
                {
                    affinity |= 1L << i;
                }
            }
            return affinity;
        }

        public static CoreMask FromProcessorAffinity(long affinity, int coreCount, string name = "Custom")
        {
            var mask = new CoreMask { Name = name };
            for (int i = 0; i < coreCount; i++)
            {
                mask.BoolMask.Add(((affinity >> i) & 1) == 1);
            }
            return mask;
        }

        public static CoreMask CreateAllCoresMask(int coreCount)
        {
            var mask = new CoreMask
            {
                Name = "All Cores",
                Description = "Use all available CPU cores",
                IsDefault = true,
            };

            for (int i = 0; i < coreCount; i++)
            {
                mask.BoolMask.Add(true);
            }

            return mask;
        }

        public static CoreMask CreateNoMask()
        {
            return new CoreMask
            {
                Name = "No Restriction",
                Description = "Process can use all cores",
                IsDefault = false,
            };
        }

        public CoreMask Clone()
        {
            var cloned = new CoreMask
            {
                Id = Guid.NewGuid().ToString(),
                Name = this.Name + " (Copy)",
                Description = this.Description,
                IsEnabled = this.IsEnabled,
                IsDefault = false,
                ProfileSchemaVersion = this.ProfileSchemaVersion,
                CpuSelection = this.CpuSelection,
                CpuSelectionMigration = this.CpuSelectionMigration,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            foreach (var bit in this.BoolMask)
            {
                cloned.BoolMask.Add(bit);
            }

            return cloned;
        }

        public override string ToString()
        {
            return $"{this.Name} ({this.SelectedCoreCount}/{this.BoolMask.Count} cores)";
        }
    }
}
