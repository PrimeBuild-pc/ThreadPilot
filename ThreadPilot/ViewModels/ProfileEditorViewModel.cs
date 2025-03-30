using System;
using System.Collections.ObjectModel;
using System.Linq;
using ThreadPilot.Commands;
using ThreadPilot.Models;
using ThreadPilot.Services;

namespace ThreadPilot.ViewModels
{
    /// <summary>
    /// View model for profile editor tab
    /// </summary>
    public class ProfileEditorViewModel : ViewModelBase
    {
        private readonly INotificationService _notificationService;
        private readonly IPowerProfileService _powerProfileService;
        
        private PowerProfile? _currentProfile;
        private ProcessAffinityRule? _selectedRule;
        private string _newProcessPattern = string.Empty;
        
        /// <summary>
        /// Constructor
        /// </summary>
        public ProfileEditorViewModel()
        {
            // Get services
            _notificationService = ServiceLocator.Get<INotificationService>();
            _powerProfileService = ServiceLocator.Get<IPowerProfileService>();
            
            // Initialize rules collection
            Rules = new ObservableCollection<ProcessAffinityRule>();
            
            // Initialize commands
            AddRuleCommand = new RelayCommand(AddRule, CanAddRule);
            RemoveRuleCommand = new RelayCommand(RemoveRule, CanRemoveRule);
            MoveRuleUpCommand = new RelayCommand(MoveRuleUp, CanMoveRuleUp);
            MoveRuleDownCommand = new RelayCommand(MoveRuleDown, CanMoveRuleDown);
            SaveProfileCommand = new RelayCommand(SaveProfile, CanSaveProfile);
            ExportProfileCommand = new RelayCommand(ExportProfile, CanSaveProfile);
            ApplyProfileCommand = new RelayCommand(ApplyProfile, CanSaveProfile);
        }
        
