namespace ThreadPilot.Platforms.Windows
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Runtime.InteropServices;
    using Microsoft.Extensions.Logging;
    using Microsoft.Win32.SafeHandles;
    using ThreadPilot.Models;
    using ThreadPilot.Services;

    public class ProcessCpuSetHandler : IProcessCpuSetHandler
    {
        private static CpuSetMapping staticCpuSetMapping = CpuSetMapping.Empty;
        private static readonly object staticInitLock = new object();
        private static bool staticInitialized = false;

        private readonly Queue<CpuTimeTimestamp> cpuTimeMovingAverageBuffer = new();
        private readonly string executableName;
        private readonly uint pid;
        private readonly IProcessCpuSetNativeApi nativeApi;
        private readonly CpuSetMapping cpuSetMapping;
        private readonly ILogger? logger;

        private SafeProcessHandle? queryLimitedInfoHandle;
        private SafeProcessHandle? setLimitedInfoHandle;
        private bool disposed = false;

        public ProcessCpuSetHandler(uint processId, string executableName, ILogger? logger = null)
            : this(processId, executableName, ProcessCpuSetNativeApi.Instance, EnsureStaticInitialization(ProcessCpuSetNativeApi.Instance), logger)
        {
        }

        internal ProcessCpuSetHandler(
            uint processId,
            string executableName,
            IProcessCpuSetNativeApi nativeApi,
            CpuSetMapping cpuSetMapping,
            ILogger? logger = null)
        {
            this.pid = processId;
            this.executableName = executableName ?? $"PID_{processId}";
            this.nativeApi = nativeApi ?? throw new ArgumentNullException(nameof(nativeApi));
            this.cpuSetMapping = cpuSetMapping ?? throw new ArgumentNullException(nameof(cpuSetMapping));
            this.logger = logger;

            // Open handle for querying process information
            this.queryLimitedInfoHandle = this.nativeApi.OpenProcess(
                ProcessAccessFlags.PROCESS_QUERY_LIMITED_INFORMATION,
                false,
                processId);

            if (this.queryLimitedInfoHandle == null || this.queryLimitedInfoHandle.IsInvalid)
            {
                var error = this.nativeApi.GetLastWin32Error();
                this.logger?.LogWarning("Failed to open process {ProcessId} for querying: {Error}", processId, new Win32Exception(error).Message);
            }
        }

        public uint ProcessId => this.pid;

        public string ExecutableName => this.executableName;

        public bool IsValid => this.queryLimitedInfoHandle != null && !this.queryLimitedInfoHandle.IsInvalid;

        private static CpuSetMapping EnsureStaticInitialization(IProcessCpuSetNativeApi nativeApi)
        {
            if (staticInitialized)
            {
                return staticCpuSetMapping;
            }

            lock (staticInitLock)
            {
                if (staticInitialized)
                {
                    return staticCpuSetMapping;
                }

                try
                {
                    staticCpuSetMapping = GetCpuSetMapping(nativeApi);
                }
                catch (Exception)
                {
                    // If we can't get CPU Set mapping, CPU Sets won't be available
                    // The handler will still work but ApplyCpuSetMask will return false
                    staticCpuSetMapping = CpuSetMapping.Empty;
                }

                staticInitialized = true;
                return staticCpuSetMapping;
            }
        }

        public double GetAverageCpuUsage()
        {
            if (this.queryLimitedInfoHandle == null || this.queryLimitedInfoHandle.IsInvalid)
            {
                return -1;
            }

            try
            {
                DateTime now = DateTime.Now;

                // Remove datapoints older than 30 seconds from the moving average buffer
                while (this.cpuTimeMovingAverageBuffer.Count > 0)
                {
                    TimeSpan datapointAge = now - this.cpuTimeMovingAverageBuffer.Peek().Timestamp;
                    if (datapointAge.TotalSeconds > 30)
                    {
                        this.cpuTimeMovingAverageBuffer.Dequeue();
                    }
                    else
                    {
                        break;
                    }
                }

                // Get the current total CPU time of the process
                bool success = this.nativeApi.GetProcessTimes(
                    this.queryLimitedInfoHandle,
                    out _,
                    out _,
                    out FILETIME kernelTime,
                    out FILETIME userTime);

                if (!success)
                {
                    return -1;
                }

                TimeSpan totalCpuTime = TimeSpan.FromTicks((long)(kernelTime.ULong + userTime.ULong));
                this.cpuTimeMovingAverageBuffer.Enqueue(new CpuTimeTimestamp
                {
                    Timestamp = now,
                    TotalCpuTime = totalCpuTime,
                });

                // Need at least 2 samples to calculate usage
                if (this.cpuTimeMovingAverageBuffer.Count < 2)
                {
                    return 0;
                }

                // Take the CPU time from now and (up to) a minute ago, and get the average usage %
                CpuTimeTimestamp startDatapoint = this.cpuTimeMovingAverageBuffer.Peek();
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

        public bool ApplyCpuSetMask(long affinityMask, bool clearMask = false) =>
            this.ApplyCpuSetMaskDetailed(affinityMask, clearMask).Success;

        public CpuSetApplyResult ApplyCpuSetMaskDetailed(long affinityMask, bool clearMask = false)
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException(nameof(ProcessCpuSetHandler));
            }

            // Legacy mask support is intentionally limited to single-group systems where
            // logical processors 0-63 map to processor group 0. CpuSelection will replace
            // this path for group-aware selections in a later phase.
            if (this.cpuSetMapping.IsEmpty)
            {
                this.logger?.LogWarning("CPU Set mapping not available. Cannot apply CPU Sets to process {ProcessId}", this.pid);
                return CpuSetApplyResult.Failed(
                    AffinityApplyErrorCodes.CpuSetsUnavailable,
                    ProcessOperationUserMessages.CpuSetsUnavailable,
                    $"CPU Set mapping is not available for process '{this.executableName}' (PID: {this.pid}).");
            }

            var handleResult = this.EnsureSetHandleDetailed();
            if (!handleResult.Success)
            {
                return handleResult;
            }

            if (clearMask)
            {
                return this.ApplyCpuSetIdsDetailed(null, 0, "clear CPU Set");
            }

            var cpuSetIds = this.cpuSetMapping.ResolveLegacyAffinityMask(affinityMask, Environment.ProcessorCount);

            if (cpuSetIds.Count == 0)
            {
                this.logger?.LogWarning(
                    "No valid CPU Set IDs found for affinity mask 0x{AffinityMask:X} on process '{ExecutableName}'",
                    affinityMask, this.executableName);
                return CpuSetApplyResult.Failed(
                    AffinityApplyErrorCodes.InvalidTopology,
                    ProcessOperationUserMessages.InvalidTopology,
                    $"No valid CPU Set IDs found for affinity mask 0x{affinityMask:X} on process '{this.executableName}'.");
            }

            var cpuSetIdsArray = cpuSetIds.ToArray();
            var result = this.ApplyCpuSetIdsDetailed(cpuSetIdsArray, (uint)cpuSetIdsArray.Length, "apply CPU Set");

            if (result.Success)
            {
                this.logger?.LogInformation(
                    "Applied CPU Set (affinity mask 0x{AffinityMask:X}) to '{ExecutableName}' (PID: {ProcessId})",
                    affinityMask, this.executableName, this.pid);
            }

            return result;
        }

        public bool ApplyCpuSelection(CpuSelection? selection, bool clearSelection = false) =>
            this.ApplyCpuSelectionDetailed(selection, clearSelection).Success;

        public CpuSetApplyResult ApplyCpuSelectionDetailed(CpuSelection? selection, bool clearSelection = false)
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException(nameof(ProcessCpuSetHandler));
            }

            var handleResult = this.EnsureSetHandleDetailed();
            if (!handleResult.Success)
            {
                return handleResult;
            }

            if (clearSelection)
            {
                return this.ApplyCpuSetIdsDetailed(null, 0, "clear CPU Set selection");
            }

            ArgumentNullException.ThrowIfNull(selection);

            var cpuSetIds = this.cpuSetMapping.ResolveCpuSetIds(selection);
            if (cpuSetIds.Count == 0)
            {
                this.logger?.LogWarning(
                    "No valid CPU Set IDs resolved for CPU selection on process '{ExecutableName}' (PID: {ProcessId})",
                    this.executableName,
                    this.pid);
                return CpuSetApplyResult.Failed(
                    AffinityApplyErrorCodes.InvalidTopology,
                    ProcessOperationUserMessages.InvalidTopology,
                    $"No valid CPU Set IDs resolved for CPU selection on process '{this.executableName}' (PID: {this.pid}).");
            }

            var cpuSetIdsArray = cpuSetIds.ToArray();
            return this.ApplyCpuSetIdsDetailed(cpuSetIdsArray, (uint)cpuSetIdsArray.Length, "apply CPU Set selection");
        }

        private bool EnsureSetHandle()
        {
            return this.EnsureSetHandleDetailed().Success;
        }

        private CpuSetApplyResult EnsureSetHandleDetailed()
        {
            if (this.setLimitedInfoHandle == null)
            {
                this.setLimitedInfoHandle = this.nativeApi.OpenProcess(
                    ProcessAccessFlags.PROCESS_SET_LIMITED_INFORMATION,
                    false,
                    this.pid);

                if (this.setLimitedInfoHandle == null || this.setLimitedInfoHandle.IsInvalid)
                {
                    int openError = this.nativeApi.GetLastWin32Error();
                    string extraHelpString = (openError == 5)
                        ? $" {ProcessOperationUserMessages.AdminClarification}"
                        : string.Empty;
                    this.logger?.LogWarning(
                        "Could not open process '{ExecutableName}' (PID: {ProcessId}) for setting affinity: {Error}{Help}",
                        this.executableName, this.pid, new Win32Exception(openError).Message, extraHelpString);
                    return this.CreateNativeFailureResult(
                        "open process for CPU Set changes",
                        openError);
                }
            }
            else if (this.setLimitedInfoHandle.IsInvalid)
            {
                // The handle was already made previously and failed, don't bother trying again
                return CpuSetApplyResult.Failed(
                    AffinityApplyErrorCodes.CpuSetsUnavailable,
                    ProcessOperationUserMessages.CpuSetsUnavailable,
                    $"The cached CPU Set handle for '{this.executableName}' (PID: {this.pid}) is invalid.");
            }

            return CpuSetApplyResult.Succeeded($"CPU Set handle is available for '{this.executableName}' (PID: {this.pid}).");
        }

        private bool ApplyCpuSetIds(uint[]? cpuSetIds, uint cpuSetIdCount, string operationName)
        {
            return this.ApplyCpuSetIdsDetailed(cpuSetIds, cpuSetIdCount, operationName).Success;
        }

        private CpuSetApplyResult ApplyCpuSetIdsDetailed(uint[]? cpuSetIds, uint cpuSetIdCount, string operationName)
        {
            bool success = this.nativeApi.SetProcessDefaultCpuSets(this.setLimitedInfoHandle!, cpuSetIds, cpuSetIdCount);
            if (success)
            {
                this.logger?.LogInformation(
                    "Completed {OperationName} for '{ExecutableName}' (PID: {ProcessId})",
                    operationName,
                    this.executableName,
                    this.pid);
                return CpuSetApplyResult.Succeeded(
                    $"Completed {operationName} for '{this.executableName}' (PID: {this.pid}).");
            }

            int error = this.nativeApi.GetLastWin32Error();
            string errorMessage = $"Could not {operationName} for '{this.executableName}' (PID: {this.pid}): {new Win32Exception(error).Message}";
            if (error == 5)
            {
                errorMessage += $" {ProcessOperationUserMessages.AdminClarification}";
            }

            this.logger?.LogWarning(errorMessage);
            return this.CreateNativeFailureResult(operationName, error, errorMessage);
        }

        private CpuSetApplyResult CreateNativeFailureResult(
            string operationName,
            int win32ErrorCode,
            string? technicalMessage = null)
        {
            var message = technicalMessage ??
                $"Could not {operationName} for '{this.executableName}' (PID: {this.pid}): {new Win32Exception(win32ErrorCode).Message}";
            var accessDenied = win32ErrorCode == 5;

            return CpuSetApplyResult.Failed(
                accessDenied ? AffinityApplyErrorCodes.AccessDenied : AffinityApplyErrorCodes.NativeApplyFailed,
                accessDenied ? ProcessOperationUserMessages.AccessDenied : ProcessOperationUserMessages.CpuSetsUnavailable,
                message,
                win32ErrorCode,
                isAccessDenied: accessDenied);
        }

        private static CpuSetMapping GetCpuSetMapping(IProcessCpuSetNativeApi nativeApi)
        {
            uint bufferLength = 0;

            // First call to get buffer size
            if (!nativeApi.GetSystemCpuSetInformation(IntPtr.Zero, 0, ref bufferLength, new SafeProcessHandle(), 0))
            {
                int error = nativeApi.GetLastWin32Error();
                if (error != 0x7A) // ERROR_INSUFFICIENT_BUFFER
                {
                    throw new Win32Exception(error, "Failed to query CPU Set information buffer size");
                }
            }

            Dictionary<ProcessorRef, uint> cpuSets = new Dictionary<ProcessorRef, uint>();
            IntPtr buffer = Marshal.AllocHGlobal((int)bufferLength);

            try
            {
                // Second call to get actual data
                if (!nativeApi.GetSystemCpuSetInformation(buffer, bufferLength, ref bufferLength, new SafeProcessHandle(), 0))
                {
                    throw new Win32Exception(nativeApi.GetLastWin32Error(), "Failed to get CPU Set information");
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

                    var processor = CpuSetMapping.CreateProcessorRef(item.Group, item.LogicalProcessorIndex);
                    cpuSets[processor] = item.Id;

                    current = IntPtr.Add(current, (int)item.Size);
                }

                return CpuSetMapping.Create(cpuSets);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.queryLimitedInfoHandle?.Dispose();
            this.setLimitedInfoHandle?.Dispose();
            this.cpuTimeMovingAverageBuffer.Clear();

            this.disposed = true;
            GC.SuppressFinalize(this);
        }

        private class CpuTimeTimestamp
        {
            public DateTime Timestamp { get; init; }

            public TimeSpan TotalCpuTime { get; init; }
        }
    }
}
