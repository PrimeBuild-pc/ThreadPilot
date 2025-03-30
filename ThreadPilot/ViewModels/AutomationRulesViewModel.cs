using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using ThreadPilot.Helpers;
using ThreadPilot.Models;
using ThreadPilot.Services;

namespace ThreadPilot.ViewModels
{
    /// <summary>
    /// ViewModel for the AutomationRules view
    /// </summary>
    public class AutomationRulesViewModel : ViewModelBase
    {
        private readonly INotificationService _notificationService;
        private ObservableCollection<AutomationRule> _automationRules;
        private AutomationRule _selectedRule;
        private bool _isAutomationEnabled;

        /// <summary>
        /// Gets or sets the collection of automation rules
        /// </summary>
        public ObservableCollection<AutomationRule> AutomationRules
        {
            get => _automationRules;
            set
            {
                _automationRules = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets or sets the selected automation rule
        /// </summary>
        public AutomationRule SelectedRule
        {
            get => _selectedRule;
            set
            {
                _selectedRule = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether automation is enabled
        /// </summary>
        public bool IsAutomationEnabled
        {
            get => _isAutomationEnabled;
            set
            {
                _isAutomationEnabled = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Command to add a new automation rule
        /// </summary>
        public ICommand AddRuleCommand { get; }

        /// <summary>
        /// Command to remove the selected automation rule
        /// </summary>
        public ICommand RemoveRuleCommand { get; }

        /// <summary>
        /// Command to edit the selected automation rule
        /// </summary>
        public ICommand EditRuleCommand { get; }

        /// <summary>
        /// Command to toggle the enabled state of the selected automation rule
        /// </summary>
        public ICommand ToggleRuleCommand { get; }

        /// <summary>
        /// Initializes a new instance of the AutomationRulesViewModel class
        /// </summary>
        public AutomationRulesViewModel()
        {
            // Get services
            _notificationService = ServiceLocator.GetService<INotificationService>();

            // Initialize commands
            AddRuleCommand = new RelayCommand(ExecuteAddRule);
            RemoveRuleCommand = new RelayCommand(ExecuteRemoveRule, CanExecuteRemoveRule);
            EditRuleCommand = new RelayCommand(ExecuteEditRule, CanExecuteEditRule);
            ToggleRuleCommand = new RelayCommand(ExecuteToggleRule, CanExecuteToggleRule);

            // Initialize properties
            AutomationRules = new ObservableCollection<AutomationRule>();
            IsAutomationEnabled = true;

            // Load automation rules
            LoadAutomationRules();
        }

        /// <summary>
        /// Loads automation rules
        /// </summary>
        private void LoadAutomationRules()
        {
            try
            {
                // In a real implementation, this would load rules from a file or database
                AutomationRules.Clear();

                // Add sample rules
                AutomationRules.Add(new AutomationRule
                {
                    Name = "Gaming Mode",
                    ProcessName = "steam.exe",
                    ProfileName = "Gaming Mode",
                    TriggerType = TriggerType.ProcessStart,
                    IsEnabled = true
                });

                AutomationRules.Add(new AutomationRule
                {
                    Name = "Development Mode",
                    ProcessName = "devenv.exe",
                    ProfileName = "Ultimate Performance",
                    TriggerType = TriggerType.ProcessStart,
                    IsEnabled = true
                });

                AutomationRules.Add(new AutomationRule
                {
                    Name = "Power Saving Mode",
                    ProcessName = "netflix.exe",
                    ProfileName = "Power Saver",
                    TriggerType = TriggerType.ProcessStart,
                    IsEnabled = false
                });

                AutomationRules.Add(new AutomationRule
                {
                    Name = "Balanced Mode",
                    ProcessName = "",
                    ProfileName = "Balanced",
                    TriggerType = TriggerType.SystemIdle,
                    IsEnabled = true
                });
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error loading automation rules: {ex.Message}");
            }
        }

        /// <summary>
        /// Executes the add rule command
        /// </summary>
        private void ExecuteAddRule()
        {
            try
            {
                // In a real implementation, this would show a dialog to add a new rule
                _notificationService.ShowInfo("Add rule functionality is not implemented yet");
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error adding rule: {ex.Message}");
            }
        }

        /// <summary>
        /// Determines whether the remove rule command can be executed
        /// </summary>
        /// <returns>True if the command can be executed, false otherwise</returns>
        private bool CanExecuteRemoveRule()
        {
            return SelectedRule != null;
        }

        /// <summary>
        /// Executes the remove rule command
        /// </summary>
        private void ExecuteRemoveRule()
        {
            try
            {
                if (SelectedRule == null)
                    return;

                // In a real implementation, this would confirm before removing
                _notificationService.ShowInfo($"Remove rule functionality for '{SelectedRule.Name}' is not implemented yet");
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error removing rule: {ex.Message}");
            }
        }

        /// <summary>
        /// Determines whether the edit rule command can be executed
        /// </summary>
        /// <returns>True if the command can be executed, false otherwise</returns>
        private bool CanExecuteEditRule()
        {
            return SelectedRule != null;
        }

        /// <summary>
        /// Executes the edit rule command
        /// </summary>
        private void ExecuteEditRule()
        {
            try
            {
                if (SelectedRule == null)
                    return;

                // In a real implementation, this would show a dialog to edit the rule
                _notificationService.ShowInfo($"Edit rule functionality for '{SelectedRule.Name}' is not implemented yet");
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error editing rule: {ex.Message}");
            }
        }

        /// <summary>
        /// Determines whether the toggle rule command can be executed
        /// </summary>
        /// <returns>True if the command can be executed, false otherwise</returns>
        private bool CanExecuteToggleRule()
        {
            return SelectedRule != null;
        }

        /// <summary>
        /// Executes the toggle rule command
        /// </summary>
        private void ExecuteToggleRule()
        {
            try
            {
                if (SelectedRule == null)
                    return;

                SelectedRule.IsEnabled = !SelectedRule.IsEnabled;

                if (SelectedRule.IsEnabled)
                {
                    _notificationService.ShowSuccess($"Rule '{SelectedRule.Name}' has been enabled");
                }
                else
                {
                    _notificationService.ShowSuccess($"Rule '{SelectedRule.Name}' has been disabled");
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error toggling rule: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Represents an automation rule
    /// </summary>
    public class AutomationRule : ViewModelBase
    {
        private string _name;
        private string _processName;
        private string _profileName;
        private TriggerType _triggerType;
        private bool _isEnabled;

        /// <summary>
        /// Gets or sets the name of the rule
        /// </summary>
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets or sets the process name
        /// </summary>
        public string ProcessName
        {
            get => _processName;
            set
            {
                _processName = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets or sets the profile name
        /// </summary>
        public string ProfileName
        {
            get => _profileName;
            set
            {
                _profileName = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets or sets the trigger type
        /// </summary>
        public TriggerType TriggerType
        {
            get => _triggerType;
            set
            {
                _triggerType = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the rule is enabled
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets the trigger description
        /// </summary>
        public string TriggerDescription
        {
            get
            {
                switch (TriggerType)
                {
                    case TriggerType.ProcessStart:
                        return $"When '{ProcessName}' starts";
                    case TriggerType.ProcessEnd:
                        return $"When '{ProcessName}' ends";
                    case TriggerType.SystemIdle:
                        return "When system is idle";
                    case TriggerType.SystemStartup:
                        return "On system startup";
                    default:
                        return "Unknown trigger";
                }
            }
        }

        /// <summary>
        /// Gets the action description
        /// </summary>
        public string ActionDescription => $"Apply '{ProfileName}' profile";
    }

    /// <summary>
    /// Enumeration of trigger types
    /// </summary>
    public enum TriggerType
    {
        /// <summary>
        /// Triggers when a process starts
        /// </summary>
        ProcessStart,

        /// <summary>
        /// Triggers when a process ends
        /// </summary>
        ProcessEnd,

        /// <summary>
        /// Triggers when the system is idle
        /// </summary>
        SystemIdle,

        /// <summary>
        /// Triggers on system startup
        /// </summary>
        SystemStartup
    }
}