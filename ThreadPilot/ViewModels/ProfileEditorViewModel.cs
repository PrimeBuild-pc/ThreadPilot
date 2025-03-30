using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using ThreadPilot.Helpers;
using ThreadPilot.Models;
using ThreadPilot.Services;

namespace ThreadPilot.ViewModels
{
    /// <summary>
    /// View model for the power profile editor view
    /// </summary>
    public class ProfileEditorViewModel : ViewModelBase
    {
        // Dependencies
        private readonly IPowerProfileService _powerProfileService;
        private readonly IFileDialogService _fileDialogService;
        private readonly INotificationService _notificationService;
        
        // Backing fields
        private ObservableCollection<BundledPowerProfile> _availableProfiles = new ObservableCollection<BundledPowerProfile>();
        private BundledPowerProfile _selectedProfile;
        private BundledPowerProfile _activeProfile;
        private bool _isEditing;
        private bool _isBusy;
        private string _profileNameText;
        private string _profileDescriptionText;
        private bool _isNewProfile;
        
        /// <summary>
        /// Available power profiles
        /// </summary>
        public ObservableCollection<BundledPowerProfile> AvailableProfiles
        {
            get => _availableProfiles;
            set => SetProperty(ref _availableProfiles, value);
        }
        
        /// <summary>
        /// Currently selected profile
        /// </summary>
        public BundledPowerProfile SelectedProfile
        {
            get => _selectedProfile;
            set
            {
                if (SetProperty(ref _selectedProfile, value) && _selectedProfile != null)
                {
                    RefreshProfileDetails();
                }
            }
        }
        
        /// <summary>
        /// Currently active profile
        /// </summary>
        public BundledPowerProfile ActiveProfile
        {
            get => _activeProfile;
            set => SetProperty(ref _activeProfile, value);
        }
        
        /// <summary>
        /// Indicates if the profile is being edited
        /// </summary>
        public bool IsEditing
        {
            get => _isEditing;
            set => SetProperty(ref _isEditing, value);
        }
        
        /// <summary>
        /// Indicates if an operation is in progress
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }
        
        /// <summary>
        /// Profile name text
        /// </summary>
        public string ProfileNameText
        {
            get => _profileNameText;
            set => SetProperty(ref _profileNameText, value);
        }
        
        /// <summary>
        /// Profile description text
        /// </summary>
        public string ProfileDescriptionText
        {
            get => _profileDescriptionText;
            set => SetProperty(ref _profileDescriptionText, value);
        }
        
        /// <summary>
        /// Indicates if we're creating a new profile
        /// </summary>
        public bool IsNewProfile
        {
            get => _isNewProfile;
            set => SetProperty(ref _isNewProfile, value);
        }
        
        /// <summary>
        /// Command to refresh profiles
        /// </summary>
        public ICommand RefreshCommand { get; }
        
        /// <summary>
        /// Command to apply the selected profile
        /// </summary>
        public ICommand ApplyProfileCommand { get; }
        
        /// <summary>
        /// Command to edit the selected profile
        /// </summary>
        public ICommand EditProfileCommand { get; }
        
        /// <summary>
        /// Command to save profile changes
        /// </summary>
        public ICommand SaveProfileCommand { get; }
        
        /// <summary>
        /// Command to cancel editing
        /// </summary>
        public ICommand CancelEditCommand { get; }
        
        /// <summary>
        /// Command to create a new profile
        /// </summary>
        public ICommand NewProfileCommand { get; }
        
        /// <summary>
        /// Command to import a profile
        /// </summary>
        public ICommand ImportProfileCommand { get; }
        
        /// <summary>
        /// Command to export the selected profile
        /// </summary>
        public ICommand ExportProfileCommand { get; }
        
