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
    using Microsoft.Extensions.Logging;
    using ThreadPilot.Models;

    public enum AffinityApplyFailureReason
    {
        None,
        InvalidMask,
        ProcessTerminated,
        AccessDenied,
        VerificationMismatch,
        ApplyFailed,
    }

    public sealed record AffinityApplyResult(
        bool Success,
        long RequestedMask,
        long VerifiedMask,
        AffinityApplyFailureReason FailureReason,
        string Message)
    {
        public static AffinityApplyResult Succeeded(long requestedMask, long verifiedMask) =>
            new(true, requestedMask, verifiedMask, AffinityApplyFailureReason.None, "Affinity applied successfully.");

        public static AffinityApplyResult Failed(
            long requestedMask,
            long verifiedMask,
            AffinityApplyFailureReason failureReason,
            string message) =>
            new(false, requestedMask, verifiedMask, failureReason, message);
    }

    public interface IAffinityApplyService
    {
        Task<AffinityApplyResult> ApplyAsync(ProcessModel process, long requestedMask);
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
                    "Affinity mask cannot be zero.");
            }

            if (!this.cpuTopologyService.IsAffinityMaskValid(requestedMask))
            {
                return AffinityApplyResult.Failed(
                    requestedMask,
                    startingMask,
                    AffinityApplyFailureReason.InvalidMask,
                    "Affinity mask is not valid for this CPU topology.");
            }

            if (!await this.IsProcessRunningAsync(process).ConfigureAwait(false))
            {
                return AffinityApplyResult.Failed(
                    requestedMask,
                    startingMask,
                    AffinityApplyFailureReason.ProcessTerminated,
                    "Process is no longer running.");
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
                return AffinityApplyResult.Failed(
                    requestedMask,
                    process.ProcessorAffinity,
                    AffinityApplyFailureReason.AccessDenied,
                    "Affinity change blocked (anti-cheat or insufficient privileges).");
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
                    "Process exited before affinity could be applied.");
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
                    $"Failed to set processor affinity: {ex.Message}");
            }

            if (!await this.TryRefreshProcessInfoAsync(process).ConfigureAwait(false))
            {
                return AffinityApplyResult.Failed(
                    requestedMask,
                    process.ProcessorAffinity,
                    AffinityApplyFailureReason.ProcessTerminated,
                    "Process exited before affinity could be verified.");
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
