using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Implementation of process management operations
    /// </summary>
    public class ProcessService : IProcessService
    {
        private readonly Dictionary<int, Process> _cachedProcesses = new Dictionary<int, Process>();
        private readonly Dictionary<int, double> _cpuUsageCache = new Dictionary<int, double>();
        private readonly Dictionary<int, DateTime> _lastCpuTimeCache = new Dictionary<int, DateTime>();
        private readonly Dictionary<int, TimeSpan> _lastTotalProcessorTimeCache = new Dictionary<int, TimeSpan>();

        /// <summary>
        /// Occurs when a process is started
        /// </summary>
        public event EventHandler<ProcessInfo> ProcessStarted;
        
        /// <summary>
        /// Occurs when a process is terminated
        /// </summary>
        public event EventHandler<int> ProcessTerminated;

        /// <summary>
        /// Gets all running processes
        /// </summary>
        /// <returns>A list of processes</returns>
        public List<ProcessInfo> GetAllProcesses()
        {
            var processes = Process.GetProcesses();
            var result = new List<ProcessInfo>();
            var currentPid = Process.GetCurrentProcess().Id;
            
            foreach (var process in processes)
            {
                try
                {
                    // Skip current process
                    if (process.Id == currentPid)
                        continue;
                    
                    var processInfo = new ProcessInfo
                    {
                        Id = process.Id,
                        Name = process.ProcessName,
                        WindowTitle = process.MainWindowTitle,
                        Priority = ConvertPriorityClass(process.PriorityClass),
                        MemoryUsage = process.WorkingSet64,
                        ThreadCount = process.Threads.Count,
                        IsResponding = process.Responding,
                        IsSystemProcess = IsSystemProcess(process)
                    };
                    
                    try
                    {
                        processInfo.StartTime = process.StartTime;
                        processInfo.CpuUsage = GetCpuUsageForProcess(process);
                        processInfo.AffinityMask = (long)process.ProcessorAffinity;
                    }
                    catch
                    {
                        // Some system processes may not allow access to this information
                        processInfo.IsSystemProcess = true;
                    }
                    
                    try
                    {
                        var wmiQueryString = $"SELECT ExecutablePath, Description, Company FROM Win32_Process WHERE ProcessId = {process.Id}";
                        using (var searcher = new ManagementObjectSearcher(wmiQueryString))
                        {
                            using (var results = searcher.Get())
                            {
                                foreach (var obj in results)
                                {
                                    processInfo.ExecutablePath = obj["ExecutablePath"]?.ToString();
                                    processInfo.Description = obj["Description"]?.ToString();
                                    processInfo.CompanyName = obj["Company"]?.ToString();
                                }
                            }
                        }
                    }
                    catch
                    {
                        // WMI might fail for some system processes
                    }
                    
                    _cachedProcesses[process.Id] = process;
                    result.Add(processInfo);
                }
                catch
                {
                    // Skip processes that can't be accessed
                }
            }
            
            return result;
        }

        /// <summary>
        /// Gets a process by its ID
        /// </summary>
        /// <param name="processId">The process ID</param>
        /// <returns>The process information or null if not found</returns>
        public ProcessInfo GetProcessById(int processId)
        {
            try
            {
                Process process;
                
                if (_cachedProcesses.TryGetValue(processId, out process))
                {
                    if (!IsProcessAlive(process))
                    {
                        _cachedProcesses.Remove(processId);
                        return null;
                    }
                }
                else
                {
                    process = Process.GetProcessById(processId);
                    _cachedProcesses[processId] = process;
                }
                
                var processInfo = new ProcessInfo
                {
                    Id = process.Id,
                    Name = process.ProcessName,
                    WindowTitle = process.MainWindowTitle,
                    Priority = ConvertPriorityClass(process.PriorityClass),
                    MemoryUsage = process.WorkingSet64,
                    ThreadCount = process.Threads.Count,
                    IsResponding = process.Responding,
                    IsSystemProcess = IsSystemProcess(process)
                };
                
                try
                {
                    processInfo.StartTime = process.StartTime;
                    processInfo.CpuUsage = GetCpuUsageForProcess(process);
                    processInfo.AffinityMask = (long)process.ProcessorAffinity;
                }
                catch
                {
                    // Some system processes may not allow access to this information
                    processInfo.IsSystemProcess = true;
                }
                
                try
                {
                    var wmiQueryString = $"SELECT ExecutablePath, Description, Company FROM Win32_Process WHERE ProcessId = {process.Id}";
                    using (var searcher = new ManagementObjectSearcher(wmiQueryString))
                    {
                        using (var results = searcher.Get())
                        {
                            foreach (var obj in results)
                            {
                                processInfo.ExecutablePath = obj["ExecutablePath"]?.ToString();
                                processInfo.Description = obj["Description"]?.ToString();
                                processInfo.CompanyName = obj["Company"]?.ToString();
                            }
                        }
                    }
                }
                catch
                {
                    // WMI might fail for some system processes
                }
                
                return processInfo;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets processes by name pattern
        /// </summary>
        /// <param name="namePattern">The process name pattern</param>
        /// <returns>A list of matching processes</returns>
        public List<ProcessInfo> GetProcessesByName(string namePattern)
        {
            if (string.IsNullOrWhiteSpace(namePattern))
                return new List<ProcessInfo>();

            var pattern = new Regex(namePattern, RegexOptions.IgnoreCase);
            return GetAllProcesses().Where(p => pattern.IsMatch(p.Name)).ToList();
        }

        /// <summary>
        /// Sets the process priority
        /// </summary>
        /// <param name="processId">The process ID</param>
        /// <param name="priority">The new priority</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool SetProcessPriority(int processId, ProcessPriority priority)
        {
            try
            {
                Process process;
                
                if (_cachedProcesses.TryGetValue(processId, out process))
                {
                    if (!IsProcessAlive(process))
                    {
                        _cachedProcesses.Remove(processId);
                        return false;
                    }
                }
                else
                {
                    process = Process.GetProcessById(processId);
                    _cachedProcesses[processId] = process;
                }
                
                process.PriorityClass = ConvertPriority(priority);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Sets the process affinity mask
        /// </summary>
        /// <param name="processId">The process ID</param>
        /// <param name="affinityMask">The new affinity mask</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool SetProcessAffinity(int processId, long affinityMask)
        {
            if (affinityMask <= 0)
                return false;
                
            try
            {
                Process process;
                
                if (_cachedProcesses.TryGetValue(processId, out process))
                {
                    if (!IsProcessAlive(process))
                    {
                        _cachedProcesses.Remove(processId);
                        return false;
                    }
                }
                else
                {
                    process = Process.GetProcessById(processId);
                    _cachedProcesses[processId] = process;
                }
                
                process.ProcessorAffinity = new IntPtr(affinityMask);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Creates a process affinity mask from a list of core indices
        /// </summary>
        /// <param name="coreIndices">The core indices to include</param>
        /// <returns>The affinity mask</returns>
        public long CreateAffinityMask(IEnumerable<int> coreIndices)
        {
            if (coreIndices == null)
                return 0;
                
            long mask = 0;
            
            foreach (var index in coreIndices)
            {
                if (index >= 0 && index < 64) // Max 64 cores for long mask
                {
                    mask |= (1L << index);
                }
            }
            
            return mask;
        }

        /// <summary>
        /// Gets the core indices from an affinity mask
        /// </summary>
        /// <param name="affinityMask">The affinity mask</param>
        /// <returns>A list of core indices</returns>
        public List<int> GetCoreIndicesFromMask(long affinityMask)
        {
            var indices = new List<int>();
            
            for (int i = 0; i < 64; i++) // Max 64 cores for long mask
            {
                if ((affinityMask & (1L << i)) != 0)
                {
                    indices.Add(i);
                }
            }
            
            return indices;
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
                Process process;
                
                if (_cachedProcesses.TryGetValue(processId, out process))
                {
                    if (!IsProcessAlive(process))
                    {
                        _cachedProcesses.Remove(processId);
                        return true; // Already terminated
                    }
                }
                else
                {
                    process = Process.GetProcessById(processId);
                    _cachedProcesses[processId] = process;
                }
                
                process.Kill();
                _cachedProcesses.Remove(processId);
                
                ProcessTerminated?.Invoke(this, processId);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Applies affinity rules to running processes
        /// </summary>
        /// <param name="rules">The list of affinity rules to apply</param>
        /// <returns>The number of rules applied successfully</returns>
        public int ApplyAffinityRules(IEnumerable<ProcessAffinityRule> rules)
        {
            if (rules == null)
                return 0;
                
            int appliedCount = 0;
            var processes = GetAllProcesses();
            
            foreach (var rule in rules.Where(r => r.IsEnabled))
            {
                var matchingProcesses = new List<ProcessInfo>();
                
                if (rule.ExactMatch)
                {
                    matchingProcesses.AddRange(
                        processes.Where(p => 
                            rule.CaseSensitive 
                                ? p.Name == rule.ProcessNamePattern 
                                : p.Name.Equals(rule.ProcessNamePattern, StringComparison.OrdinalIgnoreCase)
                        )
                    );
                }
                else
                {
                    var pattern = new Regex(rule.ProcessNamePattern, 
                        rule.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase);
                    matchingProcesses.AddRange(processes.Where(p => pattern.IsMatch(p.Name)));
                }
                
                foreach (var process in matchingProcesses)
                {
                    bool anyApplied = false;
                    
                    if (rule.IncludedCores.Count > 0)
                    {
                        long affinityMask = CreateAffinityMask(rule.IncludedCores);
                        if (SetProcessAffinity(process.Id, affinityMask))
                        {
                            anyApplied = true;
                        }
                    }
                    
                    if (rule.Priority.HasValue)
                    {
                        if (SetProcessPriority(process.Id, rule.Priority.Value))
                        {
                            anyApplied = true;
                        }
                    }
                    
                    if (anyApplied)
                    {
                        appliedCount++;
                    }
                }
                
                if (appliedCount > 0 && rule.ApplyOnce)
                {
                    rule.HasBeenApplied = true;
                }
            }
            
            return appliedCount;
        }

        /// <summary>
        /// Gets the CPU usage per process
        /// </summary>
        /// <returns>A dictionary mapping process IDs to CPU usage percentages</returns>
        public Dictionary<int, double> GetProcessCpuUsage()
        {
            var result = new Dictionary<int, double>();
            var processes = Process.GetProcesses();
            var currentPid = Process.GetCurrentProcess().Id;
            
            foreach (var process in processes)
            {
                try
                {
                    // Skip current process
                    if (process.Id == currentPid)
                        continue;
                        
                    result[process.Id] = GetCpuUsageForProcess(process);
                }
                catch
                {
                    // Skip processes that can't be accessed
                }
            }
            
            return result;
        }

        private double GetCpuUsageForProcess(Process process)
        {
            try
            {
                var now = DateTime.Now;
                var currentTotalProcessorTime = process.TotalProcessorTime;
                
                if (_lastCpuTimeCache.TryGetValue(process.Id, out var lastTime) &&
                    _lastTotalProcessorTimeCache.TryGetValue(process.Id, out var lastTotalProcessorTime))
                {
                    var timeDiff = now - lastTime;
                    var cpuUsedMs = (currentTotalProcessorTime - lastTotalProcessorTime).TotalMilliseconds;
                    var cpuUsage = cpuUsedMs / timeDiff.TotalMilliseconds / Environment.ProcessorCount * 100;
                    _cpuUsageCache[process.Id] = cpuUsage;
                }
                
                _lastCpuTimeCache[process.Id] = now;
                _lastTotalProcessorTimeCache[process.Id] = currentTotalProcessorTime;
                
                if (_cpuUsageCache.TryGetValue(process.Id, out var cachedUsage))
                {
                    return cachedUsage;
                }
                
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        private bool IsProcessAlive(Process process)
        {
            try
            {
                return !process.HasExited;
            }
            catch
            {
                return false;
            }
        }

        private ProcessPriority ConvertPriorityClass(ProcessPriorityClass priorityClass)
        {
            switch (priorityClass)
            {
                case ProcessPriorityClass.Idle:
                    return ProcessPriority.Idle;
                case ProcessPriorityClass.BelowNormal:
                    return ProcessPriority.BelowNormal;
                case ProcessPriorityClass.Normal:
                    return ProcessPriority.Normal;
                case ProcessPriorityClass.AboveNormal:
                    return ProcessPriority.AboveNormal;
                case ProcessPriorityClass.High:
                    return ProcessPriority.High;
                case ProcessPriorityClass.RealTime:
                    return ProcessPriority.Realtime;
                default:
                    return ProcessPriority.Normal;
            }
        }

        private ProcessPriorityClass ConvertPriority(ProcessPriority priority)
        {
            switch (priority)
            {
                case ProcessPriority.Idle:
                    return ProcessPriorityClass.Idle;
                case ProcessPriority.BelowNormal:
                    return ProcessPriorityClass.BelowNormal;
                case ProcessPriority.Normal:
                    return ProcessPriorityClass.Normal;
                case ProcessPriority.AboveNormal:
                    return ProcessPriorityClass.AboveNormal;
                case ProcessPriority.High:
                    return ProcessPriorityClass.High;
                case ProcessPriority.Realtime:
                    return ProcessPriorityClass.RealTime;
                default:
                    return ProcessPriorityClass.Normal;
            }
        }

        private bool IsSystemProcess(Process process)
        {
            try
            {
                // Common system processes
                string[] systemProcessNames = { "system", "svchost", "lsass", "csrss", "winlogon", "explorer", "services", "smss" };
                
                if (systemProcessNames.Contains(process.ProcessName.ToLower()))
                    return true;
                    
                if (process.SessionId == 0)
                    return true;
                    
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}