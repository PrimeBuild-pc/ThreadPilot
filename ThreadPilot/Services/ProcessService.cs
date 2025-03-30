using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Implementation of process operations
    /// </summary>
    public class ProcessService : IProcessService
    {
        #region Win32 API imports
        
        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);
        
        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);
        
        [DllImport("kernel32.dll")]
        private static extern bool SetProcessAffinityMask(IntPtr hProcess, IntPtr dwProcessAffinityMask);
        
        [DllImport("kernel32.dll")]
        private static extern bool GetProcessAffinityMask(IntPtr hProcess, out IntPtr lpProcessAffinityMask, out IntPtr lpSystemAffinityMask);
        
        [DllImport("kernel32.dll")]
        private static extern bool SetPriorityClass(IntPtr hProcess, uint dwPriorityClass);
        
        [DllImport("kernel32.dll")]
        private static extern uint GetPriorityClass(IntPtr hProcess);
        
        private const int PROCESS_ALL_ACCESS = 0x1F0FFF;
        
        #endregion
        
        private readonly Dictionary<int, PerformanceCounter> _cpuCounters = new Dictionary<int, PerformanceCounter>();
        private readonly Dictionary<int, double> _lastCpuUsage = new Dictionary<int, double>();
        private readonly Timer _processTrackingTimer;
        private bool _isTracking;
        private readonly object _lockObj = new object();
        
        /// <summary>
        /// Occurs when a process is started
        /// </summary>
        public event EventHandler<ProcessInfo> ProcessStarted;
        
        /// <summary>
        /// Occurs when a process is terminated
        /// </summary>
        public event EventHandler<int> ProcessTerminated;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="ProcessService"/> class
        /// </summary>
        public ProcessService()
        {
            _processTrackingTimer = new Timer(TrackProcesses, null, Timeout.Infinite, Timeout.Infinite);
            
            ProcessCounterCategory.InstanceCreated += OnProcessCounterCategoryInstanceCreated;
            ProcessCounterCategory.InstanceDeleted += OnProcessCounterCategoryInstanceDeleted;
        }
        
        /// <summary>
        /// Gets a process by its ID
        /// </summary>
        /// <param name="processId">The process ID</param>
        /// <returns>The process info or null if not found</returns>
        public ProcessInfo GetProcess(int processId)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                var processInfo = ProcessInfo.FromProcess(process);
                UpdateProcessCpuUsage(processInfo);
                return processInfo;
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// Gets a process by its name
        /// </summary>
        /// <param name="processName">The process name</param>
        /// <returns>The first process with the given name or null if not found</returns>
        public ProcessInfo GetProcessByName(string processName)
        {
            try
            {
                var processes = Process.GetProcessesByName(processName);
                if (processes.Length == 0)
                    return null;
                    
                var processInfo = ProcessInfo.FromProcess(processes[0]);
                UpdateProcessCpuUsage(processInfo);
                return processInfo;
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// Gets all processes
        /// </summary>
        /// <returns>The list of all processes</returns>
        public List<ProcessInfo> GetAllProcesses()
        {
            try
            {
                var processes = Process.GetProcesses();
                var result = new List<ProcessInfo>();
                
                foreach (var process in processes)
                {
                    try
                    {
                        var processInfo = ProcessInfo.FromProcess(process);
                        UpdateProcessCpuUsage(processInfo);
                        result.Add(processInfo);
                    }
                    catch
                    {
                        // Skip processes that cannot be accessed
                    }
                }
                
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to get all processes: {ex.Message}");
                return new List<ProcessInfo>();
            }
        }
        
        /// <summary>
        /// Gets all processes by name
        /// </summary>
        /// <param name="processName">The process name</param>
        /// <returns>The list of processes with the given name</returns>
        public List<ProcessInfo> GetProcessesByName(string processName)
        {
            try
            {
                var processes = Process.GetProcessesByName(processName);
                var result = new List<ProcessInfo>();
                
                foreach (var process in processes)
                {
                    try
                    {
                        var processInfo = ProcessInfo.FromProcess(process);
                        UpdateProcessCpuUsage(processInfo);
                        result.Add(processInfo);
                    }
                    catch
                    {
                        // Skip processes that cannot be accessed
                    }
                }
                
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to get processes by name: {ex.Message}");
                return new List<ProcessInfo>();
            }
        }
        
        /// <summary>
        /// Gets the CPU usage of a process
        /// </summary>
        /// <param name="processId">The process ID</param>
        /// <returns>The CPU usage percentage</returns>
        public double GetProcessCpuUsage(int processId)
        {
            try
            {
                lock (_lockObj)
                {
                    if (_lastCpuUsage.TryGetValue(processId, out var usage))
                    {
                        return usage;
                    }
                    
                    if (!_cpuCounters.TryGetValue(processId, out var counter))
                    {
                        try
                        {
                            counter = new PerformanceCounter("Process", "% Processor Time", GetProcessInstanceName(processId));
                            _cpuCounters[processId] = counter;
                            // First call returns 0, so call it once
                            counter.NextValue();
                            // Wait a bit
                            Thread.Sleep(100);
                            // Get the actual value
                            usage = counter.NextValue() / Environment.ProcessorCount;
                            _lastCpuUsage[processId] = usage;
                            return usage;
                        }
                        catch
                        {
                            return 0;
                        }
                    }
                    
                    try
                    {
                        usage = counter.NextValue() / Environment.ProcessorCount;
                        _lastCpuUsage[processId] = usage;
                        return usage;
                    }
                    catch
                    {
                        return 0;
                    }
                }
            }
            catch
            {
                return 0;
            }
        }
        
        /// <summary>
        /// Gets the memory usage of a process
        /// </summary>
        /// <param name="processId">The process ID</param>
        /// <returns>The memory usage in bytes</returns>
        public long GetProcessMemoryUsage(int processId)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                return process.WorkingSet64;
            }
            catch
            {
                return 0;
            }
        }
        
        /// <summary>
        /// Gets the processor affinity of a process
        /// </summary>
        /// <param name="processId">The process ID</param>
        /// <returns>The processor affinity mask</returns>
        public long GetProcessAffinity(int processId)
        {
            try
            {
                var processHandle = OpenProcess(PROCESS_ALL_ACCESS, false, processId);
                if (processHandle == IntPtr.Zero)
                    return 0;
                    
                try
                {
                    IntPtr processAffinityMask, systemAffinityMask;
                    if (GetProcessAffinityMask(processHandle, out processAffinityMask, out systemAffinityMask))
                    {
                        return processAffinityMask.ToInt64();
                    }
                    
                    return 0;
                }
                finally
                {
                    CloseHandle(processHandle);
                }
            }
            catch
            {
                return 0;
            }
        }
        
        /// <summary>
        /// Sets the processor affinity of a process
        /// </summary>
        /// <param name="processId">The process ID</param>
        /// <param name="affinity">The processor affinity mask</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool SetProcessAffinity(int processId, long affinity)
        {
            try
            {
                var processHandle = OpenProcess(PROCESS_ALL_ACCESS, false, processId);
                if (processHandle == IntPtr.Zero)
                    return false;
                    
                try
                {
                    return SetProcessAffinityMask(processHandle, new IntPtr(affinity));
                }
                finally
                {
                    CloseHandle(processHandle);
                }
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Gets the priority of a process
        /// </summary>
        /// <param name="processId">The process ID</param>
        /// <returns>The process priority</returns>
        public ProcessPriority GetProcessPriority(int processId)
        {
            try
            {
                var processHandle = OpenProcess(PROCESS_ALL_ACCESS, false, processId);
                if (processHandle == IntPtr.Zero)
                    return ProcessPriority.Normal;
                    
                try
                {
                    var priorityClass = GetPriorityClass(processHandle);
                    
                    switch (priorityClass)
                    {
                        case 0x00000040: // IDLE_PRIORITY_CLASS
                            return ProcessPriority.Idle;
                        case 0x00004000: // BELOW_NORMAL_PRIORITY_CLASS
                            return ProcessPriority.BelowNormal;
                        case 0x00000020: // NORMAL_PRIORITY_CLASS
                            return ProcessPriority.Normal;
                        case 0x00008000: // ABOVE_NORMAL_PRIORITY_CLASS
                            return ProcessPriority.AboveNormal;
                        case 0x00000080: // HIGH_PRIORITY_CLASS
                            return ProcessPriority.High;
                        case 0x00000100: // REALTIME_PRIORITY_CLASS
                            return ProcessPriority.RealTime;
                        default:
                            return ProcessPriority.Normal;
                    }
                }
                finally
                {
                    CloseHandle(processHandle);
                }
            }
            catch
            {
                return ProcessPriority.Normal;
            }
        }
        
        /// <summary>
        /// Sets the priority of a process
        /// </summary>
        /// <param name="processId">The process ID</param>
        /// <param name="priority">The process priority</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool SetProcessPriority(int processId, ProcessPriority priority)
        {
            try
            {
                var processHandle = OpenProcess(PROCESS_ALL_ACCESS, false, processId);
                if (processHandle == IntPtr.Zero)
                    return false;
                    
                try
                {
                    uint priorityClass;
                    
                    switch (priority)
                    {
                        case ProcessPriority.Idle:
                            priorityClass = 0x00000040; // IDLE_PRIORITY_CLASS
                            break;
                        case ProcessPriority.BelowNormal:
                            priorityClass = 0x00004000; // BELOW_NORMAL_PRIORITY_CLASS
                            break;
                        case ProcessPriority.Normal:
                            priorityClass = 0x00000020; // NORMAL_PRIORITY_CLASS
                            break;
                        case ProcessPriority.AboveNormal:
                            priorityClass = 0x00008000; // ABOVE_NORMAL_PRIORITY_CLASS
                            break;
                        case ProcessPriority.High:
                            priorityClass = 0x00000080; // HIGH_PRIORITY_CLASS
                            break;
                        case ProcessPriority.RealTime:
                            priorityClass = 0x00000100; // REALTIME_PRIORITY_CLASS
                            break;
                        default:
                            priorityClass = 0x00000020; // NORMAL_PRIORITY_CLASS
                            break;
                    }
                    
                    return SetPriorityClass(processHandle, priorityClass);
                }
                finally
                {
                    CloseHandle(processHandle);
                }
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Terminates a process
        /// </summary>
        /// <param name="processId">The process ID</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool TerminateProcess(int processId)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                process.Kill();
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Creates a process
        /// </summary>
        /// <param name="fileName">The file name</param>
        /// <param name="arguments">The command line arguments</param>
        /// <param name="workingDirectory">The working directory</param>
        /// <returns>The process info or null if creation failed</returns>
        public ProcessInfo CreateProcess(string fileName, string arguments = null, string workingDirectory = null)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = true
                };
                
                var process = Process.Start(startInfo);
                return ProcessInfo.FromProcess(process);
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// Applies affinity rules to processes
        /// </summary>
        /// <param name="rules">The list of affinity rules</param>
        /// <returns>The number of rules applied</returns>
        public int ApplyAffinityRules(IEnumerable<ProcessAffinityRule> rules)
        {
            if (rules == null)
                return 0;
                
            var appliedCount = 0;
            var processes = GetAllProcesses();
            
            foreach (var rule in rules)
            {
                if (!rule.IsEnabled)
                    continue;
                    
                var regex = new Regex("^" + Regex.Escape(rule.ProcessNamePattern).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.IgnoreCase);
                
                foreach (var process in processes)
                {
                    if (regex.IsMatch(process.Name))
                    {
                        var success = false;
                        
                        if (rule.ApplyAffinity)
                        {
                            if (rule.UsePerformanceCores || rule.UseEfficiencyCores)
                            {
                                // Apply special core type settings
                                success = ApplySpecialCoreTypeAffinity(process.Id, rule);
                            }
                            else
                            {
                                // Apply normal affinity mask
                                success = SetProcessAffinity(process.Id, rule.Affinity);
                            }
                        }
                        
                        if (rule.ApplyPriority)
                        {
                            success = SetProcessPriority(process.Id, rule.Priority) || success;
                        }
                        
                        if (success)
                        {
                            appliedCount++;
                        }
                    }
                }
            }
            
            return appliedCount;
        }
        
        /// <summary>
        /// Starts tracking process CPU usage
        /// </summary>
        public void StartProcessTracking()
        {
            if (_isTracking)
                return;
                
            _processTrackingTimer.Change(0, 5000);
            _isTracking = true;
        }
        
        /// <summary>
        /// Stops tracking process CPU usage
        /// </summary>
        public void StopProcessTracking()
        {
            if (!_isTracking)
                return;
                
            _processTrackingTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _isTracking = false;
        }
        
        private void TrackProcesses(object state)
        {
            try
            {
                var processes = GetAllProcesses();
                var processIds = processes.Select(p => p.Id).ToList();
                
                lock (_lockObj)
                {
                    // Remove counters for processes that are no longer running
                    var counterKeys = _cpuCounters.Keys.ToList();
                    foreach (var key in counterKeys)
                    {
                        if (!processIds.Contains(key))
                        {
                            var counter = _cpuCounters[key];
                            counter.Dispose();
                            _cpuCounters.Remove(key);
                            _lastCpuUsage.Remove(key);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to track processes: {ex.Message}");
            }
        }
        
        private bool ApplySpecialCoreTypeAffinity(int processId, ProcessAffinityRule rule)
        {
            try
            {
                var systemInfo = ServiceLocator.Get<ISystemInfoService>();
                var cores = systemInfo.GetCpuCores();
                var affinityMask = 0L;
                
                // Create affinity mask for performance cores
                if (rule.UsePerformanceCores)
                {
                    foreach (var core in cores.Where(c => c.IsPerformanceCore))
                    {
                        affinityMask |= (1L << core.Index);
                    }
                }
                
                // Create affinity mask for efficiency cores
                if (rule.UseEfficiencyCores)
                {
                    foreach (var core in cores.Where(c => c.IsEfficiencyCore))
                    {
                        affinityMask |= (1L << core.Index);
                    }
                }
                
                // If no special cores are found, use all cores
                if (affinityMask == 0)
                {
                    foreach (var core in cores)
                    {
                        affinityMask |= (1L << core.Index);
                    }
                }
                
                // Apply the affinity mask
                return SetProcessAffinity(processId, affinityMask);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to apply special core type affinity: {ex.Message}");
                return false;
            }
        }
        
        private void UpdateProcessCpuUsage(ProcessInfo processInfo)
        {
            if (processInfo != null)
            {
                processInfo.CpuUsage = GetProcessCpuUsage(processInfo.Id);
            }
        }
        
        private static string GetProcessInstanceName(int processId)
        {
            using (var searcher = new ManagementObjectSearcher($"SELECT * FROM Win32_Process WHERE ProcessId = {processId}"))
            {
                foreach (var obj in searcher.Get())
                {
                    return obj["Name"]?.ToString();
                }
            }
            
            return null;
        }
        
        private void OnProcessCounterCategoryInstanceCreated(object sender, InstanceCreatedEventArgs e)
        {
            try
            {
                var instanceName = e.InstanceName;
                int processId = GetProcessIdFromInstanceName(instanceName);
                
                if (processId > 0)
                {
                    var processInfo = GetProcess(processId);
                    if (processInfo != null)
                    {
                        ProcessStarted?.Invoke(this, processInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to handle process instance created: {ex.Message}");
            }
        }
        
        private void OnProcessCounterCategoryInstanceDeleted(object sender, InstanceDeletedEventArgs e)
        {
            try
            {
                var instanceName = e.InstanceName;
                int processId = GetProcessIdFromInstanceName(instanceName);
                
                if (processId > 0)
                {
                    lock (_lockObj)
                    {
                        if (_cpuCounters.TryGetValue(processId, out var counter))
                        {
                            counter.Dispose();
                            _cpuCounters.Remove(processId);
                            _lastCpuUsage.Remove(processId);
                        }
                    }
                    
                    ProcessTerminated?.Invoke(this, processId);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to handle process instance deleted: {ex.Message}");
            }
        }
        
        private int GetProcessIdFromInstanceName(string instanceName)
        {
            try
            {
                int processId = 0;
                string processName = instanceName;
                
                // Extract process ID from instance name (e.g., "chrome#1234" -> 1234)
                var hashIndex = instanceName.LastIndexOf('#');
                if (hashIndex >= 0 && hashIndex < instanceName.Length - 1)
                {
                    var idString = instanceName.Substring(hashIndex + 1);
                    if (int.TryParse(idString, out processId))
                    {
                        return processId;
                    }
                }
                
                // If that fails, try to find the process by name
                using (var searcher = new ManagementObjectSearcher($"SELECT * FROM Win32_Process WHERE Name = '{processName}'"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        var id = obj["ProcessId"];
                        if (id != null)
                        {
                            processId = Convert.ToInt32(id);
                            return processId;
                        }
                    }
                }
                
                return 0;
            }
            catch
            {
                return 0;
            }
        }
    }
    
    /// <summary>
    /// Provides access to process performance counter category events
    /// </summary>
    internal static class ProcessCounterCategory
    {
        private static readonly PerformanceCounterCategory _category = new PerformanceCounterCategory("Process");
        private static readonly Timer _timer;
        private static readonly HashSet<string> _instances = new HashSet<string>();
        private static readonly object _lock = new object();
        
        /// <summary>
        /// Occurs when a process instance is created
        /// </summary>
        public static event EventHandler<InstanceCreatedEventArgs> InstanceCreated;
        
        /// <summary>
        /// Occurs when a process instance is deleted
        /// </summary>
        public static event EventHandler<InstanceDeletedEventArgs> InstanceDeleted;
        
        /// <summary>
        /// Initializes the <see cref="ProcessCounterCategory"/> class
        /// </summary>
        static ProcessCounterCategory()
        {
            lock (_lock)
            {
                try
                {
                    var instanceNames = _category.GetInstanceNames();
                    foreach (var name in instanceNames)
                    {
                        _instances.Add(name);
                    }
                }
                catch
                {
                    // Ignore exceptions
                }
            }
            
            _timer = new Timer(CheckForChanges, null, 0, 2000);
        }
        
        private static void CheckForChanges(object state)
        {
            lock (_lock)
            {
                try
                {
                    var currentInstances = new HashSet<string>(_category.GetInstanceNames());
                    
                    // Check for new instances
                    foreach (var instance in currentInstances)
                    {
                        if (!_instances.Contains(instance))
                        {
                            InstanceCreated?.Invoke(null, new InstanceCreatedEventArgs(instance));
                            _instances.Add(instance);
                        }
                    }
                    
                    // Check for deleted instances
                    var deletedInstances = new List<string>();
                    foreach (var instance in _instances)
                    {
                        if (!currentInstances.Contains(instance))
                        {
                            InstanceDeleted?.Invoke(null, new InstanceDeletedEventArgs(instance));
                            deletedInstances.Add(instance);
                        }
                    }
                    
                    // Remove deleted instances from the set
                    foreach (var instance in deletedInstances)
                    {
                        _instances.Remove(instance);
                    }
                }
                catch
                {
                    // Ignore exceptions
                }
            }
        }
    }
    
    /// <summary>
    /// Provides data for the InstanceCreated event
    /// </summary>
    internal class InstanceCreatedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the instance name
        /// </summary>
        public string InstanceName { get; }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="InstanceCreatedEventArgs"/> class
        /// </summary>
        /// <param name="instanceName">The instance name</param>
        public InstanceCreatedEventArgs(string instanceName)
        {
            InstanceName = instanceName;
        }
    }
    
    /// <summary>
    /// Provides data for the InstanceDeleted event
    /// </summary>
    internal class InstanceDeletedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the instance name
        /// </summary>
        public string InstanceName { get; }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="InstanceDeletedEventArgs"/> class
        /// </summary>
        /// <param name="instanceName">The instance name</param>
        public InstanceDeletedEventArgs(string instanceName)
        {
            InstanceName = instanceName;
        }
    }
}