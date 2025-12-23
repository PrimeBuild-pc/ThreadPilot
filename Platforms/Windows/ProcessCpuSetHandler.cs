/*
 * ThreadPilot - Advanced Windows Process and Power Plan Manager
 * Copyright (C) 2025 Prime Build
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, version 3 only.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ThreadPilot.Platforms.Windows
{
    /// <summary>
    /// Handles CPU Set operations for a specific process using Windows APIs
    /// Based on CPUSetSetter's ProcessHandlerWindows implementation
    /// </summary>
    public class ProcessCpuSetHandler : IProcessCpuSetHandler
    {
        private static readonly Dictionary<int, uint> _cpuSetIdPerLogicalProcessor;
        private static readonly object _staticInitLock = new object();
        private static bool _staticInitialized = false;

        private readonly Queue<CpuTimeTimestamp> _cpuTimeMovingAverageBuffer = new();
        private readonly string _executableName;
        private readonly uint _pid;
        private readonly ILogger? _logger;

        private SafeProcessHandle? _queryLimitedInfoHandle;
        private SafeProcessHandle? _setLimitedInfoHandle;
        private bool _disposed = false;

        static ProcessCpuSetHandler()
        {
            _cpuSetIdPerLogicalProcessor = new Dictionary<int, uint>();
        }

        public ProcessCpuSetHandler(uint processId, string executableName, ILogger? logger = null)
        {
            _pid = processId;
            _executableName = executableName ?? $"PID_{processId}";
            _logger = logger;

            // Initialize CPU Set mapping on first use
            EnsureStaticInitialization();

            // Open handle for querying process information
            _queryLimitedInfoHandle = CpuSetNativeMethods.OpenProcess(
                ProcessAccessFlags.PROCESS_QUERY_LIMITED_INFORMATION,
                false,
                processId);

            if (_queryLimitedInfoHandle == null || _queryLimitedInfoHandle.IsInvalid)
            {
                var error = Marshal.GetLastWin32Error();
                _logger?.LogWarning("Failed to open process {ProcessId} for querying: {Error}", processId, new Win32Exception(error).Message);
            }
        }

        public uint ProcessId => _pid;
        public string ExecutableName => _executableName;
        public bool IsValid => _queryLimitedInfoHandle != null && !_queryLimitedInfoHandle.IsInvalid;

        private static void EnsureStaticInitialization()
        {
            if (_staticInitialized) return;

            lock (_staticInitLock)
            {
                if (_staticInitialized) return;

                try
                {
                    var mapping = GetCpuSetIdPerLogicalProcessor();
                    foreach (var kvp in mapping)
                    {
                        _cpuSetIdPerLogicalProcessor[kvp.Key] = kvp.Value;
                    }
                    _staticInitialized = true;
                }
                catch (Exception)
                {
                    // If we can't get CPU Set mapping, CPU Sets won't be available
                    // The handler will still work but ApplyCpuSetMask will return false
                }
            }
        }

        public double GetAverageCpuUsage()
        {
            if (_queryLimitedInfoHandle == null || _queryLimitedInfoHandle.IsInvalid)
            {
                return -1;
            }

            try
            {
                DateTime now = DateTime.Now;

                // Remove datapoints older than 30 seconds from the moving average buffer
                while (_cpuTimeMovingAverageBuffer.Count > 0)
                {
                    TimeSpan datapointAge = now - _cpuTimeMovingAverageBuffer.Peek().Timestamp;
                    if (datapointAge.TotalSeconds > 30)
                    {
                        _cpuTimeMovingAverageBuffer.Dequeue();
                    }
                    else
                    {
                        break;
                    }
                }

                // Get the current total CPU time of the process
                bool success = CpuSetNativeMethods.GetProcessTimes(
                    _queryLimitedInfoHandle,
                    out _,
                    out _,
                    out FILETIME kernelTime,
                    out FILETIME userTime);

                if (!success)
                {
                    return -1;
                }

                TimeSpan totalCpuTime = TimeSpan.FromTicks((long)(kernelTime.ULong + userTime.ULong));
                _cpuTimeMovingAverageBuffer.Enqueue(new CpuTimeTimestamp
                {
                    Timestamp = now,
                    TotalCpuTime = totalCpuTime
                });

                // Need at least 2 samples to calculate usage
                if (_cpuTimeMovingAverageBuffer.Count < 2)
                {
                    return 0;
                }

                // Take the CPU time from now and (up to) a minute ago, and get the average usage %
                CpuTimeTimestamp startDatapoint = _cpuTimeMovingAverageBuffer.Peek();
                TimeSpan deltaTime = now - startDatapoint.Timestamp;
                TimeSpan deltaCpuTime = totalCpuTime - startDatapoint.TotalCpuTime;

                if (deltaCpuTime.Ticks == 0 || deltaTime.Ticks == 0)
                {
                    return 0;
                }

                return (double)deltaCpuTime.Ticks / deltaTime.Ticks / Environment.ProcessorCount;
            }
            catch
            {
                return -1;
            }
        }

        public bool ApplyCpuSetMask(long affinityMask, bool clearMask = false)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ProcessCpuSetHandler));
            }

            // Ensure we have CPU Set mapping
            if (_cpuSetIdPerLogicalProcessor.Count == 0)
            {
                _logger?.LogWarning("CPU Set mapping not available. Cannot apply CPU Sets to process {ProcessId}", _pid);
                return false;
            }

            // Open handle for setting process information if not already open
            if (_setLimitedInfoHandle == null)
            {
                _setLimitedInfoHandle = CpuSetNativeMethods.OpenProcess(
                    ProcessAccessFlags.PROCESS_SET_LIMITED_INFORMATION,
                    false,
                    _pid);

                if (_setLimitedInfoHandle == null || _setLimitedInfoHandle.IsInvalid)
                {
                    int openError = Marshal.GetLastWin32Error();
                    string extraHelpString = (openError == 5) ? " Try restarting as Administrator" : "";
                    _logger?.LogWarning("Could not open process '{ExecutableName}' (PID: {ProcessId}) for setting affinity: {Error}{Help}",
                        _executableName, _pid, new Win32Exception(openError).Message, extraHelpString);
                    return false;
                }
            }
            else if (_setLimitedInfoHandle.IsInvalid)
            {
                // The handle was already made previously and failed, don't bother trying again
                return false;
            }

            bool success;
            int error;

            if (clearMask)
            {
                // Clear the CPU Set (allow process to run on all cores)
                success = CpuSetNativeMethods.SetProcessDefaultCpuSets(_setLimitedInfoHandle, null, 0);
                if (success)
                {
                    _logger?.LogInformation("Cleared CPU Set of '{ExecutableName}' (PID: {ProcessId})", _executableName, _pid);
                    return true;
                }

                error = Marshal.GetLastWin32Error();
                _logger?.LogWarning("Could not clear CPU Set of '{ExecutableName}' (PID: {ProcessId}): {Error}",
                    _executableName, _pid, new Win32Exception(error).Message);
                return false;
            }

            // Convert affinity mask to CPU Set IDs
            List<uint> cpuSetIds = new List<uint>();
            int logicalCoreCount = Environment.ProcessorCount;

            for (int i = 0; i < logicalCoreCount; i++)
            {
                long coreBit = 1L << i;
                if ((affinityMask & coreBit) != 0)
                {
                    if (_cpuSetIdPerLogicalProcessor.TryGetValue(i, out uint cpuSetId))
                    {
                        cpuSetIds.Add(cpuSetId);
                    }
                    else
                    {
                        _logger?.LogWarning("Unable to include core {CoreIndex} in CPU Set for '{ExecutableName}'. It does not have a CPU Set ID",
                            i, _executableName);
                    }
                }
            }

            if (cpuSetIds.Count == 0)
            {
                _logger?.LogWarning("No valid CPU Set IDs found for affinity mask 0x{AffinityMask:X} on process '{ExecutableName}'",
                    affinityMask, _executableName);
                return false;
            }

            uint[] cpuSetIdsArray = cpuSetIds.ToArray();
            success = CpuSetNativeMethods.SetProcessDefaultCpuSets(_setLimitedInfoHandle, cpuSetIdsArray, (uint)cpuSetIdsArray.Length);

            if (success)
            {
                _logger?.LogInformation("Applied CPU Set (affinity mask 0x{AffinityMask:X}) to '{ExecutableName}' (PID: {ProcessId})",
                    affinityMask, _executableName, _pid);
                return true;
            }

            error = Marshal.GetLastWin32Error();
            string errorMessage = $"Could not apply CPU Set to '{_executableName}' (PID: {_pid}): {new Win32Exception(error).Message}";
            if (error == 5)
            {
                errorMessage += " (Likely due to anti-cheat or insufficient privileges)";
            }
            _logger?.LogWarning(errorMessage);
            return false;
        }

        /// <summary>
        /// Get the CPU Set Id of each logical processor
        /// </summary>
        private static Dictionary<int, uint> GetCpuSetIdPerLogicalProcessor()
        {
            uint bufferLength = 0;

            // First call to get buffer size
            if (!CpuSetNativeMethods.GetSystemCpuSetInformation(IntPtr.Zero, 0, ref bufferLength, new SafeProcessHandle(), 0))
            {
                int error = Marshal.GetLastWin32Error();
                if (error != 0x7A) // ERROR_INSUFFICIENT_BUFFER
                {
                    throw new Win32Exception(error, "Failed to query CPU Set information buffer size");
                }
            }

            Dictionary<int, uint> cpuSets = new Dictionary<int, uint>();
            IntPtr buffer = Marshal.AllocHGlobal((int)bufferLength);

            try
            {
                // Second call to get actual data
                if (!CpuSetNativeMethods.GetSystemCpuSetInformation(buffer, bufferLength, ref bufferLength, new SafeProcessHandle(), 0))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to get CPU Set information");
                }

                IntPtr current = buffer;
                IntPtr bufferEnd = buffer + (int)bufferLength;

                while (current.ToInt64() < bufferEnd.ToInt64())
                {
                    SYSTEM_CPU_SET_INFORMATION item = Marshal.PtrToStructure<SYSTEM_CPU_SET_INFORMATION>(current);

                    if (item.Type != CPU_SET_INFORMATION_TYPE.CpuSetInformation)
                    {
                        throw new InvalidCastException("Invalid CPU Set information type encountered");
                    }

                    cpuSets[item.LogicalProcessorIndex] = item.Id;

                    current = IntPtr.Add(current, (int)item.Size);
                }

                return cpuSets;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _queryLimitedInfoHandle?.Dispose();
            _setLimitedInfoHandle?.Dispose();
            _cpuTimeMovingAverageBuffer.Clear();

            _disposed = true;
            GC.SuppressFinalize(this);
        }

        private class CpuTimeTimestamp
        {
            public DateTime Timestamp { get; init; }
            public TimeSpan TotalCpuTime { get; init; }
        }
    }
}

