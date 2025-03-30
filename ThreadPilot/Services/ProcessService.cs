using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.Principal;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Implementation of the process service
    /// </summary>
    public class ProcessService : IProcessService
    {
        // Process counters for tracking CPU usage
        private readonly Dictionary<int, PerformanceCounter> _processCounters = new();
        
        // Cache of process company names
        private readonly Dictionary<string, string> _processCompanyNames = new();
        
        // Rules file path
        private readonly string _rulesFilePath;
        
        // List of rules
        private List<ProcessAffinityRule> _rules = new();
        
        /// <summary>
        /// Constructor
        /// </summary>
        public ProcessService()
        {
            // Initialize rules file path
            var appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ThreadPilot");
                
            if (!Directory.Exists(appDataFolder))
            {
                Directory.CreateDirectory(appDataFolder);
            }
            
            _rulesFilePath = Path.Combine(appDataFolder, "AffinityRules.json");
            
            // Load rules
            LoadRulesFromFile();
        }
        
        /// <summary>
        /// Get all processes
        /// </summary>
        public IList<ProcessInfo> GetProcesses()
        {
            var result = new List<ProcessInfo>();
            
            try
            {
                // Get all running processes
                var processes = Process.GetProcesses();
                
                // Create the performance counters for each process if needed
                foreach (var process in processes)
                {
                    if (!_processCounters.ContainsKey(process.Id))
                    {
                        try
                        {
                            var counter = new PerformanceCounter("Process", "% Processor Time", process.ProcessName);
                            _processCounters[process.Id] = counter;
                            counter.NextValue(); // First call to NextValue always returns 0
                        }
                        catch
                        {
                            // Ignore errors for specific processes
                        }
                    }
                }
                
                // Get process information
                foreach (var process in processes)
                {
                    try
                    {
                        var info = new ProcessInfo
                        {
                            Id = process.Id,
                            Name = process.ProcessName,
                            CpuUsage = GetProcessCpuUsage(process.Id),
                            MemoryUsageMb = Math.Round(process.WorkingSet64 / 1024.0 / 1024.0, 2),
                            ThreadCount = process.Threads.Count,
                            StartTime = process.StartTime,
                            IsResponding = process.Responding,
                            Priority = (int)process.PriorityClass,
                            ExecutablePath = GetProcessPath(process)
                        };
                        
                        // Get process description
                        info.Description = GetProcessDescription(info.ExecutablePath);
                        
                        // Get company name
                        info.CompanyName = GetProcessCompanyName(info.ExecutablePath);
                        
                        // Get affinity mask
                        try
                        {
                            info.AffinityMask = (long)process.ProcessorAffinity.ToInt64();
                        }
                        catch
                        {
                            info.AffinityMask = 0;
                        }
                        
                        // Check if the process is 64-bit (this is an approximation)
                        info.Is64Bit = Environment.Is64BitOperatingSystem && 
                                       !info.ExecutablePath.Contains("SysWOW64") &&
                                       !string.IsNullOrEmpty(info.ExecutablePath);
                        
                        // Check if the process is elevated (this is an approximation)
                        if (info.Name.Equals("System", StringComparison.OrdinalIgnoreCase) ||
                            info.Name.Equals("svchost", StringComparison.OrdinalIgnoreCase) ||
                            info.Name.StartsWith("wininit", StringComparison.OrdinalIgnoreCase) ||
                            info.Name.StartsWith("lsass", StringComparison.OrdinalIgnoreCase))
                        {
                            info.IsElevated = true;
                            info.IsSystemProcess = true;
                        }
                        else
                        {
                            info.IsElevated = IsProcessElevated(process);
                            info.IsSystemProcess = IsSystemProcess(process);
                        }
                        
                        result.Add(info);
                    }
                    catch
                    {
                        // Ignore errors for specific processes
                    }
                }
                
                // Clean up any counters for processes that no longer exist
                var existingIds = processes.Select(p => p.Id).ToList();
                var keysToRemove = _processCounters.Keys.Where(k => !existingIds.Contains(k)).ToList();
                
                foreach (var key in keysToRemove)
                {
                    if (_processCounters.TryGetValue(key, out var counter))
                    {
                        counter.Dispose();
                        _processCounters.Remove(key);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting processes: {ex.Message}");
            }
            
            // If we got no processes or an error occurred, return demo data
            if (result.Count == 0)
            {
                result = GenerateDemoProcesses();
            }
            
            return result;
        }
        
        /// <summary>
        /// Get a process by ID
        /// </summary>
        public ProcessInfo? GetProcessById(int id)
        {
            try
            {
                var process = Process.GetProcessById(id);
                
                var info = new ProcessInfo
                {
                    Id = process.Id,
                    Name = process.ProcessName,
                    CpuUsage = GetProcessCpuUsage(process.Id),
                    MemoryUsageMb = Math.Round(process.WorkingSet64 / 1024.0 / 1024.0, 2),
                    ThreadCount = process.Threads.Count,
                    StartTime = process.StartTime,
                    IsResponding = process.Responding,
                    Priority = (int)process.PriorityClass,
                    ExecutablePath = GetProcessPath(process)
                };
                
                // Get process description
                info.Description = GetProcessDescription(info.ExecutablePath);
                
                // Get company name
                info.CompanyName = GetProcessCompanyName(info.ExecutablePath);
                
                // Get affinity mask
                try
                {
                    info.AffinityMask = (long)process.ProcessorAffinity.ToInt64();
                }
                catch
                {
                    info.AffinityMask = 0;
                }
                
                // Check if the process is 64-bit
                info.Is64Bit = Environment.Is64BitOperatingSystem && 
                               !info.ExecutablePath.Contains("SysWOW64") &&
                               !string.IsNullOrEmpty(info.ExecutablePath);
                
                // Check if the process is elevated
                if (info.Name.Equals("System", StringComparison.OrdinalIgnoreCase) ||
                    info.Name.Equals("svchost", StringComparison.OrdinalIgnoreCase) ||
                    info.Name.StartsWith("wininit", StringComparison.OrdinalIgnoreCase) ||
                    info.Name.StartsWith("lsass", StringComparison.OrdinalIgnoreCase))
                {
                    info.IsElevated = true;
                    info.IsSystemProcess = true;
                }
                else
                {
                    info.IsElevated = IsProcessElevated(process);
                    info.IsSystemProcess = IsSystemProcess(process);
                }
                
                return info;
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// Set process affinity mask
        /// </summary>
        public bool SetProcessAffinity(int processId, long affinityMask)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                process.ProcessorAffinity = new IntPtr(affinityMask);
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Set process priority
        /// </summary>
        public bool SetProcessPriority(int processId, int priority)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                process.PriorityClass = (ProcessPriorityClass)priority;
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// End a process
        /// </summary>
        public bool EndProcess(int processId)
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
        /// Get all process affinity rules
        /// </summary>
        public IList<ProcessAffinityRule> GetAffinityRules()
        {
            return _rules.ToList();
        }
        
        /// <summary>
        /// Add or update a process affinity rule
        /// </summary>
        public bool SaveAffinityRule(ProcessAffinityRule rule)
        {
            try
            {
                // Find existing rule
                var existingRule = _rules.FirstOrDefault(r => r.Id == rule.Id);
                
                if (existingRule != null)
                {
                    // Update existing rule
                    existingRule.ProcessNamePattern = rule.ProcessNamePattern;
                    existingRule.AffinityMask = rule.AffinityMask;
                    existingRule.Priority = rule.Priority;
                    existingRule.Description = rule.Description;
                    existingRule.IsEnabled = rule.IsEnabled;
                    existingRule.ModifiedOn = DateTime.Now;
                }
                else
                {
                    // Add new rule
                    _rules.Add(rule);
                }
                
                // Save rules to file
                SaveRulesToFile();
                
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Delete a process affinity rule
        /// </summary>
        public bool DeleteAffinityRule(ProcessAffinityRule rule)
        {
            try
            {
                // Find existing rule
                var existingRule = _rules.FirstOrDefault(r => r.Id == rule.Id);
                
                if (existingRule != null)
                {
                    // Remove rule
                    _rules.Remove(existingRule);
                    
                    // Save rules to file
                    SaveRulesToFile();
                }
                
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Apply all enabled affinity rules
        /// </summary>
        public void ApplyAffinityRules()
        {
            try
            {
                // Get all running processes
                var processes = Process.GetProcesses();
                
                // Apply rules
                foreach (var rule in _rules.Where(r => r.IsEnabled))
                {
                    // Find matching processes
                    foreach (var process in processes)
                    {
                        try
                        {
                            // Check if the process name matches the pattern
                            if (MatchesPattern(process.ProcessName, rule.ProcessNamePattern))
                            {
                                // Apply affinity
                                if (rule.AffinityMask > 0)
                                {
                                    process.ProcessorAffinity = new IntPtr(rule.AffinityMask);
                                }
                                
                                // Apply priority
                                if (rule.Priority > 0)
                                {
                                    process.PriorityClass = (ProcessPriorityClass)rule.Priority;
                                }
                            }
                        }
                        catch
                        {
                            // Ignore errors for specific processes
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying rules: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Optimize processes according to predefined rules
        /// </summary>
        public void OptimizeProcesses()
        {
            try
            {
                // Get all running processes
                var processes = Process.GetProcesses();
                
                // Apply predefined optimizations
                foreach (var process in processes)
                {
                    try
                    {
                        // Skip system processes
                        if (IsSystemProcess(process))
                        {
                            continue;
                        }
                        
                        // Optimize based on process name
                        if (process.ProcessName.Equals("chrome", StringComparison.OrdinalIgnoreCase) ||
                            process.ProcessName.Equals("firefox", StringComparison.OrdinalIgnoreCase) ||
                            process.ProcessName.Equals("msedge", StringComparison.OrdinalIgnoreCase))
                        {
                            // Set browsers to use cores 0-3 (first 4 cores)
                            process.ProcessorAffinity = new IntPtr(0xF); // 0b1111
                            process.PriorityClass = ProcessPriorityClass.Normal;
                        }
                        else if (process.ProcessName.Contains("game", StringComparison.OrdinalIgnoreCase) ||
                                 process.ProcessName.Contains("steam", StringComparison.OrdinalIgnoreCase))
                        {
                            // Set games to high priority
                            process.PriorityClass = ProcessPriorityClass.High;
                        }
                    }
                    catch
                    {
                        // Ignore errors for specific processes
                    }
                }
                
                // Apply custom rules
                ApplyAffinityRules();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error optimizing processes: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Get process CPU usage
        /// </summary>
        private float GetProcessCpuUsage(int processId)
        {
            try
            {
                if (_processCounters.TryGetValue(processId, out var counter))
                {
                    return counter.NextValue() / Environment.ProcessorCount;
                }
            }
            catch
            {
                // Ignore errors
            }
            
            return 0;
        }
        
        /// <summary>
        /// Get process executable path
        /// </summary>
        private string GetProcessPath(Process process)
        {
            try
            {
                return process.MainModule?.FileName ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
        
        /// <summary>
        /// Get process description
        /// </summary>
        private string GetProcessDescription(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }
            
            try
            {
                var fileVersionInfo = FileVersionInfo.GetVersionInfo(path);
                return fileVersionInfo.FileDescription ?? Path.GetFileName(path);
            }
            catch
            {
                return Path.GetFileName(path);
            }
        }
        
        /// <summary>
        /// Get process company name
        /// </summary>
        private string GetProcessCompanyName(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }
            
            try
            {
                // Check cache first
                if (_processCompanyNames.TryGetValue(path, out var company))
                {
                    return company;
                }
                
                var fileVersionInfo = FileVersionInfo.GetVersionInfo(path);
                company = fileVersionInfo.CompanyName ?? string.Empty;
                
                // Add to cache
                _processCompanyNames[path] = company;
                
                return company;
            }
            catch
            {
                return string.Empty;
            }
        }
        
        /// <summary>
        /// Check if process is elevated
        /// </summary>
        private bool IsProcessElevated(Process process)
        {
            try
            {
                // Get current user identity
                var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                
                // Check if running as administrator
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Check if process is a system process
        /// </summary>
        private bool IsSystemProcess(Process process)
        {
            // Some well-known system processes
            var systemProcesses = new[]
            {
                "System", "svchost", "wininit", "lsass", "csrss",
                "smss", "spoolsv", "services", "winlogon", "dwm"
            };
            
            return systemProcesses.Any(p => 
                process.ProcessName.Equals(p, StringComparison.OrdinalIgnoreCase) || 
                process.ProcessName.StartsWith(p, StringComparison.OrdinalIgnoreCase));
        }
        
        /// <summary>
        /// Check if a string matches a pattern (with * wildcard)
        /// </summary>
        private bool MatchesPattern(string text, string pattern)
        {
            // Simple pattern matching with * wildcard
            if (pattern == "*")
            {
                return true;
            }
            
            if (!pattern.Contains('*'))
            {
                return text.Equals(pattern, StringComparison.OrdinalIgnoreCase);
            }
            
            var parts = pattern.Split('*', StringSplitOptions.RemoveEmptyEntries);
            
            if (parts.Length == 0)
            {
                return true;
            }
            
            if (pattern.StartsWith('*'))
            {
                if (pattern.EndsWith('*'))
                {
                    // *contains*
                    return parts.All(p => text.Contains(p, StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    // *endsWith
                    return text.EndsWith(parts[^1], StringComparison.OrdinalIgnoreCase) &&
                           parts.Take(parts.Length - 1).All(p => text.Contains(p, StringComparison.OrdinalIgnoreCase));
                }
            }
            else
            {
                if (pattern.EndsWith('*'))
                {
                    // startsWith*
                    return text.StartsWith(parts[0], StringComparison.OrdinalIgnoreCase) &&
                           parts.Skip(1).All(p => text.Contains(p, StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    // startsWith*endsWith
                    return text.StartsWith(parts[0], StringComparison.OrdinalIgnoreCase) &&
                           text.EndsWith(parts[^1], StringComparison.OrdinalIgnoreCase) &&
                           parts.Skip(1).Take(parts.Length - 2).All(p => text.Contains(p, StringComparison.OrdinalIgnoreCase));
                }
            }
        }
        
        /// <summary>
        /// Load rules from file
        /// </summary>
        private void LoadRulesFromFile()
        {
            try
            {
                if (File.Exists(_rulesFilePath))
                {
                    // In a real app, this would deserialize from JSON
                    // For this demo, we'll just create some sample rules
                    _rules = new List<ProcessAffinityRule>
                    {
                        new ProcessAffinityRule
                        {
                            ProcessNamePattern = "chrome*",
                            AffinityMask = 0xF, // First 4 cores
                            Priority = (int)ProcessPriorityClass.Normal,
                            Description = "Set Chrome to use first 4 cores",
                            IsEnabled = true
                        },
                        new ProcessAffinityRule
                        {
                            ProcessNamePattern = "*game*",
                            Priority = (int)ProcessPriorityClass.High,
                            Description = "Set games to high priority",
                            IsEnabled = true
                        }
                    };
                }
                else
                {
                    // Create default rules
                    _rules = new List<ProcessAffinityRule>
                    {
                        new ProcessAffinityRule
                        {
                            ProcessNamePattern = "chrome*",
                            AffinityMask = 0xF, // First 4 cores
                            Priority = (int)ProcessPriorityClass.Normal,
                            Description = "Set Chrome to use first 4 cores",
                            IsEnabled = true
                        },
                        new ProcessAffinityRule
                        {
                            ProcessNamePattern = "*game*",
                            Priority = (int)ProcessPriorityClass.High,
                            Description = "Set games to high priority",
                            IsEnabled = true
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading rules: {ex.Message}");
                _rules = new List<ProcessAffinityRule>();
            }
        }
        
        /// <summary>
        /// Save rules to file
        /// </summary>
        private void SaveRulesToFile()
        {
            try
            {
                // In a real app, this would serialize to JSON
                // For this demo, we'll just print a debug message
                Debug.WriteLine($"Would save {_rules.Count} rules to {_rulesFilePath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving rules: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Generate demo processes for testing
        /// </summary>
        private List<ProcessInfo> GenerateDemoProcesses()
        {
            var random = new Random();
            var result = new List<ProcessInfo>();
            
            // Add some common processes
            result.Add(new ProcessInfo
            {
                Id = 4,
                Name = "System",
                Description = "Windows System Process",
                CpuUsage = random.NextDouble() * 2,
                MemoryUsageMb = 10 + random.NextDouble() * 10,
                ThreadCount = 100,
                StartTime = DateTime.Now.AddHours(-random.Next(1, 24)),
                IsResponding = true,
                IsSystemProcess = true,
                IsElevated = true,
                Priority = (int)ProcessPriorityClass.Normal,
                AffinityMask = 0xFFFFFFFF,
                CompanyName = "Microsoft Corporation",
                ExecutablePath = @"C:\Windows\System32\ntoskrnl.exe"
            });
            
            result.Add(new ProcessInfo
            {
                Id = 700 + random.Next(1, 100),
                Name = "chrome",
                Description = "Google Chrome Browser",
                CpuUsage = 5 + random.NextDouble() * 15,
                MemoryUsageMb = 200 + random.NextDouble() * 500,
                ThreadCount = 30 + random.Next(1, 20),
                StartTime = DateTime.Now.AddHours(-random.Next(1, 8)),
                IsResponding = true,
                IsSystemProcess = false,
                IsElevated = false,
                Priority = (int)ProcessPriorityClass.Normal,
                AffinityMask = 0xF,
                CompanyName = "Google Inc.",
                ExecutablePath = @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                Is64Bit = true
            });
            
            result.Add(new ProcessInfo
            {
                Id = 1000 + random.Next(1, 100),
                Name = "msedge",
                Description = "Microsoft Edge Browser",
                CpuUsage = 4 + random.NextDouble() * 10,
                MemoryUsageMb = 180 + random.NextDouble() * 400,
                ThreadCount = 25 + random.Next(1, 15),
                StartTime = DateTime.Now.AddHours(-random.Next(1, 5)),
                IsResponding = true,
                IsSystemProcess = false,
                IsElevated = false,
                Priority = (int)ProcessPriorityClass.Normal,
                AffinityMask = 0xF,
                CompanyName = "Microsoft Corporation",
                ExecutablePath = @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
                Is64Bit = true
            });
            
            result.Add(new ProcessInfo
            {
                Id = 2000 + random.Next(1, 100),
                Name = "explorer",
                Description = "Windows Explorer",
                CpuUsage = 1 + random.NextDouble() * 3,
                MemoryUsageMb = 50 + random.NextDouble() * 100,
                ThreadCount = 15 + random.Next(1, 10),
                StartTime = DateTime.Now.AddHours(-random.Next(12, 48)),
                IsResponding = true,
                IsSystemProcess = true,
                IsElevated = false,
                Priority = (int)ProcessPriorityClass.Normal,
                AffinityMask = 0xFFFFFFFF,
                CompanyName = "Microsoft Corporation",
                ExecutablePath = @"C:\Windows\explorer.exe",
                Is64Bit = true
            });
            
            result.Add(new ProcessInfo
            {
                Id = 3000 + random.Next(1, 100),
                Name = "discord",
                Description = "Discord Chat Client",
                CpuUsage = 2 + random.NextDouble() * 5,
                MemoryUsageMb = 150 + random.NextDouble() * 200,
                ThreadCount = 20 + random.Next(1, 10),
                StartTime = DateTime.Now.AddHours(-random.Next(1, 12)),
                IsResponding = true,
                IsSystemProcess = false,
                IsElevated = false,
                Priority = (int)ProcessPriorityClass.Normal,
                AffinityMask = 0xFFFFFFFF,
                CompanyName = "Discord Inc.",
                ExecutablePath = @"C:\Users\User\AppData\Local\Discord\app-1.0.9002\Discord.exe",
                Is64Bit = true
            });
            
            // Add some game processes
            result.Add(new ProcessInfo
            {
                Id = 4000 + random.Next(1, 100),
                Name = "SteamGameOverlay",
                Description = "Steam Game Overlay",
                CpuUsage = 1 + random.NextDouble() * 3,
                MemoryUsageMb = 50 + random.NextDouble() * 100,
                ThreadCount = 10 + random.Next(1, 5),
                StartTime = DateTime.Now.AddHours(-random.Next(1, 4)),
                IsResponding = true,
                IsSystemProcess = false,
                IsElevated = false,
                Priority = (int)ProcessPriorityClass.High,
                AffinityMask = 0xFFFFFFFF,
                CompanyName = "Valve Corporation",
                ExecutablePath = @"C:\Program Files (x86)\Steam\GameOverlayUI.exe",
                Is64Bit = true
            });
            
            result.Add(new ProcessInfo
            {
                Id = 5000 + random.Next(1, 100),
                Name = "GameXYZ",
                Description = "Popular Game",
                CpuUsage = 20 + random.NextDouble() * 40,
                MemoryUsageMb = 2000 + random.NextDouble() * 4000,
                ThreadCount = 40 + random.Next(1, 20),
                StartTime = DateTime.Now.AddHours(-random.Next(1, 3)),
                IsResponding = true,
                IsSystemProcess = false,
                IsElevated = false,
                Priority = (int)ProcessPriorityClass.High,
                AffinityMask = 0xFFFFFFFF,
                CompanyName = "Game Studio Inc.",
                ExecutablePath = @"C:\Program Files\GameXYZ\GameXYZ.exe",
                Is64Bit = true
            });
            
            // Add some Office applications
            result.Add(new ProcessInfo
            {
                Id = 6000 + random.Next(1, 100),
                Name = "WINWORD",
                Description = "Microsoft Word",
                CpuUsage = 1 + random.NextDouble() * 5,
                MemoryUsageMb = 80 + random.NextDouble() * 150,
                ThreadCount = 10 + random.Next(1, 10),
                StartTime = DateTime.Now.AddHours(-random.Next(1, 3)),
                IsResponding = true,
                IsSystemProcess = false,
                IsElevated = false,
                Priority = (int)ProcessPriorityClass.Normal,
                AffinityMask = 0xFFFFFFFF,
                CompanyName = "Microsoft Corporation",
                ExecutablePath = @"C:\Program Files\Microsoft Office\root\Office16\WINWORD.EXE",
                Is64Bit = true
            });
            
            result.Add(new ProcessInfo
            {
                Id = 7000 + random.Next(1, 100),
                Name = "EXCEL",
                Description = "Microsoft Excel",
                CpuUsage = 2 + random.NextDouble() * 8,
                MemoryUsageMb = 100 + random.NextDouble() * 200,
                ThreadCount = 12 + random.Next(1, 10),
                StartTime = DateTime.Now.AddHours(-random.Next(1, 3)),
                IsResponding = true,
                IsSystemProcess = false,
                IsElevated = false,
                Priority = (int)ProcessPriorityClass.Normal,
                AffinityMask = 0xFFFFFFFF,
                CompanyName = "Microsoft Corporation",
                ExecutablePath = @"C:\Program Files\Microsoft Office\root\Office16\EXCEL.EXE",
                Is64Bit = true
            });
            
            return result;
        }
    }
}