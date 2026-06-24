namespace ThreadPilot.Core.Tests
{
    using ThreadPilot.Services;

    public sealed class PowerPlanTransitionGateTests
    {
        [Fact]
        public void ShouldApply_WhenTargetWasNotRequested_ReturnsTrue()
        {
            var now = DateTimeOffset.Parse("2026-05-09T10:00:00Z");
            var gate = new PowerPlanTransitionGate(TimeSpan.FromSeconds(2), () => now);

            var decision = gate.ShouldApply("plan-game", "balanced");

            Assert.True(decision.ShouldApply);
            Assert.Equal(PowerPlanTransitionSuppressionReason.None, decision.SuppressionReason);
        }

        [Fact]
        public void ShouldApply_WhenTargetIsAlreadyActive_ReturnsFalse()
        {
            var now = DateTimeOffset.Parse("2026-05-09T10:00:00Z");
            var gate = new PowerPlanTransitionGate(TimeSpan.FromSeconds(2), () => now);

            var decision = gate.ShouldApply("plan-game", "plan-game");

            Assert.False(decision.ShouldApply);
            Assert.Equal(PowerPlanTransitionSuppressionReason.AlreadyActive, decision.SuppressionReason);
        }

        [Fact]
        public void ShouldApply_WhenSameTargetWasRecentlyRequested_ReturnsFalse()
        {
            var now = DateTimeOffset.Parse("2026-05-09T10:00:00Z");
            var gate = new PowerPlanTransitionGate(TimeSpan.FromSeconds(2), () => now);

            Assert.True(gate.ShouldApply("plan-game", "balanced").ShouldApply);
            gate.RecordAttempt("plan-game");
            now = now.AddMilliseconds(500);

            var decision = gate.ShouldApply("plan-game", "balanced");

            Assert.False(decision.ShouldApply);
            Assert.Equal(PowerPlanTransitionSuppressionReason.RecentDuplicateRequest, decision.SuppressionReason);
        }

        [Fact]
        public void ShouldApply_WhenDifferentTargetArrives_UsesLatestTarget()
        {
            var now = DateTimeOffset.Parse("2026-05-09T10:00:00Z");
            var gate = new PowerPlanTransitionGate(TimeSpan.FromSeconds(2), () => now);

            gate.RecordAttempt("plan-game");
            now = now.AddMilliseconds(500);

            var decision = gate.ShouldApply("plan-default", "plan-game");

            Assert.True(decision.ShouldApply);
        }

        [Fact]
        public void ShouldApply_WhenDuplicateWindowExpires_ReturnsTrue()
        {
            var now = DateTimeOffset.Parse("2026-05-09T10:00:00Z");
            var gate = new PowerPlanTransitionGate(TimeSpan.FromSeconds(2), () => now);

            gate.RecordAttempt("plan-game");
            now = now.AddSeconds(3);

            var decision = gate.ShouldApply("plan-game", "balanced");

            Assert.True(decision.ShouldApply);
            Assert.Equal(PowerPlanTransitionSuppressionReason.None, decision.SuppressionReason);
        }

        [Fact]
        public void Constructor_WhenDuplicateWindowIsNegative_UsesZeroWindow()
        {
            var now = DateTimeOffset.Parse("2026-05-09T10:00:00Z");
            var gate = new PowerPlanTransitionGate(TimeSpan.FromSeconds(-1), () => now);

            gate.RecordAttempt("plan-game");

            var decision = gate.ShouldApply("plan-game", "balanced");

            Assert.True(decision.ShouldApply);
        }
    }
}
