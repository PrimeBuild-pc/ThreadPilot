using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThreadPilot.Models;
using ThreadPilot.Platforms.Windows;

namespace ThreadPilot.Services
{
    public class ProcessService : IProcessService
    {
        private readonly ConcurrentDictionary<int, CpuSample> _cpuSamples = new();
        private readonly ConcurrentDictionary<int, IProcessCpuSetHandler> _cpuSetHandlers = new();
        private readonly ILogger<ProcessService>? _logger;
        private string ProfilesDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Profiles");
        private bool _useCpuSets = true; // Enable CPU Sets by default

        // Tracking for cleanup on exit
        private readonly ConcurrentDictionary<int, string> _appliedMasks = new(); // ProcessId -> MaskId
        private readonly ConcurrentDictionary<int, ProcessPriorityClass> _originalPriorities = new(); // ProcessId -> OriginalPriority

        public ProcessService(ILogger<ProcessService>? logger = null)
        {
            _logger = logger;

            if (!Directory.Exists(ProfilesDirectory))
            {
                Directory.CreateDirectory(ProfilesDirectory);
            }

            _logger?.LogInformation("ProcessService initialized with CPU Sets support enabled");
        }

        public async Task<ObservableCollection<ProcessModel>> GetProcessesAsync()
        {
            return await Task.Run(() =>
            {
                var processes = Process.GetProcesses()
                    .Select(CreateProcessModel)
                    .Where(p => p != null)
                    .OrderBy(p => p.Name);

                return new ObservableCollection<ProcessModel>(processes);
            });
        }

        private sealed class CpuSample
        {
            public CpuSample(TimeSpan totalProcessorTime, DateTime timestamp)
            {
                TotalProcessorTime = totalProcessorTime;
                Timestamp = timestamp;
            }

            public TimeSpan TotalProcessorTime { get; set; }
            public DateTime Timestamp { get; set; }
        }

        private double CalculateCpuUsage(Process process)
        {
            try
            {
                var now = DateTime.UtcNow;
                var totalProcessorTime = process.TotalProcessorTime;

                if (_cpuSamples.TryGetValue(process.Id, out var previous))
                {
                    var cpuDeltaMs = (totalProcessorTime - previous.TotalProcessorTime).TotalMilliseconds;
                    var timeDeltaMs = (now - previous.Timestamp).TotalMilliseconds;

                    // Ignore extremely small deltas to avoid noisy values
                    if (timeDeltaMs <= 0 || cpuDeltaMs < 0)
                    {
                        _cpuSamples[process.Id] = new CpuSample(totalProcessorTime, now);
                        return 0;
                    }

                    var usage = (cpuDeltaMs / (timeDeltaMs * Environment.ProcessorCount)) * 100.0;
                    usage = Math.Clamp(usage, 0, 100);

                    _cpuSamples[process.Id] = new CpuSample(totalProcessorTime, now);
                    return usage;
                }

                _cpuSamples[process.Id] = new CpuSample(totalProcessorTime, now);
                return 0; // First sample cannot produce a rate
            }
            catch
            {
                return 0;
            }
        }

        public ProcessModel CreateProcessModel(Process process)
        {
            var model = new ProcessModel();
            try
            {
                model.Process = process;
                model.ProcessId = process.Id;
                model.Name = process.ProcessName;
                model.MemoryUsage = process.PrivateMemorySize64;
                model.Priority = process.PriorityClass;
                model.ProcessorAffinity = (long)process.ProcessorAffinity;
                model.CpuUsage = CalculateCpuUsage(process);

                // Capture window information
                model.MainWindowHandle = process.MainWindowHandle;
                model.MainWindowTitle = process.MainWindowTitle ?? string.Empty;
                model.HasVisibleWindow = model.MainWindowHandle != IntPtr.Zero && !string.IsNullOrWhiteSpace(model.MainWindowTitle);

                // Try to get executable path
                try
                {
                    model.ExecutablePath = process.MainModule?.FileName ?? string.Empty;
                }
                catch
                {
                    model.ExecutablePath = string.Empty;
                }
            }
            catch
            {
                // Process may have terminated or access denied
                // Return a minimal model
                model.ProcessId = process.Id;
                model.Name = process.ProcessName;
            }

            return model;
        }

