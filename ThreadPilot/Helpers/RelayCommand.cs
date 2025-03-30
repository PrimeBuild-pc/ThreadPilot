using System;
using System.Windows.Input;

namespace ThreadPilot.Helpers
{
    /// <summary>
    /// Implementation of ICommand that delegates Execute and CanExecute to provided Action and Func
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;
        
        /// <summary>
        /// Event that is fired when the command's ability to execute changes
        /// </summary>
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="execute">Action to execute when the command is invoked</param>
        /// <param name="canExecute">Function to determine if the command can execute</param>
        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }
        
        /// <summary>
        /// Determines if the command can execute
        /// </summary>
        public bool CanExecute(object? parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }
        
        /// <summary>
        /// Executes the command
        /// </summary>
        public void Execute(object? parameter)
        {
            _execute(parameter);
        }
        
        /// <summary>
        /// Raises the CanExecuteChanged event
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}