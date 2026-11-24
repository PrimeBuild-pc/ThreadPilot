namespace ThreadPilot.Core.Tests
{
    using ThreadPilot.Helpers;
    using ThreadPilot.Models;

    public sealed class AutostartOffNoticePolicyTests
    {
        [Theory]
        [InlineData(false, false, true)]
        [InlineData(false, true, false)]
        [InlineData(true, false, false)]
        public void ShouldShow_OnlyWhileAutostartIsOffAndTheNoticeIsUnseen(bool autostart, bool seen, bool expected)
        {
            var settings = new ApplicationSettingsModel { AutostartWithWindows = autostart, HasSeenAutostartOffNotice = seen };

            Assert.Equal(expected, AutostartOffNoticePolicy.ShouldShow(settings));
        }
    }
}
