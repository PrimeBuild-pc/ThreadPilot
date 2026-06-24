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
