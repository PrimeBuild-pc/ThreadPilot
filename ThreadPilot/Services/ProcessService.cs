using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Implementation of process service
    /// </summary>
    public class ProcessService : IProcessService
    {
        // Random for generating demo data
        private readonly Random _random = new Random();
        
        // List of demo processes
        private readonly List<ProcessInfo> _demoProcesses;
        
        /// <summary>
        /// Constructor
        /// </summary>
        public ProcessService()
        {
            // Create demo processes
            _demoProcesses = GenerateDemoProcesses();
        }
        
        /// <summary>
        /// Get all processes
        /// </summary>
        public IEnumerable<ProcessInfo> GetAllProcesses()
        {
            try
            {
                // In a real application, we would get the actual processes
                // For now, we'll return demo data
                
                // Refresh demo processes
                RefreshDemoProcesses();
                
                return _demoProcesses;
            }
            catch (Exception)
            {
                // In case of error, we'll return empty list
                return new List<ProcessInfo>();
            }
        }
        
        /// <summary>
        /// Get process by ID
        /// </summary>
        public ProcessInfo? GetProcessById(int processId)
        {
            try
            {
                // In a real application, we would get the actual process
                // For now, we'll return demo data
                
                return _demoProcesses.FirstOrDefault(p => p.Id == processId);
            }
            catch (Exception)
            {
                // In case of error, we'll return null
                return null;
            }
        }
        
        /// <summary>
        /// Set process affinity
        /// </summary>
        public bool SetProcessAffinity(int processId, long affinityMask)
        {
            try
            {
                // In a real application, we would set the process affinity
                // For now, we'll just update our demo data
                
                var process = _demoProcesses.FirstOrDefault(p => p.Id == processId);
                
                if (process == null)
                {
                    return false;
                }
                
                process.AffinityMask = affinityMask;
                
                return true;
            }
            catch (Exception)
            {
                // In case of error, we'll return false
                return false;
            }
        }
        
        /// <summary>
        /// Set process priority
        /// </summary>
        public bool SetProcessPriority(int processId, ProcessPriority priority)
        {
            try
            {
                // In a real application, we would set the process priority
                // For now, we'll just update our demo data
                
                var process = _demoProcesses.FirstOrDefault(p => p.Id == processId);
                
                if (process == null)
                {
                    return false;
                }
                
                process.Priority = priority;
                
                return true;
            }
            catch (Exception)
            {
                // In case of error, we'll return false
                return false;
            }
        }
        
        /// <summary>
        /// Suspend process
        /// </summary>
        public bool SuspendProcess(int processId)
        {
            try
            {
                // In a real application, we would suspend the process
                // For now, we'll just update our demo data
                
                var process = _demoProcesses.FirstOrDefault(p => p.Id == processId);
                
                if (process == null)
                {
                    return false;
                }
                
                process.IsSuspended = true;
                
                return true;
            }
            catch (Exception)
            {
                // In case of error, we'll return false
                return false;
            }
        }
        
        /// <summary>
        /// Resume process
        /// </summary>
        public bool ResumeProcess(int processId)
        {
            try
            {
                // In a real application, we would resume the process
                // For now, we'll just update our demo data
                
                var process = _demoProcesses.FirstOrDefault(p => p.Id == processId);
                
                if (process == null)
                {
                    return false;
                }
                
                process.IsSuspended = false;
                
                return true;
            }
            catch (Exception)
            {
                // In case of error, we'll return false
                return false;
            }
        }
        
        /// <summary>
        /// Generate demo processes
        /// </summary>
        private List<ProcessInfo> GenerateDemoProcesses()
        {
            var processes = new List<ProcessInfo>();
            var coreCount = Environment.ProcessorCount;
            
            // System processes
            processes.Add(CreateProcess(1, "System", "Windows System Process", "C:\\Windows\\System32\\ntoskrnl.exe", 0.5, 50000, 8, (1 << coreCount) - 1, ProcessPriority.Realtime, false, true));
            processes.Add(CreateProcess(4, "System Idle Process", "Windows System Process", "C:\\Windows\\System32\\ntoskrnl.exe", 0.1, 0, 1, (1 << coreCount) - 1, ProcessPriority.Idle, false, true));
            processes.Add(CreateProcess(84, "Registry", "Windows Registry Service", "C:\\Windows\\System32\\smss.exe", 0.1, 2000, 2, (1 << coreCount) - 1, ProcessPriority.Normal, false, true));
            processes.Add(CreateProcess(372, "smss.exe", "Session Manager Subsystem", "C:\\Windows\\System32\\smss.exe", 0.1, 900, 2, (1 << coreCount) - 1, ProcessPriority.Normal, false, true));
            processes.Add(CreateProcess(476, "csrss.exe", "Client Server Runtime Process", "C:\\Windows\\System32\\csrss.exe", 0.2, 3600, 10, (1 << coreCount) - 1, ProcessPriority.Normal, false, true));
            processes.Add(CreateProcess(552, "wininit.exe", "Windows Initialization Process", "C:\\Windows\\System32\\wininit.exe", 0.1, 4500, 3, (1 << coreCount) - 1, ProcessPriority.Normal, false, true));
            processes.Add(CreateProcess(560, "winlogon.exe", "Windows Logon Process", "C:\\Windows\\System32\\winlogon.exe", 0.2, 5000, 5, (1 << coreCount) - 1, ProcessPriority.Normal, false, true));
            processes.Add(CreateProcess(652, "services.exe", "Services Control Manager", "C:\\Windows\\System32\\services.exe", 0.3, 5200, 6, (1 << coreCount) - 1, ProcessPriority.Normal, false, true));
            processes.Add(CreateProcess(676, "lsass.exe", "Local Security Authority Process", "C:\\Windows\\System32\\lsass.exe", 0.3, 11000, 12, (1 << coreCount) - 1, ProcessPriority.Normal, false, true));
            processes.Add(CreateProcess(748, "svchost.exe", "Host Process for Windows Services", "C:\\Windows\\System32\\svchost.exe", 0.4, 8000, 16, (1 << coreCount) - 1, ProcessPriority.Normal, false, true));
            
            // User processes
            processes.Add(CreateProcess(1056, "dwm.exe", "Desktop Window Manager", "C:\\Windows\\System32\\dwm.exe", 2.0, 75000, 12, (1 << coreCount) - 1, ProcessPriority.AboveNormal, false, false));
            processes.Add(CreateProcess(1180, "explorer.exe", "Windows Explorer", "C:\\Windows\\explorer.exe", 1.5, 65000, 32, (1 << coreCount) - 1, ProcessPriority.Normal, false, false));
            processes.Add(CreateProcess(3224, "chrome.exe", "Google Chrome", "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe", 5.0, 250000, 35, (1 << coreCount) - 1, ProcessPriority.Normal, false, false));
            processes.Add(CreateProcess(3352, "chrome.exe", "Google Chrome - Tab 1", "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe", 2.5, 125000, 18, (1 << coreCount) - 1, ProcessPriority.Normal, false, false));
            processes.Add(CreateProcess(3648, "chrome.exe", "Google Chrome - Tab 2", "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe", 3.0, 145000, 22, (1 << coreCount) - 1, ProcessPriority.Normal, false, false));
            processes.Add(CreateProcess(4152, "msedge.exe", "Microsoft Edge", "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe", 4.0, 200000, 28, (1 << coreCount) - 1, ProcessPriority.Normal, false, false));
            processes.Add(CreateProcess(5908, "Discord.exe", "Discord", "C:\\Users\\user\\AppData\\Local\\Discord\\app-1.0.9020\\Discord.exe", 1.8, 180000, 20, (1 << coreCount) - 1, ProcessPriority.Normal, false, false));
            processes.Add(CreateProcess(6604, "Spotify.exe", "Spotify", "C:\\Users\\user\\AppData\\Roaming\\Spotify\\Spotify.exe", 1.2, 160000, 16, (1 << coreCount) - 1, ProcessPriority.Normal, false, false));
            processes.Add(CreateProcess(7124, "Slack.exe", "Slack", "C:\\Users\\user\\AppData\\Local\\slack\\app-4.31.0\\slack.exe", 1.5, 170000, 18, (1 << coreCount) - 1, ProcessPriority.Normal, false, false));
            processes.Add(CreateProcess(8752, "Teams.exe", "Microsoft Teams", "C:\\Users\\user\\AppData\\Local\\Microsoft\\Teams\\current\\Teams.exe", 2.2, 190000, 24, (1 << coreCount) - 1, ProcessPriority.Normal, false, false));
            
            // Office
            processes.Add(CreateProcess(9480, "WINWORD.EXE", "Microsoft Word", "C:\\Program Files\\Microsoft Office\\root\\Office16\\WINWORD.EXE", 1.0, 110000, 12, (1 << coreCount) - 1, ProcessPriority.Normal, false, false));
            processes.Add(CreateProcess(9876, "EXCEL.EXE", "Microsoft Excel", "C:\\Program Files\\Microsoft Office\\root\\Office16\\EXCEL.EXE", 1.5, 120000, 14, (1 << coreCount) - 1, ProcessPriority.Normal, false, false));
            processes.Add(CreateProcess(10244, "OUTLOOK.EXE", "Microsoft Outlook", "C:\\Program Files\\Microsoft Office\\root\\Office16\\OUTLOOK.EXE", 1.2, 130000, 16, (1 << coreCount) - 1, ProcessPriority.Normal, false, false));
            
            // Games and other processes
            processes.Add(CreateProcess(11568, "steam.exe", "Steam", "C:\\Program Files (x86)\\Steam\\steam.exe", 0.8, 85000, 22, (1 << coreCount) - 1, ProcessPriority.Normal, false, false));
            processes.Add(CreateProcess(12328, "EpicGamesLauncher.exe", "Epic Games Launcher", "C:\\Program Files (x86)\\Epic Games\\Launcher\\Portal\\Binaries\\Win32\\EpicGamesLauncher.exe", 0.7, 75000, 18, (1 << coreCount) - 1, ProcessPriority.Normal, false, false));
            processes.Add(CreateProcess(13072, "Battle.net.exe", "Battle.net", "C:\\Program Files (x86)\\Battle.net\\Battle.net.exe", 0.6, 68000, 16, (1 << coreCount) - 1, ProcessPriority.Normal, false, false));
            processes.Add(CreateProcess(14356, "Photoshop.exe", "Adobe Photoshop", "C:\\Program Files\\Adobe\\Adobe Photoshop 2023\\Photoshop.exe", 2.8, 250000, 24, (1 << coreCount) - 1, ProcessPriority.Normal, false, false));
            processes.Add(CreateProcess(15052, "AfterFX.exe", "Adobe After Effects", "C:\\Program Files\\Adobe\\Adobe After Effects 2023\\Support Files\\AfterFX.exe", 3.5, 300000, 28, (1 << coreCount) - 1, ProcessPriority.Normal, false, false));
            processes.Add(CreateProcess(16784, "Premiere Pro.exe", "Adobe Premiere Pro", "C:\\Program Files\\Adobe\\Adobe Premiere Pro 2023\\Adobe Premiere Pro.exe", 4.0, 350000, 32, (1 << coreCount) - 1, ProcessPriority.Normal, false, false));
            
            return processes;
        }
        
        /// <summary>
        /// Create a process
        /// </summary>
        private ProcessInfo CreateProcess(int id, string name, string description, string path, double cpuUsage, double memoryUsage, int threadCount, long affinityMask, ProcessPriority priority, bool isSuspended, bool isCritical)
        {
            return new ProcessInfo
            {
                Id = id,
                Name = name,
                Description = description,
                Path = path,
                CpuUsage = cpuUsage,
                MemoryUsage = memoryUsage,
                ThreadCount = threadCount,
                AffinityMask = affinityMask,
                Priority = priority,
                IsSuspended = isSuspended,
                IsCritical = isCritical
            };
        }
        
        /// <summary>
        /// Refresh demo processes
        /// </summary>
        private void RefreshDemoProcesses()
        {
            // Update CPU and memory usage for each process
            foreach (var process in _demoProcesses)
            {
                if (!process.IsSuspended)
                {
                    // Fluctuate CPU usage
                    process.CpuUsage += _random.NextDouble() * 2 - 1;
                    if (process.CpuUsage < 0.1) process.CpuUsage = 0.1;
                    if (process.CpuUsage > 100) process.CpuUsage = 100;
                    
                    // Fluctuate memory usage
                    process.MemoryUsage += (_random.NextDouble() * 2 - 1) * 1000;
                    if (process.MemoryUsage < 100) process.MemoryUsage = 100;
                }
                else
                {
                    // Suspended processes have 0 CPU usage
                    process.CpuUsage = 0;
                }
            }
        }
    }
}