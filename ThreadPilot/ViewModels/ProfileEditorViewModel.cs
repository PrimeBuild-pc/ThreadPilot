using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using ThreadPilot.Commands;
using ThreadPilot.Models;
using ThreadPilot.Services;

namespace ThreadPilot.ViewModels
{
    /// <summary>
    /// Profile editor view model
    /// </summary>
    public class ProfileEditorViewModel : ViewModelBase
    {
        // Selected profile
        private BundledPowerProfile? _selectedProfile;
        
        // Selected rule
        private ProcessAffinityRule? _selectedRule;
        
        // Power profile service
        private readonly IPowerProfileService _powerProfileService;
        
        // File dialog service
        private readonly IFileDialogService _fileDialogService;
        
        // Notification service
        private readonly INotificationService _notificationService;
        
        /// <summary>
        /// Constructor
        /// </summary>
        public ProfileEditorViewModel()
        {
            // Get services
            _powerProfileService = ServiceLocator.Get<IPowerProfileService>();
            _fileDialogService = ServiceLocator.Get<IFileDialogService>();
            _notificationService = ServiceLocator.Get<INotificationService>();
            
            // Initialize collections
            Profiles = new ObservableCollection<BundledPowerProfile>();
            
            // Create commands
            NewProfileCommand = new RelayCommand(NewProfile);
            SaveProfileCommand = new RelayCommand(SaveProfile, CanSaveProfile);
            DeleteProfileCommand = new RelayCommand(DeleteProfile, CanDeleteProfile);
            ImportProfileCommand = new RelayCommand(ImportProfile);
            ExportProfileCommand = new RelayCommand(ExportProfile, CanExportProfile);
            NewRuleCommand = new RelayCommand(NewRule, CanModifyProfile);
            DeleteRuleCommand = new RelayCommand(DeleteRule, CanDeleteRule);
            
            // Initial load
            LoadProfiles();
        }
        
        /// <summary>
        /// New profile command
        /// </summary>
        public ICommand NewProfileCommand { get; }
        
        /// <summary>
        /// Save profile command
        /// </summary>
        public ICommand SaveProfileCommand { get; }
        
        /// <summary>
        /// Delete profile command
        /// </summary>
        public ICommand DeleteProfileCommand { get; }
        
        /// <summary>
        /// Import profile command
        /// </summary>
        public ICommand ImportProfileCommand { get; }
        
        /// <summary>
        /// Export profile command
        /// </summary>
        public ICommand ExportProfileCommand { get; }
        
        /// <summary>
        /// New rule command
        /// </summary>
        public ICommand NewRuleCommand { get; }
        
        /// <summary>
        /// Delete rule command
        /// </summary>
        public ICommand DeleteRuleCommand { get; }
        
        /// <summary>
        /// Profiles
        /// </summary>
        public ObservableCollection<BundledPowerProfile> Profiles { get; }
        
