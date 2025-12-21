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

namespace ThreadPilot.ViewModels
{
    /// <summary>
    /// Wrapper for individual core bit in the mask, similar to CPUSetSetter's MaskBitViewModel
    /// </summary>
    public partial class CoreBitViewModel : ObservableObject
    {
        private readonly ObservableCollection<bool> _boolMask;
        private readonly int _index;

        public int Index => _index;

        public bool IsSelected
        {
            get => _index < _boolMask.Count && _boolMask[_index];
            set
            {
                if (_index < _boolMask.Count && _boolMask[_index] != value)
                {
                    _boolMask[_index] = value;
                    OnPropertyChanged();
                }
            }
        }

        public CoreBitViewModel(ObservableCollection<bool> boolMask, int index)
        {
            _boolMask = boolMask;
            _index = index;
        }
    }

    /// <summary>
    /// ViewModel for managing CPU core affinity masks
    /// Based on CPUSetSetter's MasksTabViewModel
    /// </summary>
    public partial class MasksViewModel : ObservableObject
    {
        private readonly ICoreMaskService _coreMaskService;
        private readonly ICpuTopologyService _cpuTopologyService;
        private readonly ILogger<MasksViewModel> _logger;

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
        /// Can delete if: mask selected, not "All Cores" baseline, not actively applied to processes
        /// Note: The actual validation happens in DeleteMask command with proper async checks
        /// </summary>
        public bool CanDeleteMask => SelectedCoreMask != null && SelectedCoreMask.Name != "All Cores";
        public bool CanDuplicateMask => SelectedCoreMask != null;

        public MasksViewModel(
            ICoreMaskService coreMaskService,
            ICpuTopologyService cpuTopologyService,
            ILogger<MasksViewModel> logger)
        {
            _coreMaskService = coreMaskService ?? throw new ArgumentNullException(nameof(coreMaskService));
            _cpuTopologyService = cpuTopologyService ?? throw new ArgumentNullException(nameof(cpuTopologyService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Initialize
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                await _coreMaskService.InitializeAsync();

                // Link to the service's collection
                CoreMasks = _coreMaskService.AvailableMasks;

                // Select the default mask
                SelectedCoreMask = _coreMaskService.DefaultMask;

                _logger.LogInformation("MasksViewModel initialized with {Count} masks", CoreMasks.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize MasksViewModel");
                StatusMessage = $"Error initializing masks: {ex.Message}";
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
                    Description = $"Created at {DateTime.Now:HH:mm:ss}"
                };

                // Initialize with all cores enabled
                for (int i = 0; i < coreCount; i++)
                    newMask.BoolMask.Add(true);

                CoreMasks.Add(newMask);
                SelectedCoreMask = newMask;

                await _coreMaskService.SaveMasksAsync();

                StatusMessage = $"Created mask '{newMask.Name}'";
                _logger.LogInformation("Created new mask '{Name}'", newMask.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create mask");
                StatusMessage = $"Error creating mask: {ex.Message}";
                MessageBox.Show($"Failed to create mask: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand(CanExecute = nameof(CanDeleteMask))]
        private async Task DeleteMask()
        {
            if (SelectedCoreMask == null)
                return;

            try
            {
                var maskId = SelectedCoreMask.Id;
                var maskName = SelectedCoreMask.Name;

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
                bool isActivelyApplied = await _coreMaskService.IsMaskActivelyAppliedAsync(maskId);
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
                var referencingProfiles = (await _coreMaskService.GetProfilesReferencingMaskAsync(maskId)).ToList();
                if (referencingProfiles.Any())
                {
                    var profileList = string.Join("\n  - ", referencingProfiles.Take(10));
                    if (referencingProfiles.Count > 10)
                        profileList += $"\n  ... and {referencingProfiles.Count - 10} more";

                    var result = MessageBox.Show(
                        $"The mask '{maskName}' is referenced by the following profiles/rules:\n\n" +
                        $"  - {profileList}\n\n" +
                        "If you delete this mask, these profiles will be updated to use the 'All Cores' mask instead.\n\n" +
                        "Do you want to continue?",
                        "Mask Referenced by Profiles",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result != MessageBoxResult.Yes)
                        return;

                    // Update all referencing profiles to use "All Cores"
                    await _coreMaskService.UpdateProfilesToDefaultMaskAsync(maskId);
                    StatusMessage = $"Updated {referencingProfiles.Count} profile(s) to use 'All Cores' mask";
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
                        return;
                }

                // 4. Delete the mask
                await _coreMaskService.DeleteMaskAsync(maskId);
                SelectedCoreMask = CoreMasks.FirstOrDefault();

                StatusMessage = $"Deleted mask '{maskName}'";
                _logger.LogInformation("Deleted mask '{Name}'", maskName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete mask");
                StatusMessage = $"Error deleting mask: {ex.Message}";
                MessageBox.Show($"Failed to delete mask: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand(CanExecute = nameof(CanDuplicateMask))]
        private async Task DuplicateMask()
        {
            if (SelectedCoreMask == null)
                return;

            try
            {
                var cloned = SelectedCoreMask.Clone();
                CoreMasks.Add(cloned);
                SelectedCoreMask = cloned;

                await _coreMaskService.SaveMasksAsync();

                StatusMessage = $"Duplicated mask '{cloned.Name}'";
                _logger.LogInformation("Duplicated mask to '{Name}'", cloned.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to duplicate mask");
                StatusMessage = $"Error duplicating mask: {ex.Message}";
                MessageBox.Show($"Failed to duplicate mask: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SaveCurrentMaskAsync()
        {
            if (SelectedCoreMask == null)
                return;

            try
            {
                await _coreMaskService.UpdateMaskAsync(SelectedCoreMask);
                _logger.LogDebug("Saved mask '{Name}'", SelectedCoreMask.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save mask");
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
            CoreBits.Clear();

            if (SelectedCoreMask == null)
                return;

            for (int i = 0; i < SelectedCoreMask.BoolMask.Count; i++)
            {
                CoreBits.Add(new CoreBitViewModel(SelectedCoreMask.BoolMask, i));
            }
        }

        private async void OnBoolMaskCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Auto-save when BoolMask changes (like CPUSetSetter's auto-save pattern)
            await SaveCurrentMaskAsync();

            // Update status to show real-time changes
            if (SelectedCoreMask != null)
            {
                StatusMessage = $"{SelectedCoreMask.SelectedCoreCount} of {SelectedCoreMask.BoolMask.Count} cores selected";
            }
        }

        private async void OnMaskPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CoreMask.Name) ||
                e.PropertyName == nameof(CoreMask.Description) ||
                e.PropertyName == nameof(CoreMask.IsDefault) ||
                e.PropertyName == nameof(CoreMask.IsEnabled))
            {
                await SaveCurrentMaskAsync();
            }

            if (e.PropertyName == nameof(CoreMask.Name))
            {
                // CanDeleteMask depends on the name (e.g., "All Cores")
                RefreshCommandStates();
            }
        }

        private void RefreshCommandStates()
        {
            DeleteMaskCommand.NotifyCanExecuteChanged();
            DuplicateMaskCommand.NotifyCanExecuteChanged();
        }
    }
}
