using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Implementation of system information operations
    /// </summary>
    public class SystemInfoService : ISystemInfoService
    {
        #region Win32 API imports
        
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();
        
        [DllImport("kernel32.dll")]
        private static extern bool GetProcessAffinityMask(IntPtr hProcess, out IntPtr lpProcessAffinityMask, out IntPtr lpSystemAffinityMask);
        
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
            
            public MEMORYSTATUSEX()
            {
                dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }
        
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);
        
        #endregion
        
        private readonly Dictionary<int, PerformanceCounter> _cpuCounters = new Dictionary<int, PerformanceCounter>();
        private readonly PerformanceCounter _totalCpuCounter;
        private readonly PerformanceCounter _memoryCounter;
        
        private SystemInfo _cachedSystemInfo;
        private Dictionary<int, double> _cpuCoreUtilization = new Dictionary<int, double>();
        private Dictionary<int, int> _cpuCoreFrequency = new Dictionary<int, int>();
        private Dictionary<int, double> _cpuCoreTemperature = new Dictionary<int, double>();
        
        private Timer _updateTimer;
        private bool _isMonitoring;
        private string _cachedOsVersion;
        private TimeSpan _cachedSystemUptime;
        private bool? _isLaptop;
        private string _cachedPowerProfileName;
        
        /// <summary>
        /// Occurs when system information is updated
        /// </summary>
        public event EventHandler<SystemInfo> SystemInfoUpdated;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="SystemInfoService"/> class
        /// </summary>
        public SystemInfoService()
        {
            // Initialize CPU performance counters
            try
            {
                _totalCpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _memoryCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use");
                
                var processorCount = Environment.ProcessorCount;
                for (int i = 0; i < processorCount; i++)
                {
                    try
                    {
                        var counter = new PerformanceCounter("Processor", "% Processor Time", i.ToString());
                        _cpuCounters[i] = counter;
                    }
                    catch
                    {
                        // Skip if counter can't be created
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize performance counters: {ex.Message}");
            }
            
            // Initialize system info
            _cachedSystemInfo = GetSystemInfoInternal();
        }
        
        /// <summary>
        /// Gets the system information
        /// </summary>
        /// <returns>The system information</returns>
        public SystemInfo GetSystemInfo()
        {
            if (_cachedSystemInfo == null)
            {
                _cachedSystemInfo = GetSystemInfoInternal();
            }
            
            // Update the dynamic values
            UpdateDynamicValues(_cachedSystemInfo);
            
            return _cachedSystemInfo;
        }
        
        /// <summary>
        /// Gets the list of CPU cores
        /// </summary>
        /// <returns>A list of CPU cores</returns>
        public List<CpuCore> GetCpuCores()
        {
            return GetSystemInfo().Cores.ToList();
        }
        
        /// <summary>
        /// Gets the CPU utilization per core
        /// </summary>
        /// <returns>A dictionary mapping core indices to utilization percentages</returns>
        public Dictionary<int, double> GetCpuCoreUtilization()
        {
            UpdateCpuUtilization();
            return new Dictionary<int, double>(_cpuCoreUtilization);
        }
        
        /// <summary>
        /// Gets the overall CPU utilization
        /// </summary>
        /// <returns>The overall CPU utilization percentage</returns>
        public double GetOverallCpuUtilization()
        {
            try
            {
                return _totalCpuCounter?.NextValue() ?? 0;
            }
            catch
            {
                return 0;
            }
        }
        
        /// <summary>
        /// Gets the memory utilization
        /// </summary>
        /// <returns>The memory utilization percentage</returns>
        public double GetMemoryUtilization()
        {
            try
            {
                return _memoryCounter?.NextValue() ?? 0;
            }
            catch
            {
                return 0;
            }
        }
        
        /// <summary>
        /// Gets the CPU frequency per core
        /// </summary>
        /// <returns>A dictionary mapping core indices to frequencies in MHz</returns>
        public Dictionary<int, int> GetCpuCoreFrequency()
        {
            UpdateCpuFrequency();
            return new Dictionary<int, int>(_cpuCoreFrequency);
        }
        
        /// <summary>
        /// Gets the CPU temperature per core
        /// </summary>
        /// <returns>A dictionary mapping core indices to temperatures in Celsius</returns>
        public Dictionary<int, double> GetCpuCoreTemperature()
        {
            UpdateCpuTemperature();
            return new Dictionary<int, double>(_cpuCoreTemperature);
        }
        
        /// <summary>
        /// Gets the overall CPU temperature
        /// </summary>
        /// <returns>The overall CPU temperature in Celsius</returns>
        public double GetOverallCpuTemperature()
        {
            UpdateCpuTemperature();
            
            if (_cpuCoreTemperature.Count == 0)
                return 0;
                
            return _cpuCoreTemperature.Values.Average();
        }
        
        /// <summary>
        /// Gets the system uptime
        /// </summary>
        /// <returns>The system uptime</returns>
        public TimeSpan GetSystemUptime()
        {
            try
            {
                if (_cachedSystemUptime == TimeSpan.Zero)
                {
                    _cachedSystemUptime = TimeSpan.FromMilliseconds(Environment.TickCount);
                }
                
                return _cachedSystemUptime;
            }
            catch
            {
                return TimeSpan.Zero;
            }
        }
        
        /// <summary>
        /// Gets the operating system version
        /// </summary>
        /// <returns>The operating system version</returns>
        public string GetOsVersion()
        {
            if (string.IsNullOrEmpty(_cachedOsVersion))
            {
                _cachedOsVersion = GetOsVersionInternal();
            }
            
            return _cachedOsVersion;
        }
        
        /// <summary>
        /// Gets whether the system is running on a laptop
        /// </summary>
        /// <returns>True if running on a laptop, false otherwise</returns>
        public bool IsLaptop()
        {
            if (!_isLaptop.HasValue)
            {
                _isLaptop = IsLaptopInternal();
            }
            
            return _isLaptop.Value;
        }
        
        /// <summary>
        /// Gets the current power profile name
        /// </summary>
        /// <returns>The current power profile name</returns>
        public string GetCurrentPowerProfileName()
        {
            if (string.IsNullOrEmpty(_cachedPowerProfileName))
            {
                _cachedPowerProfileName = GetCurrentPowerProfileNameInternal();
            }
            
            return _cachedPowerProfileName;
        }
        
        /// <summary>
        /// Starts monitoring system information
        /// </summary>
        /// <param name="updateInterval">The update interval in milliseconds</param>
        public void StartMonitoring(int updateInterval = 1000)
        {
            if (_isMonitoring)
                return;
                
            _updateTimer = new Timer(UpdateSystemInfo, null, 0, updateInterval);
            _isMonitoring = true;
        }
        
        /// <summary>
        /// Stops monitoring system information
        /// </summary>
        public void StopMonitoring()
        {
            if (!_isMonitoring)
                return;
                
            _updateTimer?.Dispose();
            _updateTimer = null;
            _isMonitoring = false;
        }
        
        private void UpdateSystemInfo(object state)
        {
            try
            {
                if (_cachedSystemInfo == null)
                {
                    _cachedSystemInfo = GetSystemInfoInternal();
                }
                
                // Update the dynamic values
                UpdateDynamicValues(_cachedSystemInfo);
                
                // Raise the event
                SystemInfoUpdated?.Invoke(this, _cachedSystemInfo);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to update system info: {ex.Message}");
            }
        }
        
        private void UpdateDynamicValues(SystemInfo systemInfo)
        {
            try
            {
                // Update CPU utilization
                UpdateCpuUtilization();
                systemInfo.CpuUtilization = GetOverallCpuUtilization();
                
                // Update CPU frequencies
                UpdateCpuFrequency();
                
                // Update CPU temperatures
                UpdateCpuTemperature();
                systemInfo.CpuTemperature = GetOverallCpuTemperature();
                
                // Update memory usage
                UpdateMemoryUsage(systemInfo);
                
                // Update system uptime
                systemInfo.Uptime = GetSystemUptime();
                
                // Update current power profile name
                systemInfo.CurrentPowerProfileName = GetCurrentPowerProfileName();
                
                // Update core information
                UpdateCoreInformation(systemInfo);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to update dynamic values: {ex.Message}");
            }
        }
        
        private void UpdateCpuUtilization()
        {
            try
            {
                foreach (var kvp in _cpuCounters)
                {
                    try
                    {
                        _cpuCoreUtilization[kvp.Key] = kvp.Value.NextValue();
                    }
                    catch
                    {
                        _cpuCoreUtilization[kvp.Key] = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to update CPU utilization: {ex.Message}");
            }
        }
        
        private void UpdateCpuFrequency()
        {
            try
            {
                // WMI query to get CPU frequency
                using (var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_Processor"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        var maxClockSpeed = Convert.ToInt32(obj["MaxClockSpeed"]);
                        var currentClockSpeed = Convert.ToInt32(obj["CurrentClockSpeed"]);
                        
                        // For simplicity, assume all cores run at the same frequency
                        for (int i = 0; i < Environment.ProcessorCount; i++)
                        {
                            _cpuCoreFrequency[i] = currentClockSpeed;
                        }
                        
                        if (_cachedSystemInfo != null)
                        {
                            _cachedSystemInfo.MaxCpuFrequency = maxClockSpeed;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to update CPU frequency: {ex.Message}");
            }
        }
        
        private void UpdateCpuTemperature()
        {
            try
            {
                // WMI query to get CPU temperature (may not be available on all systems)
                using (var searcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM MSAcpi_ThermalZoneTemperature"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        var temperatureKelvin = Convert.ToInt32(obj["CurrentTemperature"]);
                        var temperatureCelsius = (temperatureKelvin / 10.0) - 273.15;
                        
                        // For simplicity, assume all cores have the same temperature
                        for (int i = 0; i < Environment.ProcessorCount; i++)
                        {
                            _cpuCoreTemperature[i] = temperatureCelsius;
                        }
                    }
                }
            }
            catch
            {
                // Temperature information might not be available
                // Just set to 0 for all cores
                for (int i = 0; i < Environment.ProcessorCount; i++)
                {
                    _cpuCoreTemperature[i] = 0;
                }
            }
        }
        
        private void UpdateMemoryUsage(SystemInfo systemInfo)
        {
            try
            {
                var memoryStatus = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(memoryStatus))
                {
                    systemInfo.TotalMemory = (long)memoryStatus.ullTotalPhys;
                    systemInfo.AvailableMemory = (long)memoryStatus.ullAvailPhys;
                    systemInfo.MemoryUtilization = memoryStatus.dwMemoryLoad;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to update memory usage: {ex.Message}");
            }
        }
        
        private void UpdateCoreInformation(SystemInfo systemInfo)
        {
            try
            {
                // Update core utilization
                foreach (var core in systemInfo.Cores)
                {
                    if (_cpuCoreUtilization.TryGetValue(core.Index, out var utilization))
                    {
                        core.Utilization = utilization;
                    }
                    
                    if (_cpuCoreFrequency.TryGetValue(core.Index, out var frequency))
                    {
                        core.Frequency = frequency;
                    }
                    
                    if (_cpuCoreTemperature.TryGetValue(core.Index, out var temperature))
                    {
                        core.Temperature = temperature;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to update core information: {ex.Message}");
            }
        }
        
        private SystemInfo GetSystemInfoInternal()
        {
            try
            {
                var systemInfo = new SystemInfo
                {
                    OsName = "Windows",
                    OsVersion = GetOsVersion(),
                    LogicalProcessorCount = Environment.ProcessorCount,
                    IsLaptop = IsLaptop(),
                    CurrentPowerProfileName = GetCurrentPowerProfileName(),
                    Uptime = GetSystemUptime()
                };
                
                // Get CPU information
                GetCpuInformation(systemInfo);
                
                // Get memory information
                UpdateMemoryUsage(systemInfo);
                
                return systemInfo;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to get system info: {ex.Message}");
                return new SystemInfo
                {
                    OsName = "Windows",
                    OsVersion = Environment.OSVersion.VersionString,
                    LogicalProcessorCount = Environment.ProcessorCount
                };
            }
        }
        
        private void GetCpuInformation(SystemInfo systemInfo)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_Processor"))
                {
                    int socketCount = 0;
                    
                    foreach (var obj in searcher.Get())
                    {
                        socketCount++;
                        
                        if (socketCount == 1)
                        {
                            systemInfo.CpuName = obj["Name"]?.ToString();
                            systemInfo.CpuVendor = obj["Manufacturer"]?.ToString();
                            systemInfo.CpuArchitecture = obj["Architecture"]?.ToString();
                            systemInfo.PhysicalCoreCount = Convert.ToInt32(obj["NumberOfCores"]);
                            systemInfo.MaxCpuFrequency = Convert.ToInt32(obj["MaxClockSpeed"]);
                        }
                    }
                    
                    systemInfo.CpuSocketCount = socketCount;
                    systemInfo.HasMultiplePackages = socketCount > 1;
                }
                
                // Check for hybrid cores (E-cores and P-cores)
                CheckHybridCores(systemInfo);
                
                // Get CPU core information
                GetCpuCoreInformation(systemInfo);
                
                // Get system manufacturer and model
                GetSystemManufacturerAndModel(systemInfo);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to get CPU information: {ex.Message}");
            }
        }
        
        private void CheckHybridCores(SystemInfo systemInfo)
        {
            try
            {
                // Check if the CPU name contains known hybrid CPU indicators
                var cpuName = systemInfo.CpuName?.ToLower() ?? "";
                
                if (cpuName.Contains("12th gen") || cpuName.Contains("13th gen") ||
                    cpuName.Contains("core i5-12") || cpuName.Contains("core i7-12") ||
                    cpuName.Contains("core i9-12") || cpuName.Contains("core i5-13") ||
                    cpuName.Contains("core i7-13") || cpuName.Contains("core i9-13"))
                {
                    systemInfo.HasHybridCores = true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to check hybrid cores: {ex.Message}");
            }
        }
        
        private void GetCpuCoreInformation(SystemInfo systemInfo)
        {
            try
            {
                // Get the system affinity mask
                IntPtr processAffinityMask, systemAffinityMask;
                if (GetProcessAffinityMask(GetCurrentProcess(), out processAffinityMask, out systemAffinityMask))
                {
                    var processorCount = Environment.ProcessorCount;
                    
                    for (int i = 0; i < processorCount; i++)
                    {
                        var core = new CpuCore
                        {
                            Index = i,
                            LogicalProcessorIndex = i,
                            CoreId = i / 2, // Assuming hyperthreading
                            IsHyperthreaded = i % 2 == 1 // Every odd core is a hyperthread
                        };
                        
                        // For Intel 12th/13th gen hybrid processors
                        if (systemInfo.HasHybridCores)
                        {
                            // Simplified logic: assume first 8 cores are P-cores, rest are E-cores
                            if (core.CoreId < 4)
                            {
                                core.IsPerformanceCore = true;
                            }
                            else
                            {
                                core.IsEfficiencyCore = true;
                            }
                        }
                        
                        systemInfo.Cores.Add(core);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to get CPU core information: {ex.Message}");
            }
        }
        
        private void GetSystemManufacturerAndModel(SystemInfo systemInfo)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_ComputerSystem"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        systemInfo.SystemManufacturer = obj["Manufacturer"]?.ToString();
                        systemInfo.SystemModel = obj["Model"]?.ToString();
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to get system manufacturer and model: {ex.Message}");
            }
        }
        
        private string GetOsVersionInternal()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_OperatingSystem"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        return obj["Caption"]?.ToString();
                    }
                }
                
                return Environment.OSVersion.VersionString;
            }
            catch
            {
                return Environment.OSVersion.VersionString;
            }
        }
        
        private bool IsLaptopInternal()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_Battery"))
                {
                    return searcher.Get().Count > 0;
                }
            }
            catch
            {
                return false;
            }
        }
        
        private string GetCurrentPowerProfileNameInternal()
        {
            try
            {
                // For simplicity, we'll use the PowerProfileService to get the active profile name
                var powerProfileService = new PowerProfileService();
                var activeProfile = powerProfileService.GetActiveProfile();
                return activeProfile?.Name ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}