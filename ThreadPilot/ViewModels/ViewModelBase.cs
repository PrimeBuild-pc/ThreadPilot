using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ThreadPilot.Services;

namespace ThreadPilot.ViewModels
{
    /// <summary>
    /// Base class for all view models
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        /// <summary>
        /// Event fired when a property value changes
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;
        
        /// <summary>
        /// Notification service instance
        /// </summary>
        protected INotificationService NotificationService => ServiceLocator.Get<INotificationService>();
        
        /// <summary>
        /// File dialog service instance
        /// </summary>
        protected IFileDialogService FileDialogService => ServiceLocator.Get<IFileDialogService>();
        
        /// <summary>
        /// Raise the PropertyChanged event
        /// </summary>
        /// <param name="propertyName">Name of the property that changed</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        /// <summary>
        /// Set a property value and raise the PropertyChanged event if the value changed
        /// </summary>
        /// <typeparam name="T">Property type</typeparam>
        /// <param name="storage">Reference to the backing field</param>
        /// <param name="value">New value</param>
        /// <param name="propertyName">Name of the property</param>
        /// <returns>True if the value changed</returns>
        protected virtual bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
            {
                return false;
            }
            
            storage = value;
            OnPropertyChanged(propertyName);
            
            return true;
        }
        
        /// <summary>
        /// Set a property value, raise the PropertyChanged event if the value changed,
        /// and execute an action if the value changed
        /// </summary>
        /// <typeparam name="T">Property type</typeparam>
        /// <param name="storage">Reference to the backing field</param>
        /// <param name="value">New value</param>
        /// <param name="action">Action to execute if the value changed</param>
        /// <param name="propertyName">Name of the property</param>
        /// <returns>True if the value changed</returns>
        protected virtual bool SetProperty<T>(ref T storage, T value, Action action, [CallerMemberName] string propertyName = null)
        {
            if (SetProperty(ref storage, value, propertyName))
            {
                action?.Invoke();
                return true;
            }
            
            return false;
        }
    }
}