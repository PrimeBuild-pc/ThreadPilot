/*
 * ThreadPilot - Advanced Windows Process and Power Plan Manager
 * Copyright (C) 2025 Prime Build
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, version 3 only.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
namespace ThreadPilot.ViewModels
{
    using System;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading.Tasks;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using Microsoft.Extensions.Logging;
    using ThreadPilot.Models;
    using ThreadPilot.Services;
    using MessageBox = System.Windows.MessageBox;
    using MessageBoxButton = System.Windows.MessageBoxButton;
    using MessageBoxImage = System.Windows.MessageBoxImage;
    using MessageBoxResult = System.Windows.MessageBoxResult;

    /// <summary>
    /// Wrapper for individual core bit in the mask, similar to CPUSetSetter's MaskBitViewModel.
    /// </summary>
    public partial class CoreBitViewModel : ObservableObject
    {
        private readonly ObservableCollection<bool> boolMask;
        private readonly int index;

        public int Index => this.index;

        public bool IsSelected
        {
            get => this.index < this.boolMask.Count && this.boolMask[this.index];
            set
            {
                if (this.index < this.boolMask.Count && this.boolMask[this.index] != value)
                {
                    this.boolMask[this.index] = value;
                    this.OnPropertyChanged();
                }
            }
        }

        public CoreBitViewModel(ObservableCollection<bool> boolMask, int index)
        {
            this.boolMask = boolMask;
            this.index = index;
        }
    }

    /// <summary>
    /// ViewModel for managing CPU core affinity masks
    /// Based on CPUSetSetter's MasksTabViewModel.
    /// </summary>
    public partial class MasksViewModel : ObservableObject
    {
        private readonly ICoreMaskService coreMaskService;
        private readonly ICpuTopologyService cpuTopologyService;
        private readonly ILogger<MasksViewModel> logger;

        [ObservableProperty]
        private ObservableCollection<CoreMask> coreMasks = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanDeleteMask))]
        [NotifyPropertyChangedFor(nameof(CanDuplicateMask))]
        private CoreMask? selectedCoreMask;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        [ObservableProperty]
        private ObservableCollection<CoreBitViewModel> coreBits = new();

        /// <summary>
        /// Gets a value indicating whether can delete if: mask selected, not "All Cores" baseline, not actively applied to processes
        /// Note: The actual validation happens in DeleteMask command with proper async checks.
        /// </summary>
        public bool CanDeleteMask => this.SelectedCoreMask != null && this.SelectedCoreMask.Name != "All Cores";

        public bool CanDuplicateMask => this.SelectedCoreMask != null;

        public MasksViewModel(
            ICoreMaskService coreMaskService,
            ICpuTopologyService cpuTopologyService,
            ILogger<MasksViewModel> logger)
        {
            this.coreMaskService = coreMaskService ?? throw new ArgumentNullException(nameof(coreMaskService));
            this.cpuTopologyService = cpuTopologyService ?? throw new ArgumentNullException(nameof(cpuTopologyService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Initialize
            _ = this.InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                await this.coreMaskService.InitializeAsync();

                // Link to the service's collection
                this.CoreMasks = this.coreMaskService.AvailableMasks;

                // Select the default mask
                this.SelectedCoreMask = this.coreMaskService.DefaultMask;

                this.logger.LogInformation("MasksViewModel initialized with {Count} masks", this.CoreMasks.Count);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to initialize MasksViewModel");
                this.StatusMessage = $"Error initializing masks: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task CreateMask()
        {
            try
            {
                var coreCount = Environment.ProcessorCount;
                var newMask = new CoreMask
                {
                    Name = "New Mask",
                    Description = $"Created at {DateTime.Now:HH:mm:ss}",
                };

                // Initialize with all cores enabled
                for (int i = 0; i < coreCount; i++)
                {
                    newMask.BoolMask.Add(true);
                }

                this.CoreMasks.Add(newMask);
                this.SelectedCoreMask = newMask;

                await this.coreMaskService.SaveMasksAsync();

                this.StatusMessage = $"Created mask '{newMask.Name}'";
                this.logger.LogInformation("Created new mask '{Name}'", newMask.Name);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to create mask");
                this.StatusMessage = $"Error creating mask: {ex.Message}";
                MessageBox.Show($"Failed to create mask: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand(CanExecute = nameof(CanDeleteMask))]
        private async Task DeleteMask()
        {
            if (this.SelectedCoreMask == null)
            {
                return;
            }

            try
            {
                var maskId = this.SelectedCoreMask.Id;
                var maskName = this.SelectedCoreMask.Name;

                // 1. Check if mask is the "All Cores" baseline (cannot be deleted)
                if (maskName == "All Cores")
                {
                    MessageBox.Show(
                        "The 'All Cores' mask is the baseline mask and cannot be deleted.\n\n" +
                        "This mask is required as the default fallback for all processes.",
                        "Cannot Delete Baseline Mask",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // 2. Check if mask is actively applied to running processes
                bool isActivelyApplied = await this.coreMaskService.IsMaskActivelyAppliedAsync(maskId);
                if (isActivelyApplied)
                {
                    MessageBox.Show(
                        $"The mask '{maskName}' is currently applied to one or more running processes.\n\n" +
                        "You must change the mask on those processes before deleting this mask.\n\n" +
                        "Go to the Processes tab to change the affinity of the affected processes.",
                        "Mask In Use",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // 3. Check if mask is referenced by profiles/rules (not actively applied)
                var referencingProfiles = (await this.coreMaskService.GetProfilesReferencingMaskAsync(maskId)).ToList();
                if (referencingProfiles.Any())
                {
                    var profileList = string.Join("\n  - ", referencingProfiles.Take(10));
                    if (referencingProfiles.Count > 10)
                    {
                        profileList += $"\n  ... and {referencingProfiles.Count - 10} more";
                    }

                    var result = MessageBox.Show(
                        $"The mask '{maskName}' is referenced by the following profiles/rules:\n\n" +
                        $"  - {profileList}\n\n" +
                        "If you delete this mask, these profiles will be updated to use the 'All Cores' mask instead.\n\n" +
                        "Do you want to continue?",
                        "Mask Referenced by Profiles",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result != MessageBoxResult.Yes)
                    {
                        return;
                    }

                    // Update all referencing profiles to use "All Cores"
                    await this.coreMaskService.UpdateProfilesToDefaultMaskAsync(maskId);
                    this.StatusMessage = $"Updated {referencingProfiles.Count} profile(s) to use 'All Cores' mask";
                }
                else
                {
                    // Simple confirmation
                    var result = MessageBox.Show(
                        $"Are you sure you want to delete the mask '{maskName}'?",
                        "Confirm Delete",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }

                // 4. Delete the mask
                await this.coreMaskService.DeleteMaskAsync(maskId);
                this.SelectedCoreMask = this.CoreMasks.FirstOrDefault();

                this.StatusMessage = $"Deleted mask '{maskName}'";
                this.logger.LogInformation("Deleted mask '{Name}'", maskName);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to delete mask");
                this.StatusMessage = $"Error deleting mask: {ex.Message}";
                MessageBox.Show($"Failed to delete mask: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand(CanExecute = nameof(CanDuplicateMask))]
        private async Task DuplicateMask()
        {
            if (this.SelectedCoreMask == null)
            {
                return;
            }

            try
            {
                var cloned = this.SelectedCoreMask.Clone();
                this.CoreMasks.Add(cloned);
                this.SelectedCoreMask = cloned;

                await this.coreMaskService.SaveMasksAsync();

                this.StatusMessage = $"Duplicated mask '{cloned.Name}'";
                this.logger.LogInformation("Duplicated mask to '{Name}'", cloned.Name);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to duplicate mask");
                this.StatusMessage = $"Error duplicating mask: {ex.Message}";
                MessageBox.Show($"Failed to duplicate mask: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SaveCurrentMaskAsync()
        {
            if (this.SelectedCoreMask == null)
            {
                return;
            }

            try
            {
                await this.coreMaskService.UpdateMaskAsync(this.SelectedCoreMask);
                this.logger.LogDebug("Saved mask '{Name}'", this.SelectedCoreMask.Name);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to save mask");
            }
        }

        partial void OnSelectedCoreMaskChanged(CoreMask? oldValue, CoreMask? newValue)
        {
            // Unsubscribe from old mask
            if (oldValue != null)
            {
                oldValue.PropertyChanged -= OnMaskPropertyChanged;
                oldValue.BoolMask.CollectionChanged -= OnBoolMaskCollectionChanged;
            }

            // Subscribe to new mask
            if (newValue != null)
            {
                newValue.PropertyChanged += OnMaskPropertyChanged;
                newValue.BoolMask.CollectionChanged += OnBoolMaskCollectionChanged;

                // Rebuild CoreBits collection to match CPUSetSetter pattern
                RebuildCoreBits();
            }
            else
            {
                CoreBits.Clear();
            }

            RefreshCommandStates();
        }

        private void RebuildCoreBits()
        {
            this.CoreBits.Clear();

            if (this.SelectedCoreMask == null)
            {
                return;
            }

            for (int i = 0; i < this.SelectedCoreMask.BoolMask.Count; i++)
            {
                this.CoreBits.Add(new CoreBitViewModel(this.SelectedCoreMask.BoolMask, i));
            }
        }

        private void OnBoolMaskCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            TaskSafety.FireAndForget(this.OnBoolMaskCollectionChangedAsync(), ex =>
            {
                this.logger.LogWarning(ex, "Failed to handle mask collection change");
            });
        }

        private async Task OnBoolMaskCollectionChangedAsync()
        {
            // Auto-save when BoolMask changes (like CPUSetSetter's auto-save pattern)
            await this.SaveCurrentMaskAsync();

            // Update status to show real-time changes
            if (this.SelectedCoreMask != null)
            {
                this.StatusMessage = $"{this.SelectedCoreMask.SelectedCoreCount} of {this.SelectedCoreMask.BoolMask.Count} cores selected";
            }
        }

        private void OnMaskPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            TaskSafety.FireAndForget(this.OnMaskPropertyChangedAsync(e), ex =>
            {
                this.logger.LogWarning(ex, "Failed to handle mask property change");
            });
        }

        private async Task OnMaskPropertyChangedAsync(System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CoreMask.Name) ||
                e.PropertyName == nameof(CoreMask.Description) ||
                e.PropertyName == nameof(CoreMask.IsDefault) ||
                e.PropertyName == nameof(CoreMask.IsEnabled))
            {
                await this.SaveCurrentMaskAsync();
            }

            if (e.PropertyName == nameof(CoreMask.Name))
            {
                // CanDeleteMask depends on the name (e.g., "All Cores")
                this.RefreshCommandStates();
            }
        }

        private void RefreshCommandStates()
        {
            this.DeleteMaskCommand.NotifyCanExecuteChanged();
            this.DuplicateMaskCommand.NotifyCanExecuteChanged();
        }
    }
}

