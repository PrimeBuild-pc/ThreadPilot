using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ThreadPilot.ViewModels
{
    /// <summary>
    /// Base view model class
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        /// <summary>
        /// Property changed event
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Set property value and raise property changed event if value changes
        /// </summary>
        /// <typeparam name="T">Property type</typeparam>
        /// <param name="field">Field reference</param>
        /// <param name="value">New value</param>
        /// <param name="propertyName">Property name</param>
        /// <returns>True if value changed, false otherwise</returns>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// Raise property changed event
        /// </summary>
        /// <param name="propertyName">Property name</param>
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}