        public async Task SetProcessorAffinity(ProcessModel process, long affinityMask)
        {
            await Task.Run(() =>
            {
                try
                {
                    if (affinityMask == 0)
                    {
                        throw new InvalidOperationException("Affinity mask cannot be zero.");
                    }

                    // Try using CPU Sets first (Windows 10+)
                    if (_useCpuSets)
                    {
                        bool cpuSetSuccess = TrySetAffinityViaCpuSets(process, affinityMask);
                        if (cpuSetSuccess)
                        {
                            _logger?.LogInformation("Successfully applied CPU Sets affinity 0x{AffinityMask:X} to process {ProcessName} (PID: {ProcessId})",
                                affinityMask, process.Name, process.ProcessId);

                            // Update the model with the new affinity
                            process.ProcessorAffinity = affinityMask;
                            return;
                        }
                        else
                        {
                            _logger?.LogDebug("CPU Sets failed for process {ProcessName} (PID: {ProcessId}), falling back to classic ProcessorAffinity",
                                process.Name, process.ProcessId);
                        }
                    }

                    // Fallback to classic ProcessorAffinity method
                    var targetProcess = process.Process;
                    if (targetProcess == null || targetProcess.HasExited)
                    {
                        targetProcess = Process.GetProcessById(process.ProcessId);
                    }

                    targetProcess.ProcessorAffinity = new IntPtr(affinityMask);
                    process.Process = targetProcess;
                    process.ProcessorAffinity = (long)targetProcess.ProcessorAffinity;

                    _logger?.LogInformation("Successfully applied classic ProcessorAffinity 0x{AffinityMask:X} to process {ProcessName} (PID: {ProcessId})",
                        affinityMask, process.Name, process.ProcessId);
                }
                catch (Win32Exception ex) when (ex.NativeErrorCode == 87)
                {
                    throw new InvalidOperationException("Invalid affinity mask for this system.", ex);
                }
                catch (Win32Exception ex) when (ex.NativeErrorCode == 5)
                {
                    throw new InvalidOperationException("Access denied while setting processor affinity. The process may be protected (e.g., anti-cheat).", ex);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to set processor affinity: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Attempts to set process affinity using CPU Sets (Windows 10+ feature)
        /// </summary>
        private bool TrySetAffinityViaCpuSets(ProcessModel process, long affinityMask)
        {
            try
            {
                // Get or create CPU Set handler for this process
                var handler = _cpuSetHandlers.GetOrAdd(process.ProcessId, pid =>
                {
                    return new ProcessCpuSetHandler((uint)pid, process.Name, _logger);
                });

                // Check if handler is valid
                if (!handler.IsValid)
                {
                    _logger?.LogDebug("CPU Set handler for process {ProcessName} (PID: {ProcessId}) is invalid",
                        process.Name, process.ProcessId);

                    // Remove invalid handler
                    _cpuSetHandlers.TryRemove(process.ProcessId, out _);
                    return false;
                }

                // Apply the CPU Set mask
                bool success = handler.ApplyCpuSetMask(affinityMask, clearMask: false);

                if (!success)
                {
                    // Remove failed handler so we can try again later if needed
                    _cpuSetHandlers.TryRemove(process.ProcessId, out _);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Exception while applying CPU Sets to process {ProcessName} (PID: {ProcessId})",
                    process.Name, process.ProcessId);

                // Remove handler on exception
                _cpuSetHandlers.TryRemove(process.ProcessId, out _);
                return false;
            }
        }

        public async Task SetProcessPriority(ProcessModel process, ProcessPriorityClass priority)
        {
            await Task.Run(() =>
            {
                try
                {
                    var targetProcess = process.Process;
                    if (targetProcess == null || targetProcess.HasExited)
                    {
                        targetProcess = Process.GetProcessById(process.ProcessId);
                    }

                    targetProcess.PriorityClass = priority;
                    process.Process = targetProcess;
                    process.Priority = targetProcess.PriorityClass;
                }
                catch (Win32Exception ex) when (ex.NativeErrorCode == 5)
                {
                    throw new InvalidOperationException("Access denied while setting process priority. The process may be protected (e.g., anti-cheat).", ex);
                }
                catch (UnauthorizedAccessException ex)
                {
                    throw new InvalidOperationException("Access denied while setting process priority. The process may be protected (e.g., anti-cheat).", ex);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to set process priority: {ex.Message}");
                }
            });
        }

        public async Task<bool> SaveProcessProfile(string profileName, ProcessModel process)
        {
            var profile = new
            {
                ProcessName = process.Name,
                Priority = process.Priority,
                ProcessorAffinity = process.ProcessorAffinity
            };

            var filePath = Path.Combine(ProfilesDirectory, $"{profileName}.json");
            await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(profile));
            return true;
        }

        public async Task<bool> LoadProcessProfile(string profileName, ProcessModel process)
        {
            var filePath = Path.Combine(ProfilesDirectory, $"{profileName}.json");
            if (!File.Exists(filePath))
                return false;

            var content = await File.ReadAllTextAsync(filePath);
            var profile = JsonSerializer.Deserialize<dynamic>(content);

            if (profile != null)
            {
                await SetProcessPriority(process, profile.Priority);
                await SetProcessorAffinity(process, profile.ProcessorAffinity);
                return true;
            }

            return false;
        }

        public async Task RefreshProcessInfo(ProcessModel process)
        {
            await Task.Run(() =>
            {
                try
                {
                    var p = Process.GetProcessById(process.ProcessId);

                    // Check if process has exited
                    if (p.HasExited)
                    {
                        throw new InvalidOperationException("Process has exited");
                    }

                    process.MemoryUsage = p.PrivateMemorySize64;
                    process.Priority = p.PriorityClass;
                    process.ProcessorAffinity = (long)p.ProcessorAffinity;
                    process.CpuUsage = CalculateCpuUsage(p);
                    process.Process = p;

                    // Update window information
                    process.MainWindowHandle = p.MainWindowHandle;
                    process.MainWindowTitle = p.MainWindowTitle ?? string.Empty;
                    process.HasVisibleWindow = process.MainWindowHandle != IntPtr.Zero && !string.IsNullOrWhiteSpace(process.MainWindowTitle);
                }
                catch (ArgumentException)
                {
                    // Process with the specified ID does not exist
                    CleanupProcessResources(process.ProcessId);
                    throw new InvalidOperationException("Process no longer exists");
                }
                catch (Exception ex) when (ex.Message.Contains("exited") || ex.Message.Contains("terminated"))
                {
                    // Process has terminated
                    CleanupProcessResources(process.ProcessId);
                    throw new InvalidOperationException("Process has terminated");
                }
            });
        }

        /// <summary>
        /// Cleanup resources associated with a process
        /// </summary>
        private void CleanupProcessResources(int processId)
        {
            // Remove CPU samples
            _cpuSamples.TryRemove(processId, out _);

            // Dispose and remove CPU Set handler
            if (_cpuSetHandlers.TryRemove(processId, out var handler))
            {
                try
                {
                    handler.Dispose();
                    _logger?.LogDebug("Cleaned up CPU Set handler for process ID {ProcessId}", processId);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error disposing CPU Set handler for process ID {ProcessId}", processId);
                }
            }
        }

        /// <summary>
        /// Enables or disables the use of Windows CPU Sets for affinity management
        /// </summary>
        public void SetUseCpuSets(bool useCpuSets)
        {
            _useCpuSets = useCpuSets;
            _logger?.LogInformation("CPU Sets usage {Status}", useCpuSets ? "enabled" : "disabled");
        }

        /// <summary>
        /// Gets whether CPU Sets are currently enabled for affinity management
        /// </summary>
        public bool GetUseCpuSets()
        {
            return _useCpuSets;
        }

        /// <summary>
        /// Clears the CPU Set for a process (allows it to run on all cores)
        /// </summary>
        public async Task<bool> ClearProcessCpuSetAsync(ProcessModel process)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!_useCpuSets)
                    {
                        _logger?.LogDebug("CPU Sets are disabled, cannot clear CPU Set for process {ProcessName}", process.Name);
                        return false;
                    }

                    // Get or create CPU Set handler for this process
                    var handler = _cpuSetHandlers.GetOrAdd(process.ProcessId, pid =>
                    {
                        return new ProcessCpuSetHandler((uint)pid, process.Name, _logger);
                    });

                    if (!handler.IsValid)
                    {
                        _logger?.LogDebug("CPU Set handler for process {ProcessName} (PID: {ProcessId}) is invalid",
                            process.Name, process.ProcessId);
                        _cpuSetHandlers.TryRemove(process.ProcessId, out _);
                        return false;
                    }

                    // Clear the mask (set clearMask = true)
                    bool success = handler.ApplyCpuSetMask(0, clearMask: true);

                    if (success)
                    {
                        _logger?.LogInformation("Successfully cleared CPU Set for process {ProcessName} (PID: {ProcessId})",
                            process.Name, process.ProcessId);
                    }
                    else
                    {
                        _cpuSetHandlers.TryRemove(process.ProcessId, out _);
                    }

                    return success;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Exception while clearing CPU Set for process {ProcessName} (PID: {ProcessId})",
                        process.Name, process.ProcessId);
                    _cpuSetHandlers.TryRemove(process.ProcessId, out _);
                    return false;
                }
            });
        }

        public async Task<ProcessModel?> GetProcessByIdAsync(int processId)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var process = Process.GetProcessById(processId);
                    return CreateProcessModel(process);
                }
                catch
                {
                    return null;
                }
            });
        }

        public async Task<IEnumerable<ProcessModel>> GetProcessesByNameAsync(string executableName)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var processes = Process.GetProcessesByName(executableName)
                        .Select(CreateProcessModel)
                        .Where(p => p != null);

                    return processes;
                }
                catch
                {
                    return Enumerable.Empty<ProcessModel>();
                }
            });
        }

        public async Task<bool> IsProcessRunningAsync(string executableName)
        {
            var processes = await GetProcessesByNameAsync(executableName);
            return processes.Any();
        }

        public async Task<IEnumerable<ProcessModel>> GetProcessesWithPathsAsync()
        {
            return await Task.Run(() =>
            {
                var processes = Process.GetProcesses()
                    .Select(CreateProcessModel)
                    .Where(p => p != null && !string.IsNullOrEmpty(p.ExecutablePath))
                    .OrderBy(p => p.Name);

                return processes;
            });
        }

        public async Task<ObservableCollection<ProcessModel>> GetActiveApplicationsAsync()
        {
            return await Task.Run(() =>
            {
                var processes = Process.GetProcesses()
                    .Select(CreateProcessModel)
                    .Where(p => p != null && p.HasVisibleWindow)
                    .OrderBy(p => p.Name);

                return new ObservableCollection<ProcessModel>(processes);
            });
        }

        public async Task<bool> IsProcessStillRunning(ProcessModel process)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var p = Process.GetProcessById(process.ProcessId);
                    return !p.HasExited;
                }
                catch (ArgumentException)
                {
                    // Process with the specified ID does not exist
                    return false;
                }
                catch
                {
                    // Any other exception means process is not accessible/running
                    return false;
                }
            });
        }

        public async Task<bool> SetIdleServerStateAsync(ProcessModel process, bool enableIdleServer)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Get the actual process
                    var actualProcess = Process.GetProcessById(process.ProcessId);

                    // Use Windows API to set execution state for the process
                    // This prevents the system from entering idle state while the process is running
                    if (!enableIdleServer)
                    {
                        // Disable idle server by setting ES_CONTINUOUS | ES_SYSTEM_REQUIRED
                        // This keeps the system awake while the process is running
                        var result = NativeMethods.SetThreadExecutionState(
                            NativeMethods.EXECUTION_STATE.ES_CONTINUOUS |
                            NativeMethods.EXECUTION_STATE.ES_SYSTEM_REQUIRED);

                        return result != 0;
                    }
                    else
                    {
                        // Re-enable idle server by clearing the execution state
                        var result = NativeMethods.SetThreadExecutionState(
                            NativeMethods.EXECUTION_STATE.ES_CONTINUOUS);

                        return result != 0;
                    }
                }
                catch (Exception)
                {
                    return false;
                }
            });
        }

        public async Task<bool> SetRegistryPriorityAsync(ProcessModel process, bool enable, ProcessPriorityClass priority)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var key = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(
                        @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\" +
                        Path.GetFileName(process.ExecutablePath));

                    if (enable)
                    {
                        // Convert ProcessPriorityClass to registry priority value
                        int priorityValue = priority switch
                        {
                            ProcessPriorityClass.Idle => 4,
                            ProcessPriorityClass.BelowNormal => 6,
                            ProcessPriorityClass.Normal => 8,
                            ProcessPriorityClass.AboveNormal => 10,
                            ProcessPriorityClass.High => 13,
                            ProcessPriorityClass.RealTime => 24,
                            _ => 8 // Default to Normal
                        };

                        key.SetValue("PriorityClass", priorityValue, Microsoft.Win32.RegistryValueKind.DWord);
                    }
                    else
                    {
                        // Remove the registry entry to disable enforcement
                        key.DeleteValue("PriorityClass", false);
                    }

                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            });
        }

        /// <summary>
        /// Registers that a mask has been applied to a process (for tracking cleanup on exit)
        /// </summary>
        public void TrackAppliedMask(int processId, string maskId)
        {
            _appliedMasks[processId] = maskId;
            _logger?.LogDebug("Tracking mask {MaskId} applied to process {ProcessId}", maskId, processId);
        }

        /// <summary>
        /// Registers that a priority has been changed for a process (for tracking cleanup on exit)
        /// </summary>
        public void TrackPriorityChange(int processId, ProcessPriorityClass originalPriority)
        {
            // Only track if not already tracked (keep the original priority)
            if (!_originalPriorities.ContainsKey(processId))
            {
                _originalPriorities[processId] = originalPriority;
                _logger?.LogDebug("Tracking original priority {Priority} for process {ProcessId}", originalPriority, processId);
            }
        }

        /// <summary>
        /// Unregisters tracking when a process exits
        /// </summary>
        public void UntrackProcess(int processId)
        {
            _appliedMasks.TryRemove(processId, out _);
            _originalPriorities.TryRemove(processId, out _);
            CleanupProcessResources(processId);
            _logger?.LogDebug("Untracked process {ProcessId}", processId);
        }

        /// <summary>
        /// Clears all applied CPU masks/affinities from all tracked processes
        /// Processes return to using all cores (used on application exit)
        /// </summary>
        public async Task ClearAllAppliedMasksAsync()
        {
            _logger?.LogInformation("Clearing all applied CPU masks from {Count} tracked processes", _appliedMasks.Count);

            var processIds = _appliedMasks.Keys.ToList();
            var clearedCount = 0;
            var failedCount = 0;

            foreach (var processId in processIds)
            {
                try
                {
                    // Check if process is still running
                    Process process;
                    try
                    {
                        process = Process.GetProcessById(processId);
                        if (process.HasExited)
                        {
                            _appliedMasks.TryRemove(processId, out _);
                            continue;
                        }
                    }
                    catch (ArgumentException)
                    {
                        // Process no longer exists
                        _appliedMasks.TryRemove(processId, out _);
                        continue;
                    }

                    // Clear CPU Set if we have a handler
                    if (_cpuSetHandlers.TryGetValue(processId, out var handler) && handler.IsValid)
                    {
                        handler.ApplyCpuSetMask(0, clearMask: true);
                        _logger?.LogDebug("Cleared CPU Set for process {ProcessName} (PID: {ProcessId})", 
                            process.ProcessName, processId);
                    }

                    // Reset processor affinity to all cores
                    try
                    {
                        long allCoresMask = GetAllCoresAffinityMask();
                        process.ProcessorAffinity = new IntPtr(allCoresMask);
                        _logger?.LogDebug("Reset ProcessorAffinity for process {ProcessName} (PID: {ProcessId})", 
                            process.ProcessName, processId);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to reset ProcessorAffinity for process PID {ProcessId}", processId);
                    }

                    _appliedMasks.TryRemove(processId, out _);
                    clearedCount++;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to clear mask for process PID {ProcessId}", processId);
                    failedCount++;
                }
            }

            _logger?.LogInformation("Cleared CPU masks: {Cleared} succeeded, {Failed} failed", clearedCount, failedCount);
            await Task.CompletedTask;
        }

        /// <summary>
        /// Resets all modified process priorities to Normal (or their original priority)
        /// </summary>
        public async Task ResetAllProcessPrioritiesAsync()
        {
            _logger?.LogInformation("Resetting priorities for {Count} tracked processes", _originalPriorities.Count);

            var processIds = _originalPriorities.Keys.ToList();
            var resetCount = 0;
            var failedCount = 0;

            foreach (var processId in processIds)
            {
                try
                {
                    // Check if process is still running
                    Process process;
                    try
                    {
                        process = Process.GetProcessById(processId);
                        if (process.HasExited)
                        {
                            _originalPriorities.TryRemove(processId, out _);
                            continue;
                        }
                    }
                    catch (ArgumentException)
                    {
                        // Process no longer exists
                        _originalPriorities.TryRemove(processId, out _);
                        continue;
                    }

                    // Get original priority
                    if (_originalPriorities.TryGetValue(processId, out var originalPriority))
                    {
                        process.PriorityClass = originalPriority;
                        _logger?.LogDebug("Reset priority for process {ProcessName} (PID: {ProcessId}) to {Priority}", 
                            process.ProcessName, processId, originalPriority);
                    }

                    _originalPriorities.TryRemove(processId, out _);
                    resetCount++;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to reset priority for process PID {ProcessId}", processId);
                    failedCount++;
                }
            }

            _logger?.LogInformation("Reset priorities: {Reset} succeeded, {Failed} failed", resetCount, failedCount);
            await Task.CompletedTask;
        }

        /// <summary>
        /// Gets an affinity mask with all cores enabled
        /// </summary>
        private long GetAllCoresAffinityMask()
        {
            int coreCount = Environment.ProcessorCount;
            return coreCount >= 64 ? -1L : (1L << coreCount) - 1;
        }
    }

    /// <summary>
    /// Native methods for Windows API calls
    /// </summary>
    internal static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
        public static extern uint SetThreadExecutionState(EXECUTION_STATE esFlags);

        [System.Flags]
        public enum EXECUTION_STATE : uint
        {
            ES_AWAYMODE_REQUIRED = 0x00000040,
            ES_CONTINUOUS = 0x80000000,
            ES_DISPLAY_REQUIRED = 0x00000002,
            ES_SYSTEM_REQUIRED = 0x00000001
        }
    }
}