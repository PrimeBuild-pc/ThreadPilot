using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using ThreadPilot.Helpers;
using ThreadPilot.Models;
using ThreadPilot.Services;

namespace ThreadPilot.ViewModels
{
    /// <summary>
    /// ViewModel for the PowerProfilesView
    /// </summary>
    public class PowerProfilesViewModel : ViewModelBase
    {
        private readonly INotificationService _notificationService;
        private ObservableCollection<BundledPowerProfile> _powerProfiles;
        private BundledPowerProfile _selectedProfile;
        private BundledPowerProfile _activeProfile;

        /// <summary>
        /// Gets or sets the collection of power profiles
        /// </summary>
        public ObservableCollection<BundledPowerProfile> PowerProfiles
        {
            get => _powerProfiles;
            set
            {
                _powerProfiles = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets or sets the currently selected power profile
        /// </summary>
        public BundledPowerProfile SelectedProfile
        {
            get => _selectedProfile;
            set
            {
                _selectedProfile = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets or sets the currently active power profile
        /// </summary>
        public BundledPowerProfile ActiveProfile
        {
            get => _activeProfile;
            set
            {
                _activeProfile = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Command to apply the selected power profile
        /// </summary>
        public ICommand ApplyProfileCommand { get; }

        /// <summary>
        /// Command to import a power profile
        /// </summary>
        public ICommand ImportProfileCommand { get; }

        /// <summary>
        /// Command to export the selected power profile
        /// </summary>
        public ICommand ExportProfileCommand { get; }

        /// <summary>
        /// Command to edit the selected power profile
        /// </summary>
        public ICommand EditProfileCommand { get; }

        /// <summary>
        /// Command to toggle the favorite status of the selected power profile
        /// </summary>
        public ICommand ToggleFavoriteCommand { get; }

        /// <summary>
        /// Command to create a new power profile
        /// </summary>
        public ICommand CreateProfileCommand { get; }

        /// <summary>
        /// Initializes a new instance of the PowerProfilesViewModel class
        /// </summary>
        public PowerProfilesViewModel()
        {
            // Get services
            _notificationService = ServiceLocator.GetService<INotificationService>();

            // Initialize commands
            ApplyProfileCommand = new RelayCommand(ExecuteApplyProfile, CanExecuteApplyProfile);
            ImportProfileCommand = new RelayCommand(ExecuteImportProfile);
            ExportProfileCommand = new RelayCommand(ExecuteExportProfile, CanExecuteExportProfile);
            EditProfileCommand = new RelayCommand(ExecuteEditProfile, CanExecuteEditProfile);
            ToggleFavoriteCommand = new RelayCommand(ExecuteToggleFavorite, CanExecuteToggleFavorite);
            CreateProfileCommand = new RelayCommand(ExecuteCreateProfile);

            // Initialize collections
            PowerProfiles = new ObservableCollection<BundledPowerProfile>();
            
            // Load profiles
            LoadProfiles();
        }

        /// <summary>
        /// Loads power profiles from disk
        /// </summary>
        private void LoadProfiles()
        {
            try
            {
                // Clear existing profiles
                PowerProfiles.Clear();
                
                // Add sample profiles (in a real app, these would be loaded from disk)
                AddSampleProfiles();
                
                // Set active profile
                if (PowerProfiles.Count > 0)
                {
                    ActiveProfile = PowerProfiles.FirstOrDefault(p => p.IsActive) ?? PowerProfiles[0];
                    SelectedProfile = ActiveProfile;
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error loading power profiles: {ex.Message}");
            }
        }

        /// <summary>
        /// Adds sample power profiles for demonstration purposes
        /// </summary>
        private void AddSampleProfiles()
        {
            // Add a few sample profiles
            PowerProfiles.Add(new BundledPowerProfile
            {
                Name = "Balanced",
                Description = "Standard Windows power plan with balanced performance and energy usage",
                Author = "Microsoft",
                Version = "1.0",
                Category = BundledPowerProfile.ProfileCategory.Balanced,
                FilePath = "balanced.pow",
                IsActive = true
            });
            
            PowerProfiles.Add(new BundledPowerProfile
            {
                Name = "High Performance",
                Description = "Maximum performance with higher energy consumption",
                Author = "Microsoft",
                Version = "1.0",
                Category = BundledPowerProfile.ProfileCategory.Performance,
                FilePath = "high_performance.pow",
                IsActive = false
            });
            
            PowerProfiles.Add(new BundledPowerProfile
            {
                Name = "Power Saver",
                Description = "Maximizes battery life at the expense of performance",
                Author = "Microsoft",
                Version = "1.0",
                Category = BundledPowerProfile.ProfileCategory.PowerSaving,
                FilePath = "power_saver.pow",
                IsActive = false
            });
            
            PowerProfiles.Add(new BundledPowerProfile
            {
                Name = "Ultimate Performance",
                Description = "Designed for workstations and high-end PCs for maximum performance",
                Author = "Microsoft",
                Version = "1.0",
                Category = BundledPowerProfile.ProfileCategory.Performance,
                FilePath = "ultimate_performance.pow",
                IsActive = false,
                IsFavorite = true
            });
            
            PowerProfiles.Add(new BundledPowerProfile
            {
                Name = "Gaming Mode",
                Description = "Optimized for gaming with high CPU performance and responsive input",
                Author = "ThreadPilot",
                Version = "1.0",
                Category = BundledPowerProfile.ProfileCategory.Gaming,
                FilePath = "gaming.pow",
                IsActive = false,
                IsFavorite = true
            });
        }

        #region Command Execution Methods

        /// <summary>
        /// Determines whether the apply profile command can be executed
        /// </summary>
        private bool CanExecuteApplyProfile()
        {
            return SelectedProfile != null && !SelectedProfile.IsActive;
        }

        /// <summary>
        /// Executes the apply profile command
        /// </summary>
        private void ExecuteApplyProfile()
        {
            try
            {
                if (SelectedProfile == null)
                    return;

                // Set the active status of profiles
                foreach (var profile in PowerProfiles)
                {
                    profile.IsActive = false;
                }
                
                SelectedProfile.IsActive = true;
                ActiveProfile = SelectedProfile;
                
                // In a real implementation, this would apply the power profile using Windows APIs
                
                _notificationService.ShowSuccess($"Power profile '{SelectedProfile.Name}' applied successfully");
                
                // Refresh the profiles list to update the UI
                OnPropertyChanged(nameof(PowerProfiles));
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error applying power profile: {ex.Message}");
            }
        }

        /// <summary>
        /// Executes the import profile command
        /// </summary>
        private void ExecuteImportProfile()
        {
            try
            {
                // In a real implementation, this would show a file dialog and import the profile
                _notificationService.ShowInfo("Import power profile functionality is not implemented yet");
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error importing power profile: {ex.Message}");
            }
        }

        /// <summary>
        /// Determines whether the export profile command can be executed
        /// </summary>
        private bool CanExecuteExportProfile()
        {
            return SelectedProfile != null;
        }

        /// <summary>
        /// Executes the export profile command
        /// </summary>
        private void ExecuteExportProfile()
        {
            try
            {
                if (SelectedProfile == null)
                    return;
                
                // In a real implementation, this would show a file dialog and export the profile
                _notificationService.ShowInfo("Export power profile functionality is not implemented yet");
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error exporting power profile: {ex.Message}");
            }
        }

        /// <summary>
        /// Determines whether the edit profile command can be executed
        /// </summary>
        private bool CanExecuteEditProfile()
        {
            return SelectedProfile != null;
        }

        /// <summary>
        /// Executes the edit profile command
        /// </summary>
        private void ExecuteEditProfile()
        {
            try
            {
                if (SelectedProfile == null)
                    return;
                
                // In a real implementation, this would show a dialog to edit the profile
                _notificationService.ShowInfo("Edit power profile functionality is not implemented yet");
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error editing power profile: {ex.Message}");
            }
        }

        /// <summary>
        /// Determines whether the toggle favorite command can be executed
        /// </summary>
        private bool CanExecuteToggleFavorite()
        {
            return SelectedProfile != null;
        }

        /// <summary>
        /// Executes the toggle favorite command
        /// </summary>
        private void ExecuteToggleFavorite()
        {
            try
            {
                if (SelectedProfile == null)
                    return;
                
                SelectedProfile.IsFavorite = !SelectedProfile.IsFavorite;
                
                if (SelectedProfile.IsFavorite)
                {
                    _notificationService.ShowSuccess($"Power profile '{SelectedProfile.Name}' added to favorites");
                }
                else
                {
                    _notificationService.ShowSuccess($"Power profile '{SelectedProfile.Name}' removed from favorites");
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error toggling favorite status: {ex.Message}");
            }
        }

        /// <summary>
        /// Executes the create profile command
        /// </summary>
        private void ExecuteCreateProfile()
        {
            try
            {
                // In a real implementation, this would show a dialog to create a new profile
                _notificationService.ShowInfo("Create power profile functionality is not implemented yet");
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error creating power profile: {ex.Message}");
            }
        }

        #endregion
    }
}