namespace ThreadPilot.Core.Tests
{
    using ThreadPilot.Services;

    public sealed class PassiveProcessErrorThrottleTests
    {
        [Fact]
        public void ShouldLog_ReturnsFalseForRepeatedErrorInsideTtl()
        {
            var now = new DateTimeOffset(2026, 5, 9, 12, 0, 0, TimeSpan.Zero);
            var throttle = new PassiveProcessErrorThrottle(TimeSpan.FromMinutes(1), () => now);

            Assert.True(throttle.ShouldLog(42, PassiveProcessErrorKind.AccessDenied));
            Assert.False(throttle.ShouldLog(42, PassiveProcessErrorKind.AccessDenied));
        }

        [Fact]
        public void ShouldLog_ReturnsTrueAfterTtlExpires()
        {
            var now = new DateTimeOffset(2026, 5, 9, 12, 0, 0, TimeSpan.Zero);
            var throttle = new PassiveProcessErrorThrottle(TimeSpan.FromMinutes(1), () => now);

            Assert.True(throttle.ShouldLog(42, PassiveProcessErrorKind.AccessDenied));
            now = now.AddMinutes(2);

            Assert.True(throttle.ShouldLog(42, PassiveProcessErrorKind.AccessDenied));
        }

        [Fact]
        public void ShouldLog_TracksPidAndErrorKindSeparately()
        {
            var now = new DateTimeOffset(2026, 5, 9, 12, 0, 0, TimeSpan.Zero);
            var throttle = new PassiveProcessErrorThrottle(TimeSpan.FromMinutes(1), () => now);

            Assert.True(throttle.ShouldLog(42, PassiveProcessErrorKind.AccessDenied));
            Assert.True(throttle.ShouldLog(42, PassiveProcessErrorKind.Terminated));
            Assert.True(throttle.ShouldLog(43, PassiveProcessErrorKind.AccessDenied));
        }
    }
}
