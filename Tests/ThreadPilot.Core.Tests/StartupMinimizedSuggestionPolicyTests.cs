namespace ThreadPilot.Core.Tests
{
    using ThreadPilot.Helpers;
    using ThreadPilot.Models;

    public sealed class StartupMinimizedSuggestionPolicyTests
    {
        [Fact]
        public void ShouldShow_ReturnsTrue_ForFirstVisibleNormalLaunchWithoutStartMinimized()
        {
            var settings = new ApplicationSettingsModel();
            var behavior = StartupWindowBehavior.Resolve(isAutostart: false, startMinimized: false);

            Assert.True(StartupMinimizedSuggestionPolicy.ShouldShow(settings, behavior));
        }

        [Fact]
        public void ShouldShow_ReturnsFalse_WhenStartupIsSilent()
        {
            var settings = new ApplicationSettingsModel();
            var behavior = StartupWindowBehavior.Resolve(isAutostart: false, startMinimized: true);

            Assert.False(StartupMinimizedSuggestionPolicy.ShouldShow(settings, behavior));
        }

        [Fact]
        public void ShouldShow_ReturnsFalse_WhenSuggestionWasAlreadySeen()
        {
            var settings = new ApplicationSettingsModel();
            settings.HasSeenStartupMinimizedSuggestion = true;
            var behavior = StartupWindowBehavior.Resolve(isAutostart: false, startMinimized: false);

            Assert.False(StartupMinimizedSuggestionPolicy.ShouldShow(settings, behavior));
        }
    }
}
