namespace ThreadPilot.Core.Tests
{
    using ThreadPilot.Services;

    public sealed class SmartNotificationQuietHoursTests
    {
        [Theory]
        [InlineData(23, 0, 22, 8, true)]
        [InlineData(2, 0, 22, 8, true)]
        [InlineData(7, 59, 22, 8, true)]
        [InlineData(22, 0, 22, 8, true)]
        [InlineData(8, 0, 22, 8, true)]
        [InlineData(12, 0, 22, 8, false)]
        [InlineData(21, 59, 22, 8, false)]
        [InlineData(9, 0, 22, 8, false)]
        [InlineData(12, 0, 9, 17, true)]
        [InlineData(9, 0, 9, 17, true)]
        [InlineData(17, 0, 9, 17, true)]
        [InlineData(8, 59, 9, 17, false)]
        [InlineData(17, 1, 9, 17, false)]
        [InlineData(23, 0, 9, 17, false)]
        public void IsWithinQuietHours_HandlesOvernightAndSameDayWindows(
            int hour,
            int minute,
            int startHour,
            int endHour,
            bool expected)
        {
            var actual = SmartNotificationService.IsWithinQuietHours(
                new TimeSpan(hour, minute, 0),
                TimeSpan.FromHours(startHour),
                TimeSpan.FromHours(endHour));

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void IsWithinQuietHours_ZeroLengthWindow_IsNeverActive()
        {
            Assert.False(SmartNotificationService.IsWithinQuietHours(
                TimeSpan.FromHours(10),
                TimeSpan.FromHours(10),
                TimeSpan.FromHours(10)));
        }
    }
}
