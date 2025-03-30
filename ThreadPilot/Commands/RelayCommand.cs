using System;
using System.Windows.Input;

namespace ThreadPilot.Commands
{
    /// <summary>
    /// A command that relays its functionality to other objects by invoking delegates
    /// </summary>
    public class RelayCommand : ICommand
    {
        /// <summary>
        /// The action to execute
        /// </summary>
        private readonly Action<object> _execute;
        
        /// <summary>
        /// The function that determines if the command can execute
        /// </summary>
        private readonly Predicate<object> _canExecute;
        
        /// <summary>
        /// Event fired when the ability to execute the command changes
        /// </summary>
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
        
        /// <summary>
        /// Create a new command that can always execute
        /// </summary>
        /// <param name="execute">The action to execute</param>
        public RelayCommand(Action<object> execute) 
            : this(execute, null)
        {
        }
        
        /// <summary>
        /// Create a new command with conditional execution
        /// </summary>
        /// <param name="execute">The action to execute</param>
        /// <param name="canExecute">The function that determines if the command can execute</param>
        public RelayCommand(Action<object> execute, Predicate<object> canExecute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }
        
        /// <summary>
        /// Determine if the command can execute
        /// </summary>
        /// <param name="parameter">The parameter for the command</param>
        /// <returns>True if the command can execute</returns>
        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }
        
        /// <summary>
        /// Execute the command
        /// </summary>
        /// <param name="parameter">The parameter for the command</param>
        public void Execute(object parameter)
        {
            _execute(parameter);
        }
        
        /// <summary>
        /// Raise the CanExecuteChanged event
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
    
    /// <summary>
    /// A generic command that relays its functionality to other objects by invoking delegates
    /// </summary>
    /// <typeparam name="T">The type of the command parameter</typeparam>
    public class RelayCommand<T> : ICommand
    {
        /// <summary>
        /// The action to execute
        /// </summary>
        private readonly Action<T> _execute;
        
        /// <summary>
        /// The function that determines if the command can execute
        /// </summary>
        private readonly Predicate<T> _canExecute;
        
        /// <summary>
        /// Event fired when the ability to execute the command changes
        /// </summary>
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
        
        /// <summary>
        /// Create a new command that can always execute
        /// </summary>
        /// <param name="execute">The action to execute</param>
        public RelayCommand(Action<T> execute) 
            : this(execute, null)
        {
        }
        
        /// <summary>
        /// Create a new command with conditional execution
        /// </summary>
        /// <param name="execute">The action to execute</param>
        /// <param name="canExecute">The function that determines if the command can execute</param>
        public RelayCommand(Action<T> execute, Predicate<T> canExecute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }
        
        /// <summary>
        /// Determine if the command can execute
        /// </summary>
        /// <param name="parameter">The parameter for the command</param>
        /// <returns>True if the command can execute</returns>
        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute((T)parameter);
        }
        
        /// <summary>
        /// Execute the command
        /// </summary>
        /// <param name="parameter">The parameter for the command</param>
        public void Execute(object parameter)
        {
            _execute((T)parameter);
        }
        
        /// <summary>
        /// Raise the CanExecuteChanged event
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}