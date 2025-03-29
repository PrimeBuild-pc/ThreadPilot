using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ThreadPilot.Helpers;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    public class ProcessService
    {
        // Performance counters for process monitoring
        private readonly Dictionary<int, PerformanceCounter> _cpuCounters = new Dictionary<int, PerformanceCounter>();
        private readonly Dictionary<int, PerformanceCounter> _memCounters = new Dictionary<int, PerformanceCounter>();

        public ProcessService()
        {
            // Nothing to initialize here
        }

        public List<ProcessInfo> GetAllProcesses()
        {
            var result = new List<ProcessInfo>();
            
            try
            {
                var processes = Process.GetProcesses();
                
                foreach (var process in processes)
                {
                    try
                    {
                        // Skip system processes with PID 0 and 4
                        if (process.Id == 0 || process.Id == 4)
                            continue;

                        var processInfo = new ProcessInfo
                        {
                            Name = process.ProcessName,
                            Pid = process.Id,
                            Description = GetProcessDescription(process)
                        };

                        // Get process priority if accessible
                        try
                        {
                            processInfo.Priority = process.PriorityClass;
                        }
                        catch
                        {
                            processInfo.Priority = ProcessPriorityClass.Normal;
                        }

                        // Get process affinity if accessible
                        try
                        {
                            processInfo.AffinityMask = (long)process.ProcessorAffinity;
                        }
                        catch
                        {
                            processInfo.AffinityMask = 0;
                        }

                        // Get process icon if available
                        try
                        {
                            processInfo.IconData = GetProcessIcon(process);
                        }
                        catch
                        {
                            // Use default icon
                            processInfo.IconData = null;
                        }

                        // Get CPU and memory usage
                        UpdateProcessPerformanceCounters(processInfo);

                        result.Add(processInfo);
                    }
                    catch
                    {
                        // Skip processes that can't be accessed
                        continue;
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
                
                // Sort by name
                result = result.OrderBy(p => p.Name).ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting processes: {ex.Message}");
                throw;
            }

            return result;
        }

        public ProcessInfo GetProcessById(int pid)
        {
            try
            {
                var process = Process.GetProcessById(pid);
                
                var processInfo = new ProcessInfo
                {
                    Name = process.ProcessName,
                    Pid = process.Id,
                    Description = GetProcessDescription(process)
                };

                // Get process priority if accessible
                try
                {
                    processInfo.Priority = process.PriorityClass;
                }
                catch
                {
                    processInfo.Priority = ProcessPriorityClass.Normal;
                }

                // Get process affinity if accessible
                try
                {
                    processInfo.AffinityMask = (long)process.ProcessorAffinity;
                }
                catch
                {
                    processInfo.AffinityMask = 0;
                }

                // Get process icon if available
                try
                {
                    processInfo.IconData = GetProcessIcon(process);
                }
                catch
                {
                    // Use default icon
                    processInfo.IconData = null;
                }

                // Get CPU and memory usage
                UpdateProcessPerformanceCounters(processInfo);

                process.Dispose();
                return processInfo;
            }
            catch
            {
                return null;
            }
        }

        public Process GetProcessByName(string name)
        {
            try
            {
                var processes = Process.GetProcessesByName(name);
                return processes.Length > 0 ? processes[0] : null;
            }
            catch
            {
                return null;
            }
        }

        public void SetProcessPriority(int pid, ProcessPriorityClass priority)
        {
            try
            {
                var process = Process.GetProcessById(pid);
                process.PriorityClass = priority;
                process.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting process priority: {ex.Message}");
                throw;
            }
        }

        private string GetProcessDescription(Process process)
        {
            try
            {
                if (!string.IsNullOrEmpty(process.MainModule?.FileName))
                {
                    var versionInfo = FileVersionInfo.GetVersionInfo(process.MainModule.FileName);
                    return string.IsNullOrEmpty(versionInfo.FileDescription) ? process.ProcessName : versionInfo.FileDescription;
                }
            }
            catch
            {
                // Fall back to using WMI for system processes
                try
                {
                    using var searcher = new ManagementObjectSearcher($"SELECT * FROM Win32_Process WHERE ProcessId = {process.Id}");
                    foreach (var obj in searcher.Get())
                    {
                        return obj["Description"]?.ToString() ?? process.ProcessName;
                    }
                }
                catch
                {
                    // Ignore errors
                }
            }

            return process.ProcessName;
        }

        private byte[] GetProcessIcon(Process process)
        {
            try
            {
                if (!string.IsNullOrEmpty(process.MainModule?.FileName))
                {
                    using var icon = Icon.ExtractAssociatedIcon(process.MainModule.FileName);
                    if (icon != null)
                    {
                        using var bitmap = icon.ToBitmap();
                        using var ms = new MemoryStream();
                        bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        return ms.ToArray();
                    }
                }
            }
            catch
            {
                // Ignore errors
            }

            return null;
        }

        private void UpdateProcessPerformanceCounters(ProcessInfo processInfo)
        {
            try
            {
                // CPU usage
                if (!_cpuCounters.TryGetValue(processInfo.Pid, out var cpuCounter))
                {
                    try
                    {
                        cpuCounter = new PerformanceCounter("Process", "% Processor Time", processInfo.Name);
                        _cpuCounters[processInfo.Pid] = cpuCounter;
                        // First call to NextValue() always returns 0, so call it now
                        cpuCounter.NextValue();
                    }
                    catch
                    {
                        // Ignore errors for system processes
                    }
                }

                if (cpuCounter != null)
                {
                    try
                    {
                        processInfo.CpuUsage = Math.Round(cpuCounter.NextValue() / Environment.ProcessorCount, 1);
                    }
                    catch
                    {
                        // Counter might be invalid if process has terminated
                        _cpuCounters.Remove(processInfo.Pid);
                    }
                }

                // Memory usage
                if (!_memCounters.TryGetValue(processInfo.Pid, out var memCounter))
                {
                    try
                    {
                        memCounter = new PerformanceCounter("Process", "Working Set", processInfo.Name);
                        _memCounters[processInfo.Pid] = memCounter;
                    }
                    catch
                    {
                        // Ignore errors for system processes
                    }
                }

                if (memCounter != null)
                {
                    try
                    {
                        // Convert to MB
                        processInfo.MemoryUsage = Math.Round(memCounter.NextValue() / 1024 / 1024, 1);
                    }
                    catch
                    {
                        // Counter might be invalid if process has terminated
                        _memCounters.Remove(processInfo.Pid);
                    }
                }
            }
            catch
            {
                // Ignore performance counter errors
            }
        }
    }
}
