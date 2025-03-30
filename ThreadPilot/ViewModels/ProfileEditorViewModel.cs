using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
        private readonly IPowerProfileService _powerProfileService;
        private readonly IFileDialogService _fileDialogService;
        private PowerProfile _selectedProfile;
        private ProcessAffinityRule _selectedRule;
        private string _searchText;
        
        /// <summary>
        /// Constructor
        /// </summary>
        public ProfileEditorViewModel()
        {
            // Get services
            _powerProfileService = ServiceLocator.Resolve<IPowerProfileService>();
            _fileDialogService = ServiceLocator.Resolve<IFileDialogService>();
            
            // Initialize properties
            Profiles = new ObservableCollection<PowerProfile>();
            Rules = new ObservableCollection<ProcessAffinityRule>();
            AvailablePriorities = new ObservableCollection<ProcessPriority>(Enum.GetValues(typeof(ProcessPriority)).Cast<ProcessPriority>());
            
            // Initialize commands
            RefreshCommand = new RelayCommand(_ => RefreshProfiles());
            NewProfileCommand = new RelayCommand(_ => CreateNewProfile());
            CloneProfileCommand = new RelayCommand(_ => CloneProfile(), _ => SelectedProfile != null);
            DeleteProfileCommand = new RelayCommand(_ => DeleteProfile(), _ => SelectedProfile != null && !SelectedProfile.IsBundled);
            ImportProfileCommand = new RelayCommand(_ => ImportProfile());
            ExportProfileCommand = new RelayCommand(_ => ExportProfile(), _ => SelectedProfile != null);
            
            AddRuleCommand = new RelayCommand(_ => AddRule(), _ => SelectedProfile != null);
            RemoveRuleCommand = new RelayCommand(_ => RemoveRule(), _ => SelectedRule != null);
            SaveProfileCommand = new RelayCommand(_ => SaveProfile(), _ => SelectedProfile != null && !SelectedProfile.IsBundled);
            ApplyRulesCommand = new RelayCommand(_ => ApplyRules(), _ => SelectedProfile != null);
            
            // Initial load
            RefreshProfiles();
        }
        
        /// <summary>
        /// Power profiles collection
        /// </summary>
        public ObservableCollection<PowerProfile> Profiles { get; }
        
        /// <summary>
        /// Process affinity rules collection for the selected profile
        /// </summary>
        public ObservableCollection<ProcessAffinityRule> Rules { get; }
        
        /// <summary>
        /// Available process priorities collection
        /// </summary>
        public ObservableCollection<ProcessPriority> AvailablePriorities { get; }
        
        /// <summary>
        /// Selected profile
        /// </summary>
        public PowerProfile SelectedProfile
        {
            get => _selectedProfile;
            set
            {
                if (SetProperty(ref _selectedProfile, value))
                {
                    // Load rules for the selected profile
                    LoadRulesForProfile();
                    
                    // Reset selected rule
                    SelectedRule = null;
                }
            }
        }
        
        /// <summary>
        /// Selected rule
        /// </summary>
        public ProcessAffinityRule SelectedRule
        {
            get => _selectedRule;
            set => SetProperty(ref _selectedRule, value);
        }
        
        /// <summary>
        /// Search text
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    // Refresh with filter
                    RefreshProfiles();
                }
            }
        }
        
        /// <summary>
        /// Refresh command
        /// </summary>
        public ICommand RefreshCommand { get; }
        
        /// <summary>
        /// New profile command
        /// </summary>
        public ICommand NewProfileCommand { get; }
        
        /// <summary>
        /// Clone profile command
        /// </summary>
        public ICommand CloneProfileCommand { get; }
        
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
        /// Add rule command
        /// </summary>
        public ICommand AddRuleCommand { get; }
        
        /// <summary>
        /// Remove rule command
        /// </summary>
        public ICommand RemoveRuleCommand { get; }
        
        /// <summary>
        /// Save profile command
        /// </summary>
        public ICommand SaveProfileCommand { get; }
        
        /// <summary>
        /// Apply rules command
        /// </summary>
        public ICommand ApplyRulesCommand { get; }
        
        /// <summary>
        /// Refresh power profiles
        /// </summary>
        private void RefreshProfiles()
        {
            if (_powerProfileService == null)
            {
                return;
            }
            
            Profiles.Clear();
            
            var profiles = _powerProfileService.GetAllProfiles();
            
            // Apply filter
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                profiles = profiles.Where(p => 
                    p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) || 
                    p.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToArray();
            }
            
            foreach (var profile in profiles)
            {
                Profiles.Add(profile);
            }
            
            // Reset selection
            SelectedProfile = null;
        }
        
        /// <summary>
        /// Create a new power profile
        /// </summary>
        private void CreateNewProfile()
        {
            var newProfile = new PowerProfile
            {
                Name = "New Profile",
                Description = "A new power profile",
                IsBundled = false,
                LastModified = DateTime.Now,
                Guid = System.Guid.NewGuid().ToString()
            };
            
            if (_powerProfileService != null)
            {
                bool success = _powerProfileService.SaveProfile(newProfile);
                
                var notification = ServiceLocator.Resolve<INotificationService>();
                if (success)
                {
                    Profiles.Add(newProfile);
                    SelectedProfile = newProfile;
                    
                    notification?.ShowSuccess("New profile created successfully.", "Profile Created");
                }
                else
                {
                    notification?.ShowError("Failed to create new profile.", "Error");
                }
            }
        }
        
        /// <summary>
        /// Clone the selected power profile
        /// </summary>
        private void CloneProfile()
        {
            if (SelectedProfile == null || _powerProfileService == null)
            {
                return;
            }
            
            var clonedProfile = SelectedProfile.Clone();
            bool success = _powerProfileService.SaveProfile(clonedProfile);
            
            var notification = ServiceLocator.Resolve<INotificationService>();
            if (success)
            {
                Profiles.Add(clonedProfile);
                SelectedProfile = clonedProfile;
                
                notification?.ShowSuccess($"Profile '{SelectedProfile.Name}' cloned successfully.", "Profile Cloned");
            }
            else
            {
                notification?.ShowError($"Failed to clone profile '{SelectedProfile.Name}'.", "Error");
            }
        }
        
        /// <summary>
        /// Delete the selected power profile
        /// </summary>
        private void DeleteProfile()
        {
            if (SelectedProfile == null || _powerProfileService == null)
            {
                return;
            }
            
            if (SelectedProfile.IsBundled)
            {
                var notification = ServiceLocator.Resolve<INotificationService>();
                notification?.ShowError("Cannot delete bundled profiles.", "Error");
                return;
            }
            
            var notification = ServiceLocator.Resolve<INotificationService>();
            bool confirm = notification?.ShowConfirmation($"Are you sure you want to delete profile '{SelectedProfile.Name}'?", "Confirm Deletion") ?? false;
            
            if (!confirm)
            {
                return;
            }
            
            bool success = _powerProfileService.DeleteProfile(SelectedProfile);
            
            if (success)
            {
                Profiles.Remove(SelectedProfile);
                SelectedProfile = null;
                
                notification?.ShowSuccess("Profile deleted successfully.", "Profile Deleted");
            }
            else
            {
                notification?.ShowError("Failed to delete profile.", "Error");
            }
        }
        
        /// <summary>
        /// Import a power profile
        /// </summary>
        private void ImportProfile()
        {
            if (_fileDialogService == null || _powerProfileService == null)
            {
                return;
            }
            
            string filePath = _fileDialogService.OpenFileDialog("Select Profile", "Power Profile (*.pow)|*.pow");
            
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return;
            }
            
            var profile = _powerProfileService.LoadProfile(filePath);
            
            var notification = ServiceLocator.Resolve<INotificationService>();
            if (profile != null)
            {
                Profiles.Add(profile);
                SelectedProfile = profile;
                
                notification?.ShowSuccess($"Profile '{profile.Name}' imported successfully.", "Profile Imported");
            }
            else
            {
                notification?.ShowError("Failed to import profile.", "Error");
            }
        }
        
        /// <summary>
        /// Export the selected power profile
        /// </summary>
        private void ExportProfile()
        {
            if (SelectedProfile == null || _fileDialogService == null || _powerProfileService == null)
            {
                return;
            }
            
            string filePath = _fileDialogService.SaveFileDialog("Export Profile", "Power Profile (*.pow)|*.pow", SelectedProfile.Name);
            
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }
            
            bool success = _powerProfileService.SaveProfileToFile(SelectedProfile, filePath);
            
            var notification = ServiceLocator.Resolve<INotificationService>();
            if (success)
            {
                notification?.ShowSuccess($"Profile '{SelectedProfile.Name}' exported successfully.", "Profile Exported");
            }
            else
            {
                notification?.ShowError($"Failed to export profile '{SelectedProfile.Name}'.", "Error");
            }
        }
        
        /// <summary>
        /// Add a new rule to the selected profile
        /// </summary>
        private void AddRule()
        {
            if (SelectedProfile == null)
            {
                return;
            }
            
            var newRule = new ProcessAffinityRule
            {
                Name = "New Rule",
                ProcessNamePattern = ".*",
                ProcessPriority = ProcessPriority.Normal,
                IsEnabled = true
            };
            
            SelectedProfile.AffinityRules.Add(newRule);
            Rules.Add(newRule);
            SelectedRule = newRule;
        }
        
        /// <summary>
        /// Remove the selected rule from the profile
        /// </summary>
        private void RemoveRule()
        {
            if (SelectedProfile == null || SelectedRule == null)
            {
                return;
            }
            
            var notification = ServiceLocator.Resolve<INotificationService>();
            bool confirm = notification?.ShowConfirmation($"Are you sure you want to remove rule '{SelectedRule.Name}'?", "Confirm Removal") ?? false;
            
            if (!confirm)
            {
                return;
            }
            
            SelectedProfile.AffinityRules.Remove(SelectedRule);
            Rules.Remove(SelectedRule);
            SelectedRule = null;
        }
        
        /// <summary>
        /// Save the selected profile
        /// </summary>
        private void SaveProfile()
        {
            if (SelectedProfile == null || _powerProfileService == null)
            {
                return;
            }
            
            if (SelectedProfile.IsBundled)
            {
                var notification = ServiceLocator.Resolve<INotificationService>();
                notification?.ShowError("Cannot modify bundled profiles. Please clone first.", "Error");
                return;
            }
            
            // Update last modified
            SelectedProfile.LastModified = DateTime.Now;
            
            bool success = _powerProfileService.SaveProfile(SelectedProfile);
            
            var notification = ServiceLocator.Resolve<INotificationService>();
            if (success)
            {
                notification?.ShowSuccess($"Profile '{SelectedProfile.Name}' saved successfully.", "Profile Saved");
            }
            else
            {
                notification?.ShowError($"Failed to save profile '{SelectedProfile.Name}'.", "Error");
            }
        }
        
        /// <summary>
        /// Apply the rules of the selected profile
        /// </summary>
        private void ApplyRules()
        {
            if (SelectedProfile == null || _powerProfileService == null)
            {
                return;
            }
            
            bool success = _powerProfileService.ApplyProfile(SelectedProfile);
            
            var notification = ServiceLocator.Resolve<INotificationService>();
            if (success)
            {
                notification?.ShowSuccess($"Profile '{SelectedProfile.Name}' applied successfully.", "Profile Applied");
            }
            else
            {
                notification?.ShowError($"Failed to apply profile '{SelectedProfile.Name}'.", "Error");
            }
        }
        
        /// <summary>
        /// Load rules for the selected profile
        /// </summary>
        private void LoadRulesForProfile()
        {
            Rules.Clear();
            
            if (SelectedProfile != null)
            {
                foreach (var rule in SelectedProfile.AffinityRules)
                {
                    Rules.Add(rule);
                }
            }
        }
    }
}