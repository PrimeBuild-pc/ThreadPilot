/*
 * ThreadPilot - Advanced Windows Process and Power Plan Manager
 * Copyright (C) 2025 Prime Build
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, version 3 only.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
namespace ThreadPilot.Services
{
    using System.ComponentModel;
    using Microsoft.Extensions.Logging;
    using ThreadPilot.Models;
    using ThreadPilot.Platforms.Windows;

    public enum AffinityApplyFailureReason
    {
        None,
        InvalidMask,
        ProcessTerminated,
        AccessDenied,
        VerificationMismatch,
        ApplyFailed,
    }

    public static class AffinityApplyErrorCodes
    {
        public const string None = "None";
        public const string AccessDenied = "AccessDenied";
        public const string AntiCheatOrProtectedProcessLikely = "AntiCheatOrProtectedProcessLikely";
        public const string ProcessExited = "ProcessExited";
        public const string InvalidSelection = "InvalidSelection";
        public const string InvalidTopology = "InvalidTopology";
        public const string CpuSetsUnavailable = "CpuSetsUnavailable";
        public const string LegacyFallbackUnsafe = "LegacyFallbackUnsafe";
        public const string NativeApplyFailed = "NativeApplyFailed";
        public const string UnknownError = "UnknownError";
    }

    public sealed record AffinityApplyResult
    {
        public bool Success { get; init; }

        public long RequestedMask { get; init; }

        public long VerifiedMask { get; init; }

        public AffinityApplyFailureReason FailureReason { get; init; }

        public string Message => string.IsNullOrWhiteSpace(this.UserMessage) ? this.TechnicalMessage : this.UserMessage;

        public string ErrorCode { get; init; } = AffinityApplyErrorCodes.None;

        public string UserMessage { get; init; } = string.Empty;

        public string TechnicalMessage { get; init; } = string.Empty;

        public bool IsAccessDenied { get; init; }

        public bool IsAntiCheatLikely { get; init; }

        public bool IsInvalidTopology { get; init; }

        public bool IsLegacyFallbackBlocked { get; init; }

        public bool UsedCpuSets { get; init; }

        public bool UsedLegacyAffinity { get; init; }

        public static AffinityApplyResult Succeeded(long requestedMask, long verifiedMask) =>
            new()
            {
                Success = true,
                RequestedMask = requestedMask,
                VerifiedMask = verifiedMask,
                FailureReason = AffinityApplyFailureReason.None,
                ErrorCode = AffinityApplyErrorCodes.None,
                UserMessage = "Affinity applied successfully.",
                TechnicalMessage = $"Affinity 0x{requestedMask:X} applied and verified as 0x{verifiedMask:X}.",
            };

        public static AffinityApplyResult SucceededWithCpuSets(string technicalMessage) =>
            new()
            {
                Success = true,
                FailureReason = AffinityApplyFailureReason.None,
                ErrorCode = AffinityApplyErrorCodes.None,
                UserMessage = "Affinity applied successfully.",
                TechnicalMessage = technicalMessage,
                UsedCpuSets = true,
            };

        public static AffinityApplyResult SucceededWithLegacyFallback(long requestedMask, long verifiedMask) =>
            Succeeded(requestedMask, verifiedMask) with
            {
                UsedLegacyAffinity = true,
                TechnicalMessage = $"CPU Sets failed; legacy affinity 0x{requestedMask:X} applied and verified as 0x{verifiedMask:X}.",
            };

        public static AffinityApplyResult Failed(
            long requestedMask,
            long verifiedMask,
            AffinityApplyFailureReason failureReason,
            string message) =>
            new()
            {
                Success = false,
                RequestedMask = requestedMask,
                VerifiedMask = verifiedMask,
                FailureReason = failureReason,
                ErrorCode = MapFailureReason(failureReason),
                UserMessage = message,
                TechnicalMessage = message,
                IsAccessDenied = failureReason == AffinityApplyFailureReason.AccessDenied,
            };

        public static AffinityApplyResult Failed(
            string errorCode,
            string userMessage,
            string technicalMessage,
            bool isAccessDenied = false,
            bool isAntiCheatLikely = false,
            bool isInvalidTopology = false,
            bool isLegacyFallbackBlocked = false,
            long requestedMask = 0,
            long verifiedMask = 0,
            AffinityApplyFailureReason failureReason = AffinityApplyFailureReason.ApplyFailed) =>
            new()
            {
                Success = false,
                RequestedMask = requestedMask,
                VerifiedMask = verifiedMask,
                FailureReason = failureReason,
                ErrorCode = errorCode,
                UserMessage = userMessage,
                TechnicalMessage = technicalMessage,
                IsAccessDenied = isAccessDenied,
                IsAntiCheatLikely = isAntiCheatLikely,
                IsInvalidTopology = isInvalidTopology || errorCode == AffinityApplyErrorCodes.InvalidTopology,
                IsLegacyFallbackBlocked = isLegacyFallbackBlocked || errorCode == AffinityApplyErrorCodes.LegacyFallbackUnsafe,
            };

        private static string MapFailureReason(AffinityApplyFailureReason failureReason) =>
            failureReason switch
            {
                AffinityApplyFailureReason.None => AffinityApplyErrorCodes.None,
                AffinityApplyFailureReason.InvalidMask => AffinityApplyErrorCodes.InvalidSelection,
                AffinityApplyFailureReason.ProcessTerminated => AffinityApplyErrorCodes.ProcessExited,
                AffinityApplyFailureReason.AccessDenied => AffinityApplyErrorCodes.AccessDenied,
                AffinityApplyFailureReason.VerificationMismatch => AffinityApplyErrorCodes.NativeApplyFailed,
                AffinityApplyFailureReason.ApplyFailed => AffinityApplyErrorCodes.NativeApplyFailed,
                _ => AffinityApplyErrorCodes.UnknownError,
            };
    }

    public interface IAffinityApplyService
    {
        Task<AffinityApplyResult> ApplyAsync(ProcessModel process, long requestedMask);

        Task<AffinityApplyResult> ApplyAsync(ProcessModel process, CpuSelection selection);
    }

    internal sealed class CpuSelectionAffinityApplier
    {
        internal const string AccessDeniedUserMessage =
            ProcessOperationUserMessages.AccessDenied;

        internal const string AntiCheatUserMessage =
            ProcessOperationUserMessages.AntiCheatProtectedLikely;

        internal const string LegacyFallbackBlockedUserMessage =
            ProcessOperationUserMessages.LegacyFallbackBlocked;

        internal const string InvalidSelectionUserMessage =
            ProcessOperationUserMessages.InvalidTopology;

        private readonly Func<ProcessModel, IProcessCpuSetHandler> cpuSetHandlerFactory;
        private readonly Func<ProcessModel, long, Task<long>> legacyAffinityApplier;
        private readonly ILogger logger;
        private readonly Action<ProcessModel>? cpuSetFailureCallback;
        private readonly Action<ProcessModel, bool>? auditCallback;

        public CpuSelectionAffinityApplier(
            Func<ProcessModel, IProcessCpuSetHandler> cpuSetHandlerFactory,
            Func<ProcessModel, long, Task<long>> legacyAffinityApplier,
            ILogger logger,
            Action<ProcessModel>? cpuSetFailureCallback = null,
            Action<ProcessModel, bool>? auditCallback = null)
        {
            this.cpuSetHandlerFactory = cpuSetHandlerFactory ?? throw new ArgumentNullException(nameof(cpuSetHandlerFactory));
            this.legacyAffinityApplier = legacyAffinityApplier ?? throw new ArgumentNullException(nameof(legacyAffinityApplier));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.cpuSetFailureCallback = cpuSetFailureCallback;
            this.auditCallback = auditCallback;
        }

        public async Task<AffinityApplyResult> ApplyAsync(ProcessModel process, CpuSelection selection)
        {
            if (process == null || process.ProcessId <= 0)
            {
                return ProcessExited("Process is no longer running.", process);
            }

            if (selection == null || (selection.CpuSetIds.Count == 0 && selection.LogicalProcessors.Count == 0))
            {
                this.Audit(process, success: false);
                return AffinityApplyResult.Failed(
                    AffinityApplyErrorCodes.InvalidSelection,
                    InvalidSelectionUserMessage,
                    "CpuSelection contains neither CPU Set IDs nor logical processors.",
                    isInvalidTopology: true,
                    failureReason: AffinityApplyFailureReason.InvalidMask);
            }

            var cpuSetsResult = this.TryApplyCpuSets(process, selection);
            if (cpuSetsResult != null)
            {
                return cpuSetsResult;
            }

            this.cpuSetFailureCallback?.Invoke(process);

            var legacyMask = CpuSelection.ToLegacyAffinityMaskOrNull(selection);
            if (!legacyMask.HasValue || legacyMask.Value <= 0)
            {
                this.Audit(process, success: false);
                return AffinityApplyResult.Failed(
                    AffinityApplyErrorCodes.LegacyFallbackUnsafe,
                    LegacyFallbackBlockedUserMessage,
                    "CpuSelection cannot be represented as a non-zero single-group legacy affinity mask.",
                    isLegacyFallbackBlocked: true);
            }

            try
            {
                var verifiedMask = await this.legacyAffinityApplier(process, legacyMask.Value).ConfigureAwait(false);
                return AffinityApplyResult.SucceededWithLegacyFallback(legacyMask.Value, verifiedMask);
            }
            catch (Exception ex) when (AffinityApplyExceptionClassifier.IsAccessDenied(ex))
            {
                return AccessDenied(ex, legacyMask.Value, process.ProcessorAffinity);
            }
            catch (Exception ex) when (AffinityApplyExceptionClassifier.IsProcessExited(ex))
            {
                return ProcessExited("Process exited before legacy affinity fallback could be applied.", process, legacyMask.Value);
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(
                    ex,
                    "Legacy affinity fallback failed for process {ProcessName} (PID: {ProcessId})",
                    process.Name,
                    process.ProcessId);

                return AffinityApplyResult.Failed(
                    AffinityApplyErrorCodes.NativeApplyFailed,
                    "ThreadPilot could not apply this CPU selection.",
                    ex.Message,
                    requestedMask: legacyMask.Value,
                    verifiedMask: process.ProcessorAffinity);
            }
        }

        private AffinityApplyResult? TryApplyCpuSets(ProcessModel process, CpuSelection selection)
        {
            try
            {
                var handler = this.cpuSetHandlerFactory(process);
                if (!handler.IsValid)
                {
                    this.logger.LogDebug(
                        "CPU Set handler is invalid for process {ProcessName} (PID: {ProcessId})",
                        process.Name,
                        process.ProcessId);
                    return null;
                }

                var result = handler.ApplyCpuSelectionDetailed(selection);
                if (result.Success)
                {
                    this.Audit(process, success: true);
                    return AffinityApplyResult.SucceededWithCpuSets(
                        string.IsNullOrWhiteSpace(result.TechnicalMessage)
                            ? $"CPU Sets applied to process {process.Name} (PID: {process.ProcessId})."
                            : result.TechnicalMessage);
                }

                if (result.IsAccessDenied || result.ErrorCode == AffinityApplyErrorCodes.AccessDenied)
                {
                    this.Audit(process, success: false);
                    return AffinityApplyResult.Failed(
                        result.IsAntiCheatLikely
                            ? AffinityApplyErrorCodes.AntiCheatOrProtectedProcessLikely
                            : AffinityApplyErrorCodes.AccessDenied,
                        result.IsAntiCheatLikely ? AntiCheatUserMessage : AccessDeniedUserMessage,
                        result.TechnicalMessage,
                        isAccessDenied: true,
                        isAntiCheatLikely: result.IsAntiCheatLikely,
                        verifiedMask: process.ProcessorAffinity,
                        failureReason: AffinityApplyFailureReason.AccessDenied);
                }

                if (result.ErrorCode == AffinityApplyErrorCodes.InvalidTopology)
                {
                    this.Audit(process, success: false);
                    return AffinityApplyResult.Failed(
                        AffinityApplyErrorCodes.InvalidTopology,
                        ProcessOperationUserMessages.InvalidTopology,
                        result.TechnicalMessage,
                        isInvalidTopology: true,
                        verifiedMask: process.ProcessorAffinity,
                        failureReason: AffinityApplyFailureReason.InvalidMask);
                }

                this.logger.LogDebug(
                    "CPU Sets unavailable for process {ProcessName} (PID: {ProcessId}): {Message}",
                    process.Name,
                    process.ProcessId,
                    result.TechnicalMessage);
                return null;
            }
            catch (Exception ex) when (AffinityApplyExceptionClassifier.IsAccessDenied(ex))
            {
                this.Audit(process, success: false);
                return AccessDenied(ex, 0, process.ProcessorAffinity);
            }
            catch (Exception ex) when (AffinityApplyExceptionClassifier.IsProcessExited(ex))
            {
                this.Audit(process, success: false);
                return ProcessExited("Process exited before CPU Sets could be applied.", process);
            }
            catch (Exception ex)
            {
                this.logger.LogDebug(
                    ex,
                    "CPU Sets failed for process {ProcessName} (PID: {ProcessId}); evaluating legacy fallback",
                    process.Name,
                    process.ProcessId);
                return null;
            }
        }

        private void Audit(ProcessModel process, bool success) =>
            this.auditCallback?.Invoke(process, success);

        private static AffinityApplyResult AccessDenied(Exception ex, long requestedMask, long verifiedMask)
        {
            var antiCheatLikely = AffinityApplyExceptionClassifier.IsAntiCheatLikely(ex);
            return AffinityApplyResult.Failed(
                antiCheatLikely
                    ? AffinityApplyErrorCodes.AntiCheatOrProtectedProcessLikely
                    : AffinityApplyErrorCodes.AccessDenied,
                antiCheatLikely ? AntiCheatUserMessage : AccessDeniedUserMessage,
                ex.Message,
                isAccessDenied: true,
                isAntiCheatLikely: antiCheatLikely,
                requestedMask: requestedMask,
                verifiedMask: verifiedMask,
                failureReason: AffinityApplyFailureReason.AccessDenied);
        }

        private static AffinityApplyResult ProcessExited(string userMessage, ProcessModel? process, long requestedMask = 0) =>
            AffinityApplyResult.Failed(
                AffinityApplyErrorCodes.ProcessExited,
                ProcessOperationUserMessages.ProcessExited,
                userMessage,
                requestedMask: requestedMask,
                verifiedMask: process?.ProcessorAffinity ?? 0,
                failureReason: AffinityApplyFailureReason.ProcessTerminated);
    }

    internal static class AffinityApplyExceptionClassifier
    {
        public static bool IsAccessDenied(Exception ex) =>
            ex is UnauthorizedAccessException ||
            ex is Win32Exception { NativeErrorCode: 5 } ||
            IsInnerAccessDenied(ex.InnerException) ||
            ContainsAny(
                ex.Message,
                "access denied",
                "anti-cheat",
                "anti cheat",
                "protected",
                "insufficient privileges");

        public static bool IsAntiCheatLikely(Exception ex) =>
            ContainsAny(ex.Message, "anti-cheat", "anti cheat", "protected") ||
            (ex.InnerException != null && IsAntiCheatLikely(ex.InnerException));

        public static bool IsProcessExited(Exception ex)
        {
            if (ex is ArgumentException)
            {
                return true;
            }

            var message = ex.Message ?? string.Empty;
            if (ex is InvalidOperationException &&
                ContainsAny(message, "exit", "exited", "terminated", "not running", "has no process associated"))
            {
                return true;
            }

            return ex.InnerException != null && IsProcessExited(ex.InnerException);
        }

        private static bool IsInnerAccessDenied(Exception? ex) => ex != null && IsAccessDenied(ex);

        private static bool ContainsAny(string? value, params string[] needles)
        {
            var source = value ?? string.Empty;
            return needles.Any(needle => source.Contains(needle, StringComparison.OrdinalIgnoreCase));
        }
    }

    public sealed class AffinityApplyService : IAffinityApplyService
    {
        private readonly IProcessService processService;
        private readonly ICpuTopologyService cpuTopologyService;
        private readonly ILogger<AffinityApplyService> logger;

        public AffinityApplyService(
            IProcessService processService,
            ICpuTopologyService cpuTopologyService,
            ILogger<AffinityApplyService> logger)
        {
            this.processService = processService ?? throw new ArgumentNullException(nameof(processService));
            this.cpuTopologyService = cpuTopologyService ?? throw new ArgumentNullException(nameof(cpuTopologyService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<AffinityApplyResult> ApplyAsync(ProcessModel process, CpuSelection selection) =>
            process == null
                ? Task.FromResult(AffinityApplyResult.Failed(
                    AffinityApplyErrorCodes.ProcessExited,
                    ProcessOperationUserMessages.ProcessExited,
                    "ProcessModel is null.",
                    failureReason: AffinityApplyFailureReason.ProcessTerminated))
                : selection == null
                    ? Task.FromResult(AffinityApplyResult.Failed(
                        AffinityApplyErrorCodes.InvalidSelection,
                        CpuSelectionAffinityApplier.InvalidSelectionUserMessage,
                        "CpuSelection is null.",
                        isInvalidTopology: true,
                        failureReason: AffinityApplyFailureReason.InvalidMask))
                    : this.processService.SetProcessorAffinity(process, selection);

        public async Task<AffinityApplyResult> ApplyAsync(ProcessModel process, long requestedMask)
        {
            ArgumentNullException.ThrowIfNull(process);

            var startingMask = process.ProcessorAffinity;

            if (requestedMask == 0)
            {
                return AffinityApplyResult.Failed(
                    requestedMask,
                    startingMask,
                    AffinityApplyFailureReason.InvalidMask,
                    ProcessOperationUserMessages.InvalidTopology);
            }

            if (!this.cpuTopologyService.IsAffinityMaskValid(requestedMask))
            {
                return AffinityApplyResult.Failed(
                    AffinityApplyErrorCodes.InvalidTopology,
                    ProcessOperationUserMessages.InvalidTopology,
                    $"Affinity mask 0x{requestedMask:X} is not valid for this CPU topology.",
                    isInvalidTopology: true,
                    requestedMask: requestedMask,
                    verifiedMask: startingMask,
                    failureReason: AffinityApplyFailureReason.InvalidMask);
            }

            if (!await this.IsProcessRunningAsync(process).ConfigureAwait(false))
            {
                return AffinityApplyResult.Failed(
                    requestedMask,
                    startingMask,
                    AffinityApplyFailureReason.ProcessTerminated,
                    ProcessOperationUserMessages.ProcessExited);
            }

            try
            {
                await this.processService.SetProcessorAffinity(process, requestedMask).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsAccessDenied(ex))
            {
                this.logger.LogWarning(
                    ex,
                    "Affinity apply blocked for process {ProcessName} (PID: {ProcessId})",
                    process.Name,
                    process.ProcessId);

                await this.TryRefreshProcessInfoAsync(process).ConfigureAwait(false);
                return AccessDenied(ex, requestedMask, process.ProcessorAffinity);
            }
            catch (Exception ex) when (IsProcessTerminated(ex))
            {
                this.logger.LogDebug(
                    ex,
                    "Process terminated while applying affinity to {ProcessName} (PID: {ProcessId})",
                    process.Name,
                    process.ProcessId);

                return AffinityApplyResult.Failed(
                    requestedMask,
                    process.ProcessorAffinity,
                    AffinityApplyFailureReason.ProcessTerminated,
                    ProcessOperationUserMessages.ProcessExited);
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(
                    ex,
                    "Affinity apply failed for process {ProcessName} (PID: {ProcessId})",
                    process.Name,
                    process.ProcessId);

                await this.TryRefreshProcessInfoAsync(process).ConfigureAwait(false);
                return AffinityApplyResult.Failed(
                    requestedMask,
                    process.ProcessorAffinity,
                    AffinityApplyFailureReason.ApplyFailed,
                    "ThreadPilot could not apply this affinity change.");
            }

            if (!await this.TryRefreshProcessInfoAsync(process).ConfigureAwait(false))
            {
                return AffinityApplyResult.Failed(
                    requestedMask,
                    process.ProcessorAffinity,
                    AffinityApplyFailureReason.ProcessTerminated,
                    ProcessOperationUserMessages.ProcessExited);
            }

            var verifiedMask = process.ProcessorAffinity;
            if (verifiedMask != requestedMask)
            {
                return AffinityApplyResult.Failed(
                    requestedMask,
                    verifiedMask,
                    AffinityApplyFailureReason.VerificationMismatch,
                    $"Windows reported affinity 0x{verifiedMask:X} after requesting 0x{requestedMask:X}.");
            }

            return AffinityApplyResult.Succeeded(requestedMask, verifiedMask);
        }

        private static AffinityApplyResult AccessDenied(Exception ex, long requestedMask, long verifiedMask)
        {
            var antiCheatLikely = AffinityApplyExceptionClassifier.IsAntiCheatLikely(ex);
            return AffinityApplyResult.Failed(
                antiCheatLikely
                    ? AffinityApplyErrorCodes.AntiCheatOrProtectedProcessLikely
                    : AffinityApplyErrorCodes.AccessDenied,
                antiCheatLikely
                    ? ProcessOperationUserMessages.AntiCheatProtectedLikely
                    : ProcessOperationUserMessages.AccessDenied,
                ex.Message,
                isAccessDenied: true,
                isAntiCheatLikely: antiCheatLikely,
                requestedMask: requestedMask,
                verifiedMask: verifiedMask,
                failureReason: AffinityApplyFailureReason.AccessDenied);
        }

        private static bool IsAccessDenied(Exception ex)
        {
            var message = ex.Message ?? string.Empty;
            return ex is UnauthorizedAccessException ||
                message.Contains("access denied", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("anti-cheat", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("anti cheat", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("protected", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("insufficient privileges", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsProcessTerminated(Exception ex)
        {
            var message = ex.Message ?? string.Empty;
            return ex is ArgumentException ||
                (ex is InvalidOperationException &&
                (message.Contains("process", StringComparison.OrdinalIgnoreCase) &&
                (message.Contains("exit", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("terminated", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("not running", StringComparison.OrdinalIgnoreCase))));
        }

        private async Task<bool> IsProcessRunningAsync(ProcessModel process)
        {
            try
            {
                return await this.processService.IsProcessStillRunning(process).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsAccessDenied(ex))
            {
                this.logger.LogDebug(
                    ex,
                    "Could not confirm process state before affinity apply for {ProcessName} (PID: {ProcessId})",
                    process.Name,
                    process.ProcessId);
                return true;
            }
            catch (Exception ex)
            {
                this.logger.LogDebug(
                    ex,
                    "Process state check failed before affinity apply for {ProcessName} (PID: {ProcessId})",
                    process.Name,
                    process.ProcessId);
                return false;
            }
        }

        private async Task<bool> TryRefreshProcessInfoAsync(ProcessModel process)
        {
            try
            {
                await this.processService.RefreshProcessInfo(process).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex) when (IsAccessDenied(ex))
            {
                this.logger.LogDebug(
                    ex,
                    "Could not refresh process after affinity apply for {ProcessName} (PID: {ProcessId})",
                    process.Name,
                    process.ProcessId);
                return true;
            }
            catch (Exception ex)
            {
                this.logger.LogDebug(
                    ex,
                    "Process refresh failed after affinity apply for {ProcessName} (PID: {ProcessId})",
                    process.Name,
                    process.ProcessId);
                return false;
            }
        }
    }
}
