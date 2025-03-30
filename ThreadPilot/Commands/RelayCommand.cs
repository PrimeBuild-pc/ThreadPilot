using System;
using System.Windows.Input;

namespace ThreadPilot.Commands
{
    /// <summary>
    /// Implementation of ICommand for MVVM pattern
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="execute">Execute action</param>
        /// <param name="canExecute">CanExecute predicate (optional)</param>
        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }
        
        /// <summary>
        /// Constructor with no parameters
        /// </summary>
        /// <param name="execute">Execute action</param>
        /// <param name="canExecute">CanExecute predicate (optional)</param>
        public RelayCommand(Action execute, Func<bool>? canExecute = null)
            : this(_ => execute(), canExecute != null ? _ => canExecute() : null)
        {
        }
        
        /// <summary>
        /// CanExecuteChanged event
        /// </summary>
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
        
        /// <summary>
        /// Check if command can execute
        /// </summary>
        /// <param name="parameter">Command parameter</param>
        /// <returns>True if can execute</returns>
        public bool CanExecute(object? parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }
        
        /// <summary>
        /// Execute command
        /// </summary>
        /// <param name="parameter">Command parameter</param>
        public void Execute(object? parameter)
        {
            _execute(parameter);
        }
        
        /// <summary>
        /// Force command manager to raise requery event
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}