        /// <summary>
        /// Current profile being edited
        /// </summary>
        public PowerProfile? CurrentProfile
        {
            get => _currentProfile;
            set
            {
                if (SetProperty(ref _currentProfile, value))
                {
                    // Update rules collection
                    UpdateRulesCollection();
                    
                    // Update command states
                    (AddRuleCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (SaveProfileCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (ExportProfileCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (ApplyProfileCommand as RelayCommand)?.RaiseCanExecuteChanged();
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
                    // Update command states
                    (RemoveRuleCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (MoveRuleUpCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (MoveRuleDownCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }
        
        /// <summary>
        /// New process pattern
        /// </summary>
        public string NewProcessPattern
        {
            get => _newProcessPattern;
            set
            {
                if (SetProperty(ref _newProcessPattern, value))
                {
                    (AddRuleCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }
        
        /// <summary>
        /// Rules collection
        /// </summary>
        public ObservableCollection<ProcessAffinityRule> Rules { get; }
        
        /// <summary>
        /// Add rule command
        /// </summary>
        public RelayCommand AddRuleCommand { get; }
        
        /// <summary>
        /// Remove rule command
        /// </summary>
        public RelayCommand RemoveRuleCommand { get; }
        
        /// <summary>
        /// Move rule up command
        /// </summary>
        public RelayCommand MoveRuleUpCommand { get; }
        
        /// <summary>
        /// Move rule down command
        /// </summary>
        public RelayCommand MoveRuleDownCommand { get; }
        
        /// <summary>
        /// Save profile command
        /// </summary>
        public RelayCommand SaveProfileCommand { get; }
        
        /// <summary>
        /// Export profile command
        /// </summary>
        public RelayCommand ExportProfileCommand { get; }
        
        /// <summary>
        /// Apply profile command
        /// </summary>
        public RelayCommand ApplyProfileCommand { get; }
        
        /// <summary>
        /// Update rules collection from current profile
        /// </summary>
        private void UpdateRulesCollection()
        {
            // Clear existing rules
            Rules.Clear();
            
            // Add rules from current profile
            if (CurrentProfile != null)
            {
                foreach (var rule in CurrentProfile.ProcessRules)
                {
                    Rules.Add(rule);
                }
            }
        }
        
        /// <summary>
        /// Add a rule
        /// </summary>
        private void AddRule()
        {
            if (CurrentProfile == null || string.IsNullOrWhiteSpace(NewProcessPattern))
            {
                return;
            }
            
            try
            {
                // Create new rule
                var rule = new ProcessAffinityRule
                {
                    ProcessNamePattern = NewProcessPattern,
                    Priority = ProcessPriority.Normal,
                    IsExcludeList = false
                };
                
                // Add to current profile
                CurrentProfile.ProcessRules.Add(rule);
                
                // Add to rules collection
                Rules.Add(rule);
                
                // Clear pattern
                NewProcessPattern = string.Empty;
                
                // Select the new rule
                SelectedRule = rule;
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error adding rule: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Remove a rule
        /// </summary>
        private void RemoveRule()
        {
            if (CurrentProfile == null || SelectedRule == null)
            {
                return;
            }
            
            try
            {
                // Remove from current profile
                CurrentProfile.ProcessRules.Remove(SelectedRule);
                
                // Remove from rules collection
                Rules.Remove(SelectedRule);
                
                // Clear selection
                SelectedRule = null;
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error removing rule: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Move rule up
        /// </summary>
        private void MoveRuleUp()
        {
            if (CurrentProfile == null || SelectedRule == null)
            {
                return;
            }
            
            try
            {
                // Get index of selected rule
                int index = Rules.IndexOf(SelectedRule);
                if (index <= 0)
                {
                    return;
                }
                
                // Remove from current position
                Rules.RemoveAt(index);
                CurrentProfile.ProcessRules.RemoveAt(index);
                
                // Insert at new position
                Rules.Insert(index - 1, SelectedRule);
                CurrentProfile.ProcessRules.Insert(index - 1, SelectedRule);
                
                // Update command states
                (MoveRuleUpCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (MoveRuleDownCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error moving rule: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Move rule down
        /// </summary>
        private void MoveRuleDown()
        {
            if (CurrentProfile == null || SelectedRule == null)
            {
                return;
            }
            
            try
            {
                // Get index of selected rule
                int index = Rules.IndexOf(SelectedRule);
                if (index < 0 || index >= Rules.Count - 1)
                {
                    return;
                }
                
                // Remove from current position
                Rules.RemoveAt(index);
                CurrentProfile.ProcessRules.RemoveAt(index);
                
                // Insert at new position
                Rules.Insert(index + 1, SelectedRule);
                CurrentProfile.ProcessRules.Insert(index + 1, SelectedRule);
                
                // Update command states
                (MoveRuleUpCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (MoveRuleDownCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error moving rule: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Save profile
        /// </summary>
        private void SaveProfile()
        {
            if (CurrentProfile == null)
            {
                return;
            }
            
            try
            {
                // Update profile in service
                if (_powerProfileService.UpdateProfile(CurrentProfile))
                {
                    _notificationService.ShowSuccess($"Profile '{CurrentProfile.Name}' saved.");
                }
                else
                {
                    _notificationService.ShowError($"Failed to save profile '{CurrentProfile.Name}'.");
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error saving profile: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Export profile
        /// </summary>
        private void ExportProfile()
        {
            if (CurrentProfile == null)
            {
                return;
            }
            
            try
            {
                // Export profile from service
                if (_powerProfileService.ExportProfile(CurrentProfile))
                {
                    _notificationService.ShowSuccess($"Profile '{CurrentProfile.Name}' exported.");
                }
                else
                {
                    _notificationService.ShowError($"Failed to export profile '{CurrentProfile.Name}'.");
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error exporting profile: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Apply profile
        /// </summary>
        private void ApplyProfile()
        {
            if (CurrentProfile == null)
            {
                return;
            }
            
            try
            {
                // Apply profile from service
                if (_powerProfileService.ApplyProfile(CurrentProfile))
                {
                    _notificationService.ShowSuccess($"Profile '{CurrentProfile.Name}' applied.");
                }
                else
                {
                    _notificationService.ShowError($"Failed to apply profile '{CurrentProfile.Name}'.");
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error applying profile: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Check if rule can be added
        /// </summary>
        /// <returns>True if can add rule</returns>
        private bool CanAddRule()
        {
            return CurrentProfile != null && !string.IsNullOrWhiteSpace(NewProcessPattern);
        }
        
        /// <summary>
        /// Check if rule can be removed
        /// </summary>
        /// <returns>True if can remove rule</returns>
        private bool CanRemoveRule()
        {
            return CurrentProfile != null && SelectedRule != null;
        }
        
        /// <summary>
        /// Check if rule can be moved up
        /// </summary>
        /// <returns>True if can move up</returns>
        private bool CanMoveRuleUp()
        {
            if (CurrentProfile == null || SelectedRule == null)
            {
                return false;
            }
            
            int index = Rules.IndexOf(SelectedRule);
            return index > 0;
        }
        
        /// <summary>
        /// Check if rule can be moved down
        /// </summary>
        /// <returns>True if can move down</returns>
        private bool CanMoveRuleDown()
        {
            if (CurrentProfile == null || SelectedRule == null)
            {
                return false;
            }
            
            int index = Rules.IndexOf(SelectedRule);
            return index >= 0 && index < Rules.Count - 1;
        }
        
        /// <summary>
        /// Check if profile can be saved
        /// </summary>
        /// <returns>True if can save profile</returns>
        private bool CanSaveProfile()
        {
            return CurrentProfile != null;
        }
    }
}