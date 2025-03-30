using System;
using System.Windows.Input;

namespace ThreadPilot.Commands
{
    /// <summary>
    /// Relay command implementation of ICommand
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="execute">Execute action</param>
        public RelayCommand(Action<object> execute)
            : this(execute, null)
        {
        }
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="execute">Execute action</param>
        /// <param name="canExecute">Can execute predicate</param>
        public RelayCommand(Action<object> execute, Predicate<object> canExecute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }
        
        /// <summary>
        /// Can execute changed event
        /// </summary>
        public event EventHandler CanExecuteChanged;
        
        /// <summary>
        /// Determines if the command can execute in its current state
        /// </summary>
        /// <param name="parameter">Command parameter</param>
        /// <returns>True if the command can execute, false otherwise</returns>
        public bool CanExecute(object parameter)
        {
            return _canExecute?.Invoke(parameter) ?? true;
        }
        
        /// <summary>
        /// Executes the command
        /// </summary>
        /// <param name="parameter">Command parameter</param>
        public void Execute(object parameter)
        {
            _execute(parameter);
        }
        
        /// <summary>
        /// Raises the CanExecuteChanged event
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            var handler = CanExecuteChanged;
            handler?.Invoke(this, EventArgs.Empty);
        }
    }
}