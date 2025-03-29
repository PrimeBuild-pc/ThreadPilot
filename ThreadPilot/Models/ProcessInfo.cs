using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.CompilerServices;

namespace ThreadPilot.Models
{
    public class ProcessInfo : INotifyPropertyChanged
    {
        private string _name;
        private int _pid;
        private string _description;
        private ProcessPriorityClass _priority;
        private long _affinityMask;
        private double _cpuUsage;
        private double _memoryUsage;
        private byte[] _iconData;

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }

        public int Pid
        {
            get => _pid;
            set
            {
                _pid = value;
                OnPropertyChanged();
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                _description = value;
                OnPropertyChanged();
            }
        }

        public ProcessPriorityClass Priority
        {
            get => _priority;
            set
            {
                _priority = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PriorityDisplay));
            }
        }

        public string PriorityDisplay
        {
            get
            {
                return Priority switch
                {
                    ProcessPriorityClass.Idle => "Idle",
                    ProcessPriorityClass.BelowNormal => "Below Normal",
                    ProcessPriorityClass.Normal => "Normal",
                    ProcessPriorityClass.AboveNormal => "Above Normal",
                    ProcessPriorityClass.High => "High",
                    ProcessPriorityClass.RealTime => "Realtime",
                    _ => "Unknown"
                };
            }
        }

        public long AffinityMask
        {
            get => _affinityMask;
            set
            {
                _affinityMask = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AffinityCoreCount));
            }
        }

        public int AffinityCoreCount
        {
            get
            {
                // Count the number of bits set in the affinity mask
                int count = 0;
                long mask = AffinityMask;
                while (mask > 0)
                {
                    count += (int)(mask & 1);
                    mask >>= 1;
                }
                return count;
            }
        }

        public double CpuUsage
        {
            get => _cpuUsage;
            set
            {
                _cpuUsage = value;
                OnPropertyChanged();
            }
        }

        public double MemoryUsage
        {
            get => _memoryUsage;
            set
            {
                _memoryUsage = value;
                OnPropertyChanged();
            }
        }

        public byte[] IconData
        {
            get => _iconData;
            set
            {
                _iconData = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public override bool Equals(object obj)
        {
            if (obj is ProcessInfo other)
            {
                return Pid == other.Pid;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Pid.GetHashCode();
        }
    }
}
