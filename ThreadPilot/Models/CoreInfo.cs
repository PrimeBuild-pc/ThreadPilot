using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ThreadPilot.Models
{
    public class CoreInfo : INotifyPropertyChanged
    {
        private int _coreNumber;
        private bool _isSelected;

        public int CoreNumber
        {
            get => _coreNumber;
            set
            {
                _coreNumber = value;
                OnPropertyChanged();
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
