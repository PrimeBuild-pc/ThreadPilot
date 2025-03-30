using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using ThreadPilot.Commands;
using ThreadPilot.Models;
using ThreadPilot.Services;

namespace ThreadPilot.ViewModels
{
    /// <summary>
    /// View model for the power profile editor view
    /// </summary>
    public class ProfileEditorViewModel : ViewModelBase
    {
        #region Private Fields
        
        private ObservableCollection<PowerProfile> _availableProfiles;
        private PowerProfile _selectedProfile;
        private string _profileName = string.Empty;
        private string _profileDescription = string.Empty;
        private bool _isEditing = false;
        private bool _isLoading = false;
        
        #endregion
        
        #region Properties
        
        /// <summary>
        /// Collection of available power profiles
        /// </summary>
        public ObservableCollection<PowerProfile> AvailableProfiles
        {
            get => _availableProfiles;
            set => SetProperty(ref _availableProfiles, value);
        }
        
        /// <summary>
        /// Currently selected power profile
        /// </summary>
        public PowerProfile SelectedProfile
        {
            get => _selectedProfile;
            set => SetProperty(ref _selectedProfile, value, UpdateEditFields);
        }
        
        /// <summary>
        /// Name of the profile being edited
        /// </summary>
        public string ProfileName
        {
            get => _profileName;
            set => SetProperty(ref _profileName, value);
        }
        
        /// <summary>
        /// Description of the profile being edited
        /// </summary>
        public string ProfileDescription
        {
            get => _profileDescription;
            set => SetProperty(ref _profileDescription, value);
        }
        
        /// <summary>
        /// Whether the view model is in editing mode
        /// </summary>
        public bool IsEditing
        {
            get => _isEditing;
            set => SetProperty(ref _isEditing, value);
        }
        
        /// <summary>
        /// Whether the view model is loading data
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }
        
        #endregion
        
        #region Commands
        
        /// <summary>
        /// Command to create a new power profile
        /// </summary>
        public ICommand NewProfileCommand { get; }
        
        /// <summary>
        /// Command to edit the selected power profile
        /// </summary>
        public ICommand EditProfileCommand { get; }
        
        /// <summary>
        /// Command to save the current power profile
        /// </summary>
        public ICommand SaveProfileCommand { get; }
        
        /// <summary>
        /// Command to cancel editing
        /// </summary>
        public ICommand CancelEditCommand { get; }
        
        /// <summary>
        /// Command to delete the selected power profile
        /// </summary>
        public ICommand DeleteProfileCommand { get; }
        
        /// <summary>
        /// Command to import a power profile
        /// </summary>
        public ICommand ImportProfileCommand { get; }
        
        /// <summary>
        /// Command to export the selected power profile
        /// </summary>
        public ICommand ExportProfileCommand { get; }
        
        /// <summary>
        /// Command to apply the selected power profile
        /// </summary>
        public ICommand ApplyProfileCommand { get; }
        
        /// <summary>
        /// Command to refresh the list of power profiles
        /// </summary>
        public ICommand RefreshProfilesCommand { get; }
        
        #endregion
        
        /// <summary>
        /// Constructor
        /// </summary>
        public ProfileEditorViewModel()
        {
            // Initialize collections
            AvailableProfiles = new ObservableCollection<PowerProfile>();
            
            // Initialize commands
            NewProfileCommand = new RelayCommand(NewProfile);
            EditProfileCommand = new RelayCommand(EditProfile, CanEditProfile);
            SaveProfileCommand = new RelayCommand(SaveProfile, CanSaveProfile);
            CancelEditCommand = new RelayCommand(CancelEdit, CanCancelEdit);
            DeleteProfileCommand = new RelayCommand(DeleteProfile, CanDeleteProfile);
            ImportProfileCommand = new RelayCommand(ImportProfile);
            ExportProfileCommand = new RelayCommand(ExportProfile, CanExportProfile);
            ApplyProfileCommand = new RelayCommand(ApplyProfile, CanApplyProfile);
            RefreshProfilesCommand = new RelayCommand(RefreshProfiles);
            
            // Load initial data
            LoadProfiles();
        }
        
