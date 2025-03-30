using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using ThreadPilot.Helpers;
using ThreadPilot.Models;
using ThreadPilot.Services;

namespace ThreadPilot.ViewModels
{
    /// <summary>
    /// View model for the profile editor view
    /// </summary>
    public class ProfileEditorViewModel : ViewModelBase
    {
        private readonly IPowerProfileService _powerProfileService;
        private readonly IFileDialogService _fileDialogService;
        private readonly INotificationService _notificationService;
        
        private BundledPowerProfile _currentProfile = new BundledPowerProfile();
        private bool _isEditMode;
        private string _profileName = string.Empty;
        private string _profileDescription = string.Empty;
        private string _profileAuthor = string.Empty;
        
        /// <summary>
        /// Current profile being edited
        /// </summary>
        public BundledPowerProfile CurrentProfile
        {
            get => _currentProfile;
            set
            {
                SetProperty(ref _currentProfile, value);
                UpdateUIFromProfile();
            }
        }
        
        /// <summary>
        /// Whether the profile is in edit mode
        /// </summary>
        public bool IsEditMode
        {
            get => _isEditMode;
            set => SetProperty(ref _isEditMode, value);
        }
        
        /// <summary>
        /// Profile name
        /// </summary>
        public string ProfileName
        {
            get => _profileName;
            set => SetProperty(ref _profileName, value);
        }
        
        /// <summary>
        /// Profile description
        /// </summary>
        public string ProfileDescription
        {
            get => _profileDescription;
            set => SetProperty(ref _profileDescription, value);
        }
        
        /// <summary>
        /// Profile author
        /// </summary>
        public string ProfileAuthor
        {
            get => _profileAuthor;
            set => SetProperty(ref _profileAuthor, value);
        }
        
        /// <summary>
        /// Process rules in the profile
        /// </summary>
        public ObservableCollection<ProcessAffinityRule> ProcessRules { get; } = new ObservableCollection<ProcessAffinityRule>();
        
        // Commands
        /// <summary>
        /// Command to create a new profile
        /// </summary>
        public ICommand NewProfileCommand { get; }
        
        /// <summary>
        /// Command to save the profile
        /// </summary>
        public ICommand SaveProfileCommand { get; }
        
        /// <summary>
        /// Command to export the profile
        /// </summary>
        public ICommand ExportProfileCommand { get; }
        
        /// <summary>
        /// Command to add a process rule
        /// </summary>
        public ICommand AddRuleCommand { get; }
        
        /// <summary>
        /// Command to remove a process rule
        /// </summary>
        public ICommand RemoveRuleCommand { get; }
        
        /// <summary>
        /// Constructor
        /// </summary>
        public ProfileEditorViewModel()
        {
            // Get services
            _powerProfileService = ServiceLocator.Get<IPowerProfileService>();
            _fileDialogService = ServiceLocator.Get<IFileDialogService>();
            _notificationService = ServiceLocator.Get<INotificationService>();
            
            // Initialize commands
            NewProfileCommand = new RelayCommand(_ => CreateNewProfile());
            SaveProfileCommand = new RelayCommand(_ => SaveProfile(), _ => CanSaveProfile());
            ExportProfileCommand = new RelayCommand(_ => ExportProfile(), _ => CanExportProfile());
            AddRuleCommand = new RelayCommand(_ => AddProcessRule());
            RemoveRuleCommand = new RelayCommand(RemoveProcessRule, CanRemoveProcessRule);
        }
        
        /// <summary>
        /// Initialize the view model
        /// </summary>
        public override void Initialize()
        {
            // Create a new profile by default
            CreateNewProfile();
        }
        
        /// <summary>
        /// Create a new profile
        /// </summary>
        private void CreateNewProfile()
        {
            CurrentProfile = new BundledPowerProfile
            {
                Name = "New Profile",
                Description = "A custom power profile for optimizing system performance",
                Author = Environment.UserName,
                CreationDate = DateTime.Now
            };
            
            IsEditMode = true;
        }
        
        /// <summary>
        /// Update UI fields from the profile
        /// </summary>
        private void UpdateUIFromProfile()
        {
            ProfileName = CurrentProfile.Name;
            ProfileDescription = CurrentProfile.Description;
            ProfileAuthor = CurrentProfile.Author;
            
            // Update process rules
            ProcessRules.Clear();
            foreach (var rule in CurrentProfile.ProcessRules)
            {
                ProcessRules.Add(rule);
            }
        }
        
        /// <summary>
        /// Update the profile from UI fields
        /// </summary>
        private void UpdateProfileFromUI()
        {
            CurrentProfile.Name = ProfileName;
            CurrentProfile.Description = ProfileDescription;
            CurrentProfile.Author = ProfileAuthor;
            
            // Update process rules
            CurrentProfile.ProcessRules.Clear();
            foreach (var rule in ProcessRules)
            {
                CurrentProfile.ProcessRules.Add(rule);
            }
        }
        
        /// <summary>
        /// Check if the profile can be saved
        /// </summary>
        private bool CanSaveProfile()
        {
            return IsEditMode && !string.IsNullOrWhiteSpace(ProfileName);
        }
        
        /// <summary>
        /// Save the current profile
        /// </summary>
        private void SaveProfile()
        {
            try
            {
                UpdateProfileFromUI();
                
                // TODO: Save the profile to the profile service
                
                _notificationService.ShowSuccess($"Profile '{CurrentProfile.Name}' saved successfully");
                IsEditMode = false;
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error saving profile: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Check if the profile can be exported
        /// </summary>
        private bool CanExportProfile()
        {
            return !string.IsNullOrWhiteSpace(ProfileName);
        }
        
        /// <summary>
        /// Export the profile to a file
        /// </summary>
        private void ExportProfile()
        {
            try
            {
                UpdateProfileFromUI();
                
                var filePath = _fileDialogService.SaveFile(
                    "Power Profiles (*.pow)|*.pow", 
                    "Export Power Profile",
                    $"{ProfileName}.pow");
                    
                if (string.IsNullOrEmpty(filePath)) return;
                
                var result = _powerProfileService.ExportProfile(CurrentProfile, filePath);
                if (result)
                {
                    _notificationService.ShowSuccess($"Profile exported to {filePath}");
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
        /// Add a new process rule
        /// </summary>
        private void AddProcessRule()
        {
            var rule = new ProcessAffinityRule
            {
                ProcessNamePattern = "app.exe",
                AffinityMask = (1 << Environment.ProcessorCount) - 1, // Default to all cores
                Priority = ProcessPriorityClass.Normal,
                AutoApply = true
            };
            
            ProcessRules.Add(rule);
        }
        
        /// <summary>
        /// Check if a process rule can be removed
        /// </summary>
        private bool CanRemoveProcessRule(object? parameter)
        {
            return parameter is ProcessAffinityRule;
        }
        
        /// <summary>
        /// Remove a process rule
        /// </summary>
        private void RemoveProcessRule(object? parameter)
        {
            if (parameter is ProcessAffinityRule rule)
            {
                ProcessRules.Remove(rule);
            }
        }
    }
}