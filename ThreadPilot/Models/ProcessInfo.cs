using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ThreadPilot.Models
{
    /// <summary>
    /// Represents detailed information about a system process
    /// </summary>
    public class ProcessInfo : INotifyPropertyChanged
    {
        private Process _process;
        private int _id;
        private string _name;
        private string _windowTitle;
        private double _cpuUsage;
        private long _memoryUsage;
        private ProcessPriorityClass _priority;
        private IntPtr _affinityMask;
        private bool _isSelected;

        /// <summary>
        /// Event that fires when a property is changed
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the Process object
        /// </summary>
        public Process Process
        {
            get => _process;
            private set
            {
                if (_process != value)
                {
                    _process = value;
                    OnPropertyChanged(nameof(Process));
                }
            }
        }

        /// <summary>
        /// Gets the process ID
        /// </summary>
        public int Id
        {
            get => _id;
            set
            {
                if (_id != value)
                {
                    _id = value;
                    OnPropertyChanged(nameof(Id));
                }
            }
        }

        /// <summary>
        /// Gets the process name
        /// </summary>
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        /// <summary>
        /// Gets the window title of the process
        /// </summary>
        public string WindowTitle
        {
            get => _windowTitle;
            set
            {
                if (_windowTitle != value)
                {
                    _windowTitle = value;
                    OnPropertyChanged(nameof(WindowTitle));
                }
            }
        }

        /// <summary>
        /// Gets or sets the CPU usage percentage
        /// </summary>
        public double CpuUsage
        {
            get => _cpuUsage;
            set
            {
                if (Math.Abs(_cpuUsage - value) > 0.01)
                {
                    _cpuUsage = value;
                    OnPropertyChanged(nameof(CpuUsage));
                }
            }
        }

        /// <summary>
        /// Gets or sets the memory usage in bytes
        /// </summary>
        public long MemoryUsage
        {
            get => _memoryUsage;
            set
            {
                if (_memoryUsage != value)
                {
                    _memoryUsage = value;
                    OnPropertyChanged(nameof(MemoryUsage));
                    OnPropertyChanged(nameof(MemoryUsageMB));
                }
            }
        }

        /// <summary>
        /// Gets the memory usage in MB
        /// </summary>
        public double MemoryUsageMB => Math.Round((double)MemoryUsage / 1024 / 1024, 1);

        /// <summary>
        /// Gets or sets the process priority class
        /// </summary>
        public ProcessPriorityClass Priority
        {
            get => _priority;
            set
            {
                if (_priority != value)
                {
                    _priority = value;
                    OnPropertyChanged(nameof(Priority));
                }
            }
        }

        /// <summary>
        /// Gets or sets the processor affinity mask
        /// </summary>
        public IntPtr AffinityMask
        {
            get => _affinityMask;
            set
            {
                if (_affinityMask != value)
                {
                    _affinityMask = value;
                    OnPropertyChanged(nameof(AffinityMask));
                }
            }
        }

        /// <summary>
        /// Gets or sets whether this process is selected in the UI
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        /// <summary>
        /// Creates a new instance of ProcessInfo
        /// </summary>
        public ProcessInfo(Process process)
        {
            _process = process;
            _id = process.Id;
            _name = process.ProcessName;
            
            try
            {
                _windowTitle = process.MainWindowTitle;
            }
            catch
            {
                _windowTitle = string.Empty;
            }

            try
            {
                _priority = process.PriorityClass;
            }
            catch
            {
                _priority = ProcessPriorityClass.Normal;
            }

            try
            {
                _affinityMask = process.ProcessorAffinity;
            }
            catch
            {
                _affinityMask = (IntPtr)0;
            }

            try
            {
                _memoryUsage = process.PrivateMemorySize64;
            }
            catch
            {
                _memoryUsage = 0;
            }

            _cpuUsage = 0;
            _isSelected = false;
        }

        /// <summary>
        /// Updates the CPU affinity mask
        /// </summary>
        public bool UpdateAffinity(IntPtr newAffinityMask)
        {
            try
            {
                if (Process != null && Process.ProcessorAffinity != newAffinityMask)
                {
                    Process.ProcessorAffinity = newAffinityMask;
                    AffinityMask = newAffinityMask;
                    return true;
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Updates the process priority
        /// </summary>
        public bool UpdatePriority(ProcessPriorityClass newPriority)
        {
            try
            {
                if (Process != null && Process.PriorityClass != newPriority)
                {
                    Process.PriorityClass = newPriority;
                    Priority = newPriority;
                    return true;
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Refreshes the process information
        /// </summary>
        public void Refresh()
        {
            try
            {
                if (Process != null && !Process.HasExited)
                {
                    Process.Refresh();
                    MemoryUsage = Process.PrivateMemorySize64;
                    
                    try
                    {
                        WindowTitle = Process.MainWindowTitle;
                    }
                    catch
                    {
                        // Ignore errors for window title
                    }

                    try
                    {
                        Priority = Process.PriorityClass;
                    }
                    catch
                    {
                        // Ignore errors for priority
                    }

                    try
                    {
                        AffinityMask = Process.ProcessorAffinity;
                    }
                    catch
                    {
                        // Ignore errors for affinity
                    }
                }
            }
            catch (Exception)
            {
                // Process may have exited or access denied
            }
        }

        /// <summary>
        /// Handles property changed events
        /// </summary>
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}