        /// <summary>
        /// Constructor
        /// </summary>
        public ProfileEditorViewModel()
        {
            // Get dependencies
            _powerProfileService = ServiceLocator.Get<IPowerProfileService>();
            _fileDialogService = ServiceLocator.Get<IFileDialogService>();
            _notificationService = ServiceLocator.Get<INotificationService>();
            
            // Create commands
            RefreshCommand = new RelayCommand(param => RefreshProfiles(), param => !IsBusy);
            ApplyProfileCommand = new RelayCommand(param => ApplySelectedProfile(), param => CanApplyProfile());
            EditProfileCommand = new RelayCommand(param => StartEditing(), param => CanEditProfile());
            SaveProfileCommand = new RelayCommand(param => SaveProfile(), param => CanSaveProfile());
            CancelEditCommand = new RelayCommand(param => CancelEditing(), param => IsEditing);
            NewProfileCommand = new RelayCommand(param => CreateNewProfile(), param => !IsBusy && !IsEditing);
            ImportProfileCommand = new RelayCommand(param => ImportProfile(), param => !IsBusy && !IsEditing);
            ExportProfileCommand = new RelayCommand(param => ExportSelectedProfile(), param => CanExportProfile());
            
            // Load initial data
            RefreshProfiles();
        }
        
        /// <summary>
        /// Refresh available profiles
        /// </summary>
        private void RefreshProfiles()
        {
            try
            {
                IsBusy = true;
                
                // Get available profiles
                var profiles = _powerProfileService.GetAvailableProfiles();
                
                // Update collection
                AvailableProfiles.Clear();
                foreach (var profile in profiles)
                {
                    AvailableProfiles.Add(profile);
                }
                
                // Get active profile
                ActiveProfile = _powerProfileService.GetActiveProfile();
                
                // Select the active profile
                if (ActiveProfile != null)
                {
                    foreach (var profile in AvailableProfiles)
                    {
                        if (profile.Id == ActiveProfile.Id)
                        {
                            SelectedProfile = profile;
                            break;
                        }
                    }
                }
                
                // If nothing is selected, select the first one
                if (SelectedProfile == null && AvailableProfiles.Count > 0)
                {
                    SelectedProfile = AvailableProfiles[0];
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error refreshing profiles: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }
        
        /// <summary>
        /// Apply the selected profile
        /// </summary>
        private async void ApplySelectedProfile()
        {
            if (SelectedProfile == null)
                return;
                
            try
            {
                IsBusy = true;
                
                // Apply the profile
                bool success = await _powerProfileService.ApplyProfileAsync(SelectedProfile);
                
                if (success)
                {
                    // Update active profile
                    ActiveProfile = SelectedProfile;
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error applying profile: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }
        
        /// <summary>
        /// Start editing the selected profile
        /// </summary>
        private void StartEditing()
        {
            if (SelectedProfile == null)
                return;
                
            // Can't edit read-only profiles
            if (SelectedProfile.IsReadOnly)
            {
                _notificationService.ShowWarning("This profile is read-only and cannot be edited.");
                return;
            }
            
            // Start editing
            IsEditing = true;
            IsNewProfile = false;
            
            // Copy the profile details to the text fields
            ProfileNameText = SelectedProfile.Name;
            ProfileDescriptionText = SelectedProfile.Description;
        }
        
        /// <summary>
        /// Save profile changes
        /// </summary>
        private void SaveProfile()
        {
            try
            {
                IsBusy = true;
                
                if (IsNewProfile)
                {
                    // Create a new profile
                    var newProfile = _powerProfileService.CreateProfileFromCurrentSettings(
                        ProfileNameText,
                        ProfileDescriptionText);
                        
                    // Save the profile
                    string filePath = _fileDialogService.ShowSaveFileDialog(
                        "Save Power Profile",
                        "Power Profiles (*.pow)|*.pow",
                        defaultFileName: $"{ProfileNameText}.pow");
                        
                    if (!string.IsNullOrEmpty(filePath))
                    {
                        bool success = _powerProfileService.SaveProfileToFile(newProfile, filePath);
                        
                        if (success)
                        {
                            _notificationService.ShowSuccess("Profile created successfully.");
                            
                            // Refresh profiles and select the new one
                            RefreshProfiles();
                            
                            // Find and select the new profile
                            foreach (var profile in AvailableProfiles)
                            {
                                if (profile.FilePath == filePath)
                                {
                                    SelectedProfile = profile;
                                    break;
                                }
                            }
                        }
                    }
                }
                else
                {
                    // Update the existing profile
                    if (SelectedProfile != null)
                    {
                        SelectedProfile.Name = ProfileNameText;
                        SelectedProfile.Description = ProfileDescriptionText;
                        SelectedProfile.ModifiedOn = DateTime.Now;
                        
                        // Save the profile
                        bool success = _powerProfileService.SaveProfileToFile(
                            SelectedProfile, 
                            SelectedProfile.FilePath);
                            
                        if (success)
                        {
                            _notificationService.ShowSuccess("Profile updated successfully.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error saving profile: {ex.Message}");
            }
            finally
            {
                IsEditing = false;
                IsBusy = false;
            }
        }
        
        /// <summary>
        /// Cancel editing
        /// </summary>
        private void CancelEditing()
        {
            IsEditing = false;
            
            // Refresh details from the selected profile
            RefreshProfileDetails();
        }
        
        /// <summary>
        /// Create a new profile
        /// </summary>
        private void CreateNewProfile()
        {
            IsEditing = true;
            IsNewProfile = true;
            
            // Set default values
            ProfileNameText = "New Profile";
            ProfileDescriptionText = "Created on " + DateTime.Now.ToString("yyyy-MM-dd");
            
            // Clear selected profile
            SelectedProfile = null;
        }
        
        /// <summary>
        /// Import a profile
        /// </summary>
        private void ImportProfile()
        {
            try
            {
                // Show file dialog
                string filePath = _fileDialogService.ShowOpenFileDialog(
                    "Import Power Profile",
                    "Power Profiles (*.pow)|*.pow");
                    
                if (!string.IsNullOrEmpty(filePath))
                {
                    // Load the profile
                    var profile = _powerProfileService.LoadProfileFromFile(filePath);
                    
                    if (profile != null)
                    {
                        // Refresh profiles and select the imported one
                        RefreshProfiles();
                        
                        // Find and select the imported profile
                        foreach (var p in AvailableProfiles)
                        {
                            if (p.FilePath == filePath)
                            {
                                SelectedProfile = p;
                                break;
                            }
                        }
                        
                        _notificationService.ShowSuccess($"Profile '{profile.Name}' imported successfully.");
                    }
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error importing profile: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Export the selected profile
        /// </summary>
        private void ExportSelectedProfile()
        {
            if (SelectedProfile == null)
                return;
                
            try
            {
                // Show file dialog
                string filePath = _fileDialogService.ShowSaveFileDialog(
                    "Export Power Profile",
                    "Power Profiles (*.pow)|*.pow",
                    defaultFileName: $"{SelectedProfile.Name}.pow");
                    
                if (!string.IsNullOrEmpty(filePath))
                {
                    // Save the profile
                    bool success = _powerProfileService.SaveProfileToFile(SelectedProfile, filePath);
                    
                    if (success)
                    {
                        _notificationService.ShowSuccess($"Profile '{SelectedProfile.Name}' exported successfully.");
                    }
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error exporting profile: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Refresh profile details
        /// </summary>
        private void RefreshProfileDetails()
        {
            if (SelectedProfile != null)
            {
                ProfileNameText = SelectedProfile.Name;
                ProfileDescriptionText = SelectedProfile.Description;
            }
            else
            {
                ProfileNameText = string.Empty;
                ProfileDescriptionText = string.Empty;
            }
        }
        
        /// <summary>
        /// Check if the selected profile can be applied
        /// </summary>
        private bool CanApplyProfile()
        {
            return !IsBusy && SelectedProfile != null && (ActiveProfile == null || SelectedProfile.Id != ActiveProfile.Id);
        }
        
        /// <summary>
        /// Check if the selected profile can be edited
        /// </summary>
        private bool CanEditProfile()
        {
            return !IsBusy && !IsEditing && SelectedProfile != null && !SelectedProfile.IsReadOnly;
        }
        
        /// <summary>
        /// Check if the profile can be saved
        /// </summary>
        private bool CanSaveProfile()
        {
            return !IsBusy && IsEditing && !string.IsNullOrWhiteSpace(ProfileNameText);
        }
        
        /// <summary>
        /// Check if the selected profile can be exported
        /// </summary>
        private bool CanExportProfile()
        {
            return !IsBusy && !IsEditing && SelectedProfile != null;
        }
    }
}