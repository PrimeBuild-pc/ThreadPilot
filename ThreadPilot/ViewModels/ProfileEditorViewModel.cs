using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using ThreadPilot.Commands;
using ThreadPilot.Models;
using ThreadPilot.Services;

namespace ThreadPilot.ViewModels
{
    /// <summary>
    /// View model for the profile editor
    /// </summary>
    public class ProfileEditorViewModel : ViewModelBase
    {
        private readonly IFileDialogService _fileDialogService;
        private readonly IPowerProfileService _powerProfileService;
        private readonly INotificationService _notificationService;
        
        private PowerProfile _currentProfile = new PowerProfile();
        private ProcessAffinityRule? _selectedRule;
        private string _newProcessPattern = string.Empty;
        
        /// <summary>
        /// Constructor
        /// </summary>
        public ProfileEditorViewModel()
        {
            _fileDialogService = ServiceLocator.Get<IFileDialogService>();
            _powerProfileService = ServiceLocator.Get<IPowerProfileService>();
            _notificationService = ServiceLocator.Get<INotificationService>();
            
            // Initialize commands
            SaveProfileCommand = new RelayCommand(SaveProfile, CanSaveProfile);
            ExportProfileCommand = new RelayCommand(ExportProfile, CanExportProfile);
            AddRuleCommand = new RelayCommand(AddRule, CanAddRule);
            RemoveRuleCommand = new RelayCommand(RemoveRule, CanRemoveRule);
            MoveRuleUpCommand = new RelayCommand(MoveRuleUp, CanMoveRuleUp);
            MoveRuleDownCommand = new RelayCommand(MoveRuleDown, CanMoveRuleDown);
            ApplyProfileCommand = new RelayCommand(ApplyProfile, CanApplyProfile);
        }
        
        /// <summary>
        /// Command to save the profile
        /// </summary>
        public ICommand SaveProfileCommand { get; }
        
        /// <summary>
        /// Command to export the profile
        /// </summary>
        public ICommand ExportProfileCommand { get; }
        
        /// <summary>
        /// Command to add a rule
        /// </summary>
        public ICommand AddRuleCommand { get; }
        
        /// <summary>
        /// Command to remove a rule
        /// </summary>
        public ICommand RemoveRuleCommand { get; }
        
        /// <summary>
        /// Command to move a rule up
        /// </summary>
        public ICommand MoveRuleUpCommand { get; }
        
        /// <summary>
        /// Command to move a rule down
        /// </summary>
        public ICommand MoveRuleDownCommand { get; }
        
        /// <summary>
        /// Command to apply the profile
        /// </summary>
        public ICommand ApplyProfileCommand { get; }
        
        /// <summary>
        /// Current profile being edited
        /// </summary>
        public PowerProfile CurrentProfile
        {
            get => _currentProfile;
            private set => SetProperty(ref _currentProfile, value);
        }
        
