/*
 * ThreadPilot - process memory priority service.
 */
namespace ThreadPilot.Services
{
    using System.ComponentModel;
    using System.Runtime.InteropServices;
    using Microsoft.Extensions.Logging;
    using ThreadPilot.Models;
    using ThreadPilot.Platforms.Windows;

    public sealed class ProcessMemoryPriorityService : IProcessMemoryPriorityService
    {
        public const string UnsupportedUserMessage =
            "Memory priority is not supported on this Windows version or process.";

        private const string InvalidProcessErrorCode = "InvalidProcess";
        private const string UnsupportedErrorCode = "Unsupported";
        private const string InvalidPriorityErrorCode = "InvalidMemoryPriority";

        private static readonly uint MemoryPriorityInformationSize =
            (uint)Marshal.SizeOf<MemoryPriorityInformation>();

        private readonly IProcessMemoryPriorityNativeApi nativeApi;
        private readonly ILogger<ProcessMemoryPriorityService> logger;

        public ProcessMemoryPriorityService(
            IProcessMemoryPriorityNativeApi nativeApi,
            ILogger<ProcessMemoryPriorityService> logger)
        {
            this.nativeApi = nativeApi ?? throw new ArgumentNullException(nameof(nativeApi));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<ProcessMemoryPriority?> GetMemoryPriorityAsync(ProcessModel process)
        {
            if (!this.nativeApi.IsSupported || !IsValidProcess(process))
            {
                return Task.FromResult<ProcessMemoryPriority?>(null);
            }

            try
            {
                using var handle = this.nativeApi.OpenProcess(
                    ProcessAccessFlags.PROCESS_QUERY_LIMITED_INFORMATION,
                    inheritHandle: false,
                    (uint)process.ProcessId);

                if (handle.IsInvalid)
                {
                    this.logger.LogDebug(
                        "OpenProcess failed while reading memory priority for process {ProcessName} (PID: {ProcessId}): {Error}",
                        process.Name,
                        process.ProcessId,
                        this.nativeApi.GetLastWin32Error());
                    return Task.FromResult<ProcessMemoryPriority?>(null);
                }

                var information = default(MemoryPriorityInformation);
                if (!this.nativeApi.GetProcessInformation(
                    handle,
                    ProcessInformationClass.ProcessMemoryPriority,
                    ref information,
                    MemoryPriorityInformationSize))
                {
                    this.logger.LogDebug(
                        "GetProcessInformation(ProcessMemoryPriority) failed for process {ProcessName} (PID: {ProcessId}): {Error}",
                        process.Name,
                        process.ProcessId,
                        this.nativeApi.GetLastWin32Error());
                    return Task.FromResult<ProcessMemoryPriority?>(null);
                }

                return Task.FromResult(FromWindowsMemoryPriority(information.MemoryPriority));
            }
            catch (Exception ex) when (IsUnsupported(ex) || AffinityApplyExceptionClassifier.IsAccessDenied(ex) || AffinityApplyExceptionClassifier.IsProcessExited(ex))
            {
                this.logger.LogDebug(
                    ex,
                    "Could not read memory priority for process {ProcessName} (PID: {ProcessId})",
                    process.Name,
                    process.ProcessId);
                return Task.FromResult<ProcessMemoryPriority?>(null);
            }
        }

        public Task<ProcessOperationResult> SetMemoryPriorityAsync(ProcessModel process, ProcessMemoryPriority priority)
        {
            if (!IsValidProcess(process))
            {
                return Task.FromResult(ProcessOperationResult.Failed(
                    InvalidProcessErrorCode,
                    UnsupportedUserMessage,
                    "Process is null or has an invalid PID."));
            }

            if (!IsDefinedPriority(priority))
            {
                return Task.FromResult(ProcessOperationResult.Failed(
                    InvalidPriorityErrorCode,
                    UnsupportedUserMessage,
                    $"Memory priority value '{priority}' is not supported."));
            }

            if (!this.nativeApi.IsSupported)
            {
                return Task.FromResult(Unsupported("The Windows process memory priority APIs are unavailable."));
            }

            try
            {
                using var handle = this.nativeApi.OpenProcess(
                    ProcessAccessFlags.PROCESS_SET_INFORMATION,
                    inheritHandle: false,
                    (uint)process.ProcessId);

                if (handle.IsInvalid)
                {
                    return Task.FromResult(this.FromLastError(
                        "OpenProcess failed before SetProcessInformation(ProcessMemoryPriority)."));
                }

                var information = new MemoryPriorityInformation
                {
                    MemoryPriority = ToWindowsMemoryPriority(priority),
                };

                if (!this.nativeApi.SetProcessInformation(
                    handle,
                    ProcessInformationClass.ProcessMemoryPriority,
                    ref information,
                    MemoryPriorityInformationSize))
                {
                    return Task.FromResult(this.FromLastError(
                        "SetProcessInformation(ProcessMemoryPriority) failed."));
                }

                return Task.FromResult(ProcessOperationResult.Succeeded(
                    "Memory priority applied.",
                    $"Process {process.Name} (PID: {process.ProcessId}) memory priority set to {priority}."));
            }
            catch (Exception ex) when (IsUnsupported(ex))
            {
                return Task.FromResult(Unsupported(ex.Message));
            }
            catch (Exception ex) when (AffinityApplyExceptionClassifier.IsProcessExited(ex))
            {
                return Task.FromResult(ProcessOperationResult.Failed(
                    AffinityApplyErrorCodes.ProcessExited,
                    ProcessOperationUserMessages.ProcessExited,
                    ex.Message,
                    isProcessExited: true));
            }
            catch (Exception ex) when (AffinityApplyExceptionClassifier.IsAccessDenied(ex))
            {
                var antiCheatLikely = AffinityApplyExceptionClassifier.IsAntiCheatLikely(ex);
                return Task.FromResult(ProcessOperationResult.Failed(
                    antiCheatLikely
                        ? AffinityApplyErrorCodes.AntiCheatOrProtectedProcessLikely
                        : AffinityApplyErrorCodes.AccessDenied,
                    antiCheatLikely
                        ? ProcessOperationUserMessages.AntiCheatProtectedLikely
                        : ProcessOperationUserMessages.AccessDenied,
                    ex.Message,
                    isAccessDenied: true,
                    isAntiCheatLikely: antiCheatLikely));
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(
                    ex,
                    "Memory priority apply failed for process {ProcessName} (PID: {ProcessId})",
                    process.Name,
                    process.ProcessId);

                return Task.FromResult(ProcessOperationResult.Failed(
                    AffinityApplyErrorCodes.NativeApplyFailed,
                    "ThreadPilot could not apply the memory priority change.",
                    ex.Message));
            }
        }

        private static bool IsValidProcess(ProcessModel? process) =>
            process != null && process.ProcessId > 0;

        private static bool IsDefinedPriority(ProcessMemoryPriority priority) =>
            priority is ProcessMemoryPriority.VeryLow or
                ProcessMemoryPriority.Low or
                ProcessMemoryPriority.Medium or
                ProcessMemoryPriority.BelowNormal or
                ProcessMemoryPriority.Normal;

        private static uint ToWindowsMemoryPriority(ProcessMemoryPriority priority) =>
            IsDefinedPriority(priority)
                ? (uint)priority
                : throw new ArgumentOutOfRangeException(nameof(priority), priority, "Unsupported memory priority value.");

        private static ProcessMemoryPriority? FromWindowsMemoryPriority(uint priority) =>
            priority is >= (uint)ProcessMemoryPriority.VeryLow and <= (uint)ProcessMemoryPriority.Normal
                ? (ProcessMemoryPriority)priority
                : null;

        private static bool IsUnsupported(Exception ex) =>
            ex is EntryPointNotFoundException ||
            ex is DllNotFoundException ||
            (ex is Win32Exception win32Exception && win32Exception.NativeErrorCode == 50);

        private static ProcessOperationResult Unsupported(string technicalMessage) =>
            ProcessOperationResult.Failed(
                UnsupportedErrorCode,
                UnsupportedUserMessage,
                technicalMessage);

        private ProcessOperationResult FromLastError(string context)
        {
            var error = this.nativeApi.GetLastWin32Error();
            var technicalMessage = $"{context} Win32 error {error}.";

            return error switch
            {
                5 => ProcessOperationResult.Failed(
                    AffinityApplyErrorCodes.AccessDenied,
                    ProcessOperationUserMessages.AccessDenied,
                    technicalMessage,
                    isAccessDenied: true),
                50 => Unsupported(technicalMessage),
                87 => ProcessOperationResult.Failed(
                    AffinityApplyErrorCodes.ProcessExited,
                    ProcessOperationUserMessages.ProcessExited,
                    technicalMessage,
                    isProcessExited: true),
                _ => ProcessOperationResult.Failed(
                    AffinityApplyErrorCodes.NativeApplyFailed,
                    "ThreadPilot could not apply the memory priority change.",
                    technicalMessage),
            };
        }
    }
}