        /// <summary>
        /// Selected profile
        /// </summary>
        public BundledPowerProfile? SelectedProfile
        {
            get => _selectedProfile;
            set
            {
                if (SetProperty(ref _selectedProfile, value))
                {
                    SelectedRule = value?.ProcessAffinityRules.FirstOrDefault();
                    
                    ((RelayCommand)SaveProfileCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)DeleteProfileCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)ExportProfileCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)NewRuleCommand).RaiseCanExecuteChanged();
                }
            }
        }
        
        /// <summary>
        /// Selected rule
        /// </summary>
        public ProcessAffinityRule? SelectedRule
        {
            get => _selectedRule;
            set
            {
                if (SetProperty(ref _selectedRule, value))
                {
                    ((RelayCommand)DeleteRuleCommand).RaiseCanExecuteChanged();
                }
            }
        }
        
        /// <summary>
        /// Load profiles
        /// </summary>
        private void LoadProfiles()
        {
            try
            {
                // Clear profiles
                Profiles.Clear();
                
                // Load profiles
                foreach (var profile in _powerProfileService.GetAllProfiles())
                {
                    Profiles.Add(profile);
                }
                
                // Select the first profile if none is selected
                SelectedProfile ??= Profiles.Count > 0 ? Profiles[0] : null;
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error loading profiles: {ex.Message}");
            }
        }
        
        /// <summary>
        /// New profile
        /// </summary>
        private void NewProfile(object? parameter)
        {
            try
            {
                // Create a new profile
                var profile = new BundledPowerProfile
                {
                    Name = "New Profile",
                    Description = "New profile description",
                    IsEnabled = true,
                    ShouldUnparkAllCores = false
                };
                
                // Add the profile
                Profiles.Add(profile);
                
                // Select the new profile
                SelectedProfile = profile;
                
                // Save the profile
                if (_powerProfileService.SaveProfile(profile))
                {
                    _notificationService.ShowSuccess("New profile created successfully");
                }
                else
                {
                    _notificationService.ShowError("Failed to create new profile");
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error creating new profile: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Save profile
        /// </summary>
        private void SaveProfile(object? parameter)
        {
            if (SelectedProfile == null)
            {
                return;
            }
            
            try
            {
                if (_powerProfileService.SaveProfile(SelectedProfile))
                {
                    _notificationService.ShowSuccess("Profile saved successfully");
                }
                else
                {
                    _notificationService.ShowError("Failed to save profile");
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error saving profile: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Delete profile
        /// </summary>
        private void DeleteProfile(object? parameter)
        {
            if (SelectedProfile == null)
            {
                return;
            }
            
            try
            {
                // Remember the profile
                var profile = SelectedProfile;
                
                // Select another profile
                var index = Profiles.IndexOf(profile);
                if (index > 0)
                {
                    SelectedProfile = Profiles[index - 1];
                }
                else if (Profiles.Count > 1)
                {
                    SelectedProfile = Profiles[1];
                }
                else
                {
                    SelectedProfile = null;
                }
                
                // Remove the profile
                Profiles.Remove(profile);
                
                // Delete the profile
                if (_powerProfileService.DeleteProfile(profile.Id))
                {
                    _notificationService.ShowSuccess("Profile deleted successfully");
                }
                else
                {
                    _notificationService.ShowError("Failed to delete profile");
                    
                    // Restore the profile
                    Profiles.Add(profile);
                    SelectedProfile = profile;
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error deleting profile: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Import profile
        /// </summary>
        private void ImportProfile(object? parameter)
        {
            try
            {
                // Show open file dialog
                var filePath = _fileDialogService.ShowOpenDialog("Power Profile Files (*.pow)|*.pow|All Files (*.*)|*.*");
                
                if (string.IsNullOrEmpty(filePath))
                {
                    return;
                }
                
                // Import profile
                var profile = _powerProfileService.ImportProfile(filePath);
                
                if (profile != null)
                {
                    _notificationService.ShowSuccess("Profile imported successfully");
                    
                    // Add the profile
                    Profiles.Add(profile);
                    
                    // Select the new profile
                    SelectedProfile = profile;
                }
                else
                {
                    _notificationService.ShowError("Failed to import profile");
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error importing profile: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Export profile
        /// </summary>
        private void ExportProfile(object? parameter)
        {
            if (SelectedProfile == null)
            {
                return;
            }
            
            try
            {
                // Show save file dialog
                var filePath = _fileDialogService.ShowSaveDialog("Power Profile Files (*.pow)|*.pow|All Files (*.*)|*.*", $"{SelectedProfile.Name}.pow");
                
                if (string.IsNullOrEmpty(filePath))
                {
                    return;
                }
                
                // Export profile
                if (_powerProfileService.ExportProfile(SelectedProfile.Id, filePath))
                {
                    _notificationService.ShowSuccess("Profile exported successfully");
                }
                else
                {
                    _notificationService.ShowError("Failed to export profile");
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error exporting profile: {ex.Message}");
            }
        }
        
        /// <summary>
        /// New rule
        /// </summary>
        private void NewRule(object? parameter)
        {
            if (SelectedProfile == null)
            {
                return;
            }
            
            try
            {
                // Create a new rule
                var rule = new ProcessAffinityRule
                {
                    Id = SelectedProfile.ProcessAffinityRules.Count > 0 ? SelectedProfile.ProcessAffinityRules.Max(r => r.Id) + 1 : 1,
                    Name = "New Rule",
                    ProcessNamePattern = ".*",
                    AffinityMask = 0xFFFF,
                    Priority = ProcessPriority.Normal,
                    IsEnabled = true,
                    RulePriority = 100
                };
                
                // Add the rule
                SelectedProfile.ProcessAffinityRules.Add(rule);
                
                // Select the new rule
                SelectedRule = rule;
                
                // Save the profile
                if (_powerProfileService.SaveProfile(SelectedProfile))
                {
                    _notificationService.ShowSuccess("New rule created successfully");
                }
                else
                {
                    _notificationService.ShowError("Failed to create new rule");
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error creating new rule: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Delete rule
        /// </summary>
        private void DeleteRule(object? parameter)
        {
            if (SelectedProfile == null || SelectedRule == null)
            {
                return;
            }
            
            try
            {
                // Remember the rule
                var rule = SelectedRule;
                
                // Select another rule
                var index = SelectedProfile.ProcessAffinityRules.IndexOf(rule);
                if (index > 0)
                {
                    SelectedRule = SelectedProfile.ProcessAffinityRules[index - 1];
                }
                else if (SelectedProfile.ProcessAffinityRules.Count > 1)
                {
                    SelectedRule = SelectedProfile.ProcessAffinityRules[1];
                }
                else
                {
                    SelectedRule = null;
                }
                
                // Remove the rule
                SelectedProfile.ProcessAffinityRules.Remove(rule);
                
                // Save the profile
                if (_powerProfileService.SaveProfile(SelectedProfile))
                {
                    _notificationService.ShowSuccess("Rule deleted successfully");
                }
                else
                {
                    _notificationService.ShowError("Failed to delete rule");
                    
                    // Restore the rule
                    SelectedProfile.ProcessAffinityRules.Add(rule);
                    SelectedRule = rule;
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error deleting rule: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Can save profile
        /// </summary>
        private bool CanSaveProfile(object? parameter)
        {
            return SelectedProfile != null;
        }
        
        /// <summary>
        /// Can delete profile
        /// </summary>
        private bool CanDeleteProfile(object? parameter)
        {
            return SelectedProfile != null;
        }
        
        /// <summary>
        /// Can export profile
        /// </summary>
        private bool CanExportProfile(object? parameter)
        {
            return SelectedProfile != null;
        }
        
        /// <summary>
        /// Can modify profile
        /// </summary>
        private bool CanModifyProfile(object? parameter)
        {
            return SelectedProfile != null;
        }
        
        /// <summary>
        /// Can delete rule
        /// </summary>
        private bool CanDeleteRule(object? parameter)
        {
            return SelectedProfile != null && SelectedRule != null;
        }
    }
}