        /// <summary>
        /// Load power profiles
        /// </summary>
        private void LoadProfiles()
        {
            IsLoading = true;
            
            try
            {
                // This is where we would retrieve power profiles from the power profile service
                // For now, we'll create some sample data
                
                AvailableProfiles.Clear();
                
                // In the future, this will be retrieved from IPowerProfileService
                // For example: AvailableProfiles = new ObservableCollection<PowerProfile>(ServiceLocator.Get<IPowerProfileService>().GetAllProfiles());
                
                // For now, let's add some sample profiles
                AvailableProfiles.Add(new PowerProfile
                {
                    Id = Guid.NewGuid(),
                    Name = "Power Saver",
                    Description = "Conserves battery life by limiting CPU power"
                });
                
                AvailableProfiles.Add(new PowerProfile
                {
                    Id = Guid.NewGuid(),
                    Name = "Balanced",
                    Description = "Balanced performance and energy efficiency"
                });
                
                AvailableProfiles.Add(new PowerProfile
                {
                    Id = Guid.NewGuid(),
                    Name = "High Performance",
                    Description = "Maximizes system performance at the cost of higher power consumption"
                });
                
                IsLoading = false;
            }
            catch (Exception ex)
            {
                IsLoading = false;
                NotificationService.ShowError($"Error loading power profiles: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Refresh the list of power profiles
        /// </summary>
        private void RefreshProfiles(object parameter)
        {
            LoadProfiles();
        }
        
        /// <summary>
        /// Create a new power profile
        /// </summary>
        private void NewProfile(object parameter)
        {
            IsEditing = true;
            SelectedProfile = null;
            
            // Reset edit fields
            ProfileName = string.Empty;
            ProfileDescription = string.Empty;
        }
        
        /// <summary>
        /// Edit the selected power profile
        /// </summary>
        private void EditProfile(object parameter)
        {
            if (SelectedProfile == null) return;
            
            IsEditing = true;
            
            // Copy profile data to edit fields
            ProfileName = SelectedProfile.Name;
            ProfileDescription = SelectedProfile.Description;
        }
        
        /// <summary>
        /// Check if the selected profile can be edited
        /// </summary>
        private bool CanEditProfile(object parameter)
        {
            return SelectedProfile != null && !IsEditing;
        }
        
        /// <summary>
        /// Save the current power profile
        /// </summary>
        private void SaveProfile(object parameter)
        {
            try
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(ProfileName))
                {
                    NotificationService.ShowError("Profile name cannot be empty");
                    return;
                }
                
                if (SelectedProfile == null)
                {
                    // Create a new profile
                    var newProfile = new PowerProfile
                    {
                        Id = Guid.NewGuid(),
                        Name = ProfileName,
                        Description = ProfileDescription
                    };
                    
                    // Add to collection
                    AvailableProfiles.Add(newProfile);
                    
                    // Select the new profile
                    SelectedProfile = newProfile;
                    
                    NotificationService.ShowSuccess($"Profile '{newProfile.Name}' created successfully");
                }
                else
                {
                    // Update existing profile
                    SelectedProfile.Name = ProfileName;
                    SelectedProfile.Description = ProfileDescription;
                    
                    // Notify property changed for the selected profile
                    OnPropertyChanged(nameof(SelectedProfile));
                    
                    NotificationService.ShowSuccess($"Profile '{SelectedProfile.Name}' updated successfully");
                }
                
                // Exit editing mode
                IsEditing = false;
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Error saving profile: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Check if the current profile can be saved
        /// </summary>
        private bool CanSaveProfile(object parameter)
        {
            return IsEditing;
        }
        
        /// <summary>
        /// Cancel editing
        /// </summary>
        private void CancelEdit(object parameter)
        {
            IsEditing = false;
            
            // Reset edit fields
            UpdateEditFields();
        }
        
        /// <summary>
        /// Check if editing can be canceled
        /// </summary>
        private bool CanCancelEdit(object parameter)
        {
            return IsEditing;
        }
        
        /// <summary>
        /// Delete the selected power profile
        /// </summary>
        private void DeleteProfile(object parameter)
        {
            if (SelectedProfile == null) return;
            
            try
            {
                // Show confirmation dialog
                var result = System.Windows.MessageBox.Show(
                    $"Are you sure you want to delete the profile '{SelectedProfile.Name}'?",
                    "Confirm Profile Deletion",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);
                
                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    // Remove from collection
                    AvailableProfiles.Remove(SelectedProfile);
                    
                    // Clear selection
                    SelectedProfile = null;
                    
                    NotificationService.ShowSuccess("Profile deleted successfully");
                }
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Error deleting profile: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Check if the selected profile can be deleted
        /// </summary>
        private bool CanDeleteProfile(object parameter)
        {
            return SelectedProfile != null && !IsEditing;
        }
        
        /// <summary>
        /// Import a power profile
        /// </summary>
        private void ImportProfile(object parameter)
        {
            try
            {
                // Show file open dialog
                string filePath = FileDialogService.OpenFile("Power Profiles (*.pow)|*.pow|All Files (*.*)|*.*");
                
                if (!string.IsNullOrEmpty(filePath))
                {
                    // This is where we would import a power profile from a file
                    // For example: PowerProfile importedProfile = ServiceLocator.Get<IPowerProfileService>().ImportProfile(filePath);
                    
                    // For now, we'll just create a new profile with the file name
                    var fileName = Path.GetFileNameWithoutExtension(filePath);
                    var newProfile = new PowerProfile
                    {
                        Id = Guid.NewGuid(),
                        Name = fileName,
                        Description = $"Imported from {Path.GetFileName(filePath)}"
                    };
                    
                    // Add to collection
                    AvailableProfiles.Add(newProfile);
                    
                    // Select the new profile
                    SelectedProfile = newProfile;
                    
                    NotificationService.ShowSuccess($"Profile '{newProfile.Name}' imported successfully");
                }
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Error importing profile: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Export the selected power profile
        /// </summary>
        private void ExportProfile(object parameter)
        {
            if (SelectedProfile == null) return;
            
            try
            {
                // Show file save dialog
                string defaultFileName = SanitizeFileName(SelectedProfile.Name) + ".pow";
                string filePath = FileDialogService.SaveFile(
                    "Power Profiles (*.pow)|*.pow|All Files (*.*)|*.*",
                    defaultFileName);
                
                if (!string.IsNullOrEmpty(filePath))
                {
                    // This is where we would export the power profile to a file
                    // For example: ServiceLocator.Get<IPowerProfileService>().ExportProfile(SelectedProfile, filePath);
                    
                    // For now, we'll just show a notification
                    NotificationService.ShowSuccess($"Profile '{SelectedProfile.Name}' exported successfully to {filePath}");
                }
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Error exporting profile: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Check if the selected profile can be exported
        /// </summary>
        private bool CanExportProfile(object parameter)
        {
            return SelectedProfile != null && !IsEditing;
        }
        
        /// <summary>
        /// Apply the selected power profile
        /// </summary>
        private void ApplyProfile(object parameter)
        {
            if (SelectedProfile == null) return;
            
            try
            {
                // This is where we would apply the power profile to the system
                // For example: bool success = ServiceLocator.Get<IPowerProfileService>().ApplyProfile(SelectedProfile);
                
                // For now, we'll just show a notification
                NotificationService.ShowSuccess($"Profile '{SelectedProfile.Name}' applied successfully");
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Error applying profile: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Check if the selected profile can be applied
        /// </summary>
        private bool CanApplyProfile(object parameter)
        {
            return SelectedProfile != null && !IsEditing;
        }
        
        /// <summary>
        /// Update the edit fields based on the selected profile
        /// </summary>
        private void UpdateEditFields()
        {
            if (SelectedProfile != null)
            {
                ProfileName = SelectedProfile.Name;
                ProfileDescription = SelectedProfile.Description;
            }
            else
            {
                ProfileName = string.Empty;
                ProfileDescription = string.Empty;
            }
        }
        
        /// <summary>
        /// Sanitize a file name by removing invalid characters
        /// </summary>
        /// <param name="fileName">File name to sanitize</param>
        /// <returns>Sanitized file name</returns>
        private string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return "profile";
            }
            
            // Remove invalid file name characters
            char[] invalidChars = Path.GetInvalidFileNameChars();
            string result = fileName;
            
            foreach (char c in invalidChars)
            {
                result = result.Replace(c, '_');
            }
            
            return result;
        }
    }
}