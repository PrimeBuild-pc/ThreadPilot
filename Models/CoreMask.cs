using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ThreadPilot.Models
{
    /// <summary>
    /// Represents a reusable CPU core affinity mask
    /// Based on CPUSetSetter's LogicalProcessorMask
    /// </summary>
    public partial class CoreMask : ObservableObject
    {
        [ObservableProperty]
        private string id = Guid.NewGuid().ToString();

        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private string description = string.Empty;

        /// <summary>
        /// Array of boolean values, one per logical core
        /// </summary>
        public ObservableCollection<bool> BoolMask { get; set; } = new();

        [ObservableProperty]
        private bool isDefault = false;

        [ObservableProperty]
        private bool isEnabled = true;

        [ObservableProperty]
        private DateTime createdAt = DateTime.UtcNow;

        [ObservableProperty]
        private DateTime updatedAt = DateTime.UtcNow;

        /// <summary>
        /// Special mask that allows all cores (no restrictions)
        /// </summary>
        public bool IsNoMask => BoolMask.All(b => b);

        /// <summary>
        /// Gets the count of selected cores
        /// </summary>
        public int SelectedCoreCount => BoolMask.Count(b => b);

        /// <summary>
        /// Converts the boolean mask to a processor affinity value
        /// </summary>
        public long ToProcessorAffinity()
        {
            long affinity = 0;
            for (int i = 0; i < BoolMask.Count; i++)
            {
                if (BoolMask[i])
                    affinity |= (1L << i);
            }
            return affinity;
        }

        /// <summary>
        /// Creates a CoreMask from a processor affinity value
        /// </summary>
        public static CoreMask FromProcessorAffinity(long affinity, int coreCount, string name = "Custom")
        {
            var mask = new CoreMask { Name = name };
            for (int i = 0; i < coreCount; i++)
            {
                mask.BoolMask.Add(((affinity >> i) & 1) == 1);
            }
            return mask;
        }

        /// <summary>
        /// Creates a mask with all cores enabled
        /// </summary>
        public static CoreMask CreateAllCoresMask(int coreCount)
        {
            var mask = new CoreMask
            {
                Name = "All Cores",
                Description = "Use all available CPU cores",
                IsDefault = true
            };

            for (int i = 0; i < coreCount; i++)
                mask.BoolMask.Add(true);

            return mask;
        }

        /// <summary>
        /// Creates a mask with no cores (empty mask, for deletion purposes)
        /// </summary>
        public static CoreMask CreateNoMask()
        {
            return new CoreMask
            {
                Name = "No Restriction",
                Description = "Process can use all cores",
                IsDefault = false
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
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            foreach (var bit in BoolMask)
                cloned.BoolMask.Add(bit);

            return cloned;
        }

        public override string ToString()
        {
            return $"{Name} ({SelectedCoreCount}/{BoolMask.Count} cores)";
        }
    }
}
