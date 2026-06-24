namespace ThreadPilot.Services
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using Microsoft.Extensions.Logging;

    public static class SelfResourcePolicy
    {
        public static bool ShouldApplyLowImpactMode(bool isHidden, bool enableSelfLowImpactMode)
        {
            return isHidden && enableSelfLowImpactMode;
        }

        public static bool ShouldLimitAffinity(
            bool isHidden,
            bool enableSelfLowImpactMode,
            bool enableSelfAffinityLimit)
        {
            return ShouldApplyLowImpactMode(isHidden, enableSelfLowImpactMode) && enableSelfAffinityLimit;
        }

        public static bool TryCreateLowImpactAffinityMask(int logicalProcessorCount, out long affinityMask)
        {
            affinityMask = 0;

            if (logicalProcessorCount <= 2 || logicalProcessorCount >= 64)
            {
                return false;
            }

            var selectedProcessorCount = logicalProcessorCount >= 4 ? 2 : 1;
            for (var index = logicalProcessorCount - selectedProcessorCount; index < logicalProcessorCount; index++)
            {
                affinityMask |= 1L << index;
            }

            return affinityMask != 0;
        }
    }

    public sealed class SelfResourceManagementService : ISelfResourceManagementService
    {
        private readonly ILogger<SelfResourceManagementService> logger;
        private readonly object syncRoot = new();
        private ProcessPriorityClass? originalPriority;
        private IntPtr? originalAffinity;
        private bool priorityLowered;
        private bool affinityConstrained;

        public SelfResourceManagementService(ILogger<SelfResourceManagementService> logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void ApplyLowImpactMode(bool limitAffinity)
        {
            lock (this.syncRoot)
            {
                this.TryLowerPriority();

                if (limitAffinity)
                {
                    this.TryConstrainAffinity();
                    return;
                }

                this.TryRestoreAffinity();
            }
        }

        public void RestoreForegroundMode()
        {
            lock (this.syncRoot)
            {
                this.TryRestoreAffinity();
                this.TryRestorePriority();
            }
        }

        private void TryLowerPriority()
        {
            if (this.priorityLowered)
            {
                return;
            }

            try
            {
                using var process = Process.GetCurrentProcess();
                var currentPriority = process.PriorityClass;
                if (!ShouldLowerPriority(currentPriority))
                {
                    return;
                }

                this.originalPriority = currentPriority;
                process.PriorityClass = ProcessPriorityClass.BelowNormal;
                this.priorityLowered = true;
                this.logger.LogDebug("Lowered ThreadPilot priority from {OriginalPriority} to BelowNormal", currentPriority);
            }
            catch (Win32Exception ex)
            {
                this.logger.LogDebug(ex, "Windows blocked ThreadPilot self-priority lowering");
            }
            catch (Exception ex)
            {
                this.logger.LogDebug(ex, "Failed to lower ThreadPilot priority");
            }
        }

        private void TryConstrainAffinity()
        {
            if (this.affinityConstrained)
            {
                return;
            }

            try
            {
                if (!SelfResourcePolicy.TryCreateLowImpactAffinityMask(Environment.ProcessorCount, out var candidateMask))
                {
                    return;
                }

                using var process = Process.GetCurrentProcess();
                var currentAffinity = process.ProcessorAffinity;
                var effectiveMask = candidateMask & currentAffinity.ToInt64();
                if (effectiveMask == 0 || effectiveMask == currentAffinity.ToInt64())
                {
                    return;
                }

                this.originalAffinity = currentAffinity;
                process.ProcessorAffinity = new IntPtr(effectiveMask);
                this.affinityConstrained = true;
                this.logger.LogDebug("Constrained ThreadPilot affinity from 0x{OriginalAffinity:X} to 0x{LowImpactAffinity:X}", currentAffinity.ToInt64(), effectiveMask);
            }
            catch (Win32Exception ex)
            {
                this.logger.LogDebug(ex, "Windows blocked ThreadPilot self-affinity limiting");
            }
            catch (Exception ex)
            {
                this.logger.LogDebug(ex, "Failed to constrain ThreadPilot affinity");
            }
        }

        private void TryRestoreAffinity()
        {
            if (!this.affinityConstrained || this.originalAffinity == null)
            {
                return;
            }

            try
            {
                using var process = Process.GetCurrentProcess();
                process.ProcessorAffinity = this.originalAffinity.Value;
                this.logger.LogDebug("Restored ThreadPilot affinity to 0x{OriginalAffinity:X}", this.originalAffinity.Value.ToInt64());
            }
            catch (Exception ex)
            {
                this.logger.LogDebug(ex, "Failed to restore ThreadPilot affinity");
            }
            finally
            {
                this.affinityConstrained = false;
                this.originalAffinity = null;
            }
        }

        private void TryRestorePriority()
        {
            if (!this.priorityLowered || this.originalPriority == null)
            {
                return;
            }

            try
            {
                using var process = Process.GetCurrentProcess();
                process.PriorityClass = this.originalPriority.Value;
                this.logger.LogDebug("Restored ThreadPilot priority to {OriginalPriority}", this.originalPriority.Value);
            }
            catch (Exception ex)
            {
                this.logger.LogDebug(ex, "Failed to restore ThreadPilot priority");
            }
            finally
            {
                this.priorityLowered = false;
                this.originalPriority = null;
            }
        }

        private static bool ShouldLowerPriority(ProcessPriorityClass priority)
        {
            return priority is ProcessPriorityClass.Normal
                or ProcessPriorityClass.AboveNormal
                or ProcessPriorityClass.High
                or ProcessPriorityClass.RealTime;
        }
    }
}