        /// <summary>
        /// Selected rule in the rules list
        /// </summary>
        public ProcessAffinityRule? SelectedRule
        {
            get => _selectedRule;
            set
            {
                if (SetProperty(ref _selectedRule, value))
                {
                    ((RelayCommand)RemoveRuleCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)MoveRuleUpCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)MoveRuleDownCommand).RaiseCanExecuteChanged();
                }
            }
        }
        
        /// <summary>
        /// Process pattern for new rules
        /// </summary>
        public string NewProcessPattern
        {
            get => _newProcessPattern;
            set
            {
                if (SetProperty(ref _newProcessPattern, value))
                {
                    ((RelayCommand)AddRuleCommand).RaiseCanExecuteChanged();
                }
            }
        }
        
        /// <summary>
        /// Rules collection for the current profile
        /// </summary>
        public ObservableCollection<ProcessAffinityRule> Rules => new ObservableCollection<ProcessAffinityRule>(_currentProfile.ProcessRules);
        
        /// <summary>
        /// Available process priorities
        /// </summary>
        public ProcessPriority[] Priorities => Enum.GetValues<ProcessPriority>();
        
        /// <summary>
        /// Load a profile for editing
        /// </summary>
        /// <param name="profile">Profile to load</param>
        public void LoadProfile(PowerProfile profile)
        {
            // Create a deep copy to avoid modifying the original
            CurrentProfile = profile.Clone();
            
            // Reset selection
            SelectedRule = null;
            NewProcessPattern = string.Empty;
            
            // Refresh command states
            ((RelayCommand)SaveProfileCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ExportProfileCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ApplyProfileCommand).RaiseCanExecuteChanged();
            
            // Notify UI to refresh the rules collection
            OnPropertyChanged(nameof(Rules));
        }
        
        /// <summary>
        /// Save the current profile
        /// </summary>
        private void SaveProfile()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(CurrentProfile.Name))
                {
                    _notificationService.ShowError("Profile name cannot be empty");
                    return;
                }
                
                if (_powerProfileService.SaveProfile(CurrentProfile))
                {
                    _notificationService.ShowSuccess($"Profile '{CurrentProfile.Name}' saved successfully");
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error saving profile: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Export the current profile
        /// </summary>
        private void ExportProfile()
        {
            try
            {
                string? filePath = _fileDialogService.ShowSaveFileDialog(
                    "Export Power Profile",
                    "JSON Files (*.json)|*.json|POW Files (*.pow)|*.pow|All Files (*.*)|*.*",
                    $"{CurrentProfile.Name}.json");
                
                if (!string.IsNullOrEmpty(filePath))
                {
                    if (_powerProfileService.ExportProfile(CurrentProfile, filePath))
                    {
                        _notificationService.ShowSuccess($"Profile exported to {filePath}");
                    }
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error exporting profile: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Add a new rule to the profile
        /// </summary>
        private void AddRule()
        {
            try
            {
                var newRule = new ProcessAffinityRule
                {
                    ProcessNamePattern = NewProcessPattern,
                    Priority = ProcessPriority.Normal
                };
                
                CurrentProfile.ProcessRules.Add(newRule);
                SelectedRule = newRule;
                NewProcessPattern = string.Empty;
                
                // Notify UI to refresh the rules collection
                OnPropertyChanged(nameof(Rules));
                
                _notificationService.ShowSuccess($"Rule for '{newRule.ProcessNamePattern}' added");
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error adding rule: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Remove the selected rule
        /// </summary>
        private void RemoveRule()
        {
            try
            {
                if (_selectedRule != null)
                {
                    string pattern = _selectedRule.ProcessNamePattern;
                    CurrentProfile.ProcessRules.Remove(_selectedRule);
                    SelectedRule = null;
                    
                    // Notify UI to refresh the rules collection
                    OnPropertyChanged(nameof(Rules));
                    
                    _notificationService.ShowSuccess($"Rule for '{pattern}' removed");
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error removing rule: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Move the selected rule up in the list
        /// </summary>
        private void MoveRuleUp()
        {
            try
            {
                if (_selectedRule != null)
                {
                    int index = CurrentProfile.ProcessRules.IndexOf(_selectedRule);
                    
                    if (index > 0)
                    {
                        CurrentProfile.ProcessRules.RemoveAt(index);
                        CurrentProfile.ProcessRules.Insert(index - 1, _selectedRule);
                        
                        // Notify UI to refresh the rules collection
                        OnPropertyChanged(nameof(Rules));
                        
                        _notificationService.ShowSuccess("Rule moved up");
                    }
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error moving rule: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Move the selected rule down in the list
        /// </summary>
        private void MoveRuleDown()
        {
            try
            {
                if (_selectedRule != null)
                {
                    int index = CurrentProfile.ProcessRules.IndexOf(_selectedRule);
                    
                    if (index < CurrentProfile.ProcessRules.Count - 1)
                    {
                        CurrentProfile.ProcessRules.RemoveAt(index);
                        CurrentProfile.ProcessRules.Insert(index + 1, _selectedRule);
                        
                        // Notify UI to refresh the rules collection
                        OnPropertyChanged(nameof(Rules));
                        
                        _notificationService.ShowSuccess("Rule moved down");
                    }
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error moving rule: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Apply the current profile
        /// </summary>
        private void ApplyProfile()
        {
            try
            {
                int ruleCount = _powerProfileService.ApplyProfile(CurrentProfile);
                _notificationService.ShowSuccess($"Profile applied: {ruleCount} rules affected processes");
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error applying profile: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Determine if the profile can be saved
        /// </summary>
        /// <returns>True if the profile can be saved</returns>
        private bool CanSaveProfile()
        {
            return !string.IsNullOrWhiteSpace(CurrentProfile.Name);
        }
        
        /// <summary>
        /// Determine if the profile can be exported
        /// </summary>
        /// <returns>True if the profile can be exported</returns>
        private bool CanExportProfile()
        {
            return !string.IsNullOrWhiteSpace(CurrentProfile.Name);
        }
        
        /// <summary>
        /// Determine if a rule can be added
        /// </summary>
        /// <returns>True if a rule can be added</returns>
        private bool CanAddRule()
        {
            return !string.IsNullOrWhiteSpace(NewProcessPattern);
        }
        
        /// <summary>
        /// Determine if the selected rule can be removed
        /// </summary>
        /// <returns>True if the selected rule can be removed</returns>
        private bool CanRemoveRule()
        {
            return SelectedRule != null;
        }
        
        /// <summary>
        /// Determine if the selected rule can be moved up
        /// </summary>
        /// <returns>True if the selected rule can be moved up</returns>
        private bool CanMoveRuleUp()
        {
            if (SelectedRule == null)
            {
                return false;
            }
            
            int index = CurrentProfile.ProcessRules.IndexOf(SelectedRule);
            return index > 0;
        }
        
        /// <summary>
        /// Determine if the selected rule can be moved down
        /// </summary>
        /// <returns>True if the selected rule can be moved down</returns>
        private bool CanMoveRuleDown()
        {
            if (SelectedRule == null)
            {
                return false;
            }
            
            int index = CurrentProfile.ProcessRules.IndexOf(SelectedRule);
            return index < CurrentProfile.ProcessRules.Count - 1;
        }
        
        /// <summary>
        /// Determine if the current profile can be applied
        /// </summary>
        /// <returns>True if the current profile can be applied</returns>
        private bool CanApplyProfile()
        {
            return CurrentProfile.ProcessRules.Count > 0;
        }
    }
}