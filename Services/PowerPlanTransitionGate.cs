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
    public enum PowerPlanTransitionSuppressionReason
    {
        None,
        AlreadyActive,
        RecentDuplicateRequest,
    }

    public sealed record PowerPlanTransitionDecision(
        bool ShouldApply,
        PowerPlanTransitionSuppressionReason SuppressionReason);

    public sealed class PowerPlanTransitionGate
    {
        private readonly TimeSpan duplicateWindow;
        private readonly Func<DateTimeOffset> nowProvider;
        private readonly object lockObject = new();
        private string? lastRequestedPowerPlanGuid;
        private DateTimeOffset lastRequestTime;

        public PowerPlanTransitionGate()
            : this(TimeSpan.FromSeconds(2), () => DateTimeOffset.UtcNow)
        {
        }

        public PowerPlanTransitionGate(TimeSpan duplicateWindow, Func<DateTimeOffset> nowProvider)
        {
            this.duplicateWindow = duplicateWindow < TimeSpan.Zero ? TimeSpan.Zero : duplicateWindow;
            this.nowProvider = nowProvider ?? throw new ArgumentNullException(nameof(nowProvider));
        }

        public PowerPlanTransitionDecision ShouldApply(string targetPowerPlanGuid, string? currentPowerPlanGuid)
        {
            if (string.Equals(targetPowerPlanGuid, currentPowerPlanGuid, StringComparison.OrdinalIgnoreCase))
            {
                return new PowerPlanTransitionDecision(false, PowerPlanTransitionSuppressionReason.AlreadyActive);
            }

            lock (this.lockObject)
            {
                var now = this.nowProvider();
                if (string.Equals(this.lastRequestedPowerPlanGuid, targetPowerPlanGuid, StringComparison.OrdinalIgnoreCase) &&
                    now - this.lastRequestTime < this.duplicateWindow)
                {
                    return new PowerPlanTransitionDecision(false, PowerPlanTransitionSuppressionReason.RecentDuplicateRequest);
                }
            }

            return new PowerPlanTransitionDecision(true, PowerPlanTransitionSuppressionReason.None);
        }

        public void RecordAttempt(string targetPowerPlanGuid)
        {
            lock (this.lockObject)
            {
                this.lastRequestedPowerPlanGuid = targetPowerPlanGuid;
                this.lastRequestTime = this.nowProvider();
            }
        }
    }
}
