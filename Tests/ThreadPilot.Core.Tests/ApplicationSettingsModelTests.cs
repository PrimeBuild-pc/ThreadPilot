namespace ThreadPilot.Core.Tests
{
    using ThreadPilot.Models;

    public sealed class ApplicationSettingsModelTests
    {
        [Fact]
        public void Constructor_StartMinimizedDefaultsFalse_ForManualLaunchVisibility()
        {
            var settings = new ApplicationSettingsModel();

            Assert.True(settings.AutostartWithWindows);
            Assert.False(settings.StartMinimized);
            Assert.True(settings.ApplyPersistentRulesOnProcessStart);
            Assert.False(settings.HasSeenStartupMinimizedSuggestion);
        }

        [Fact]
        public void HasSameUserSettingsAs_ReturnsTrue_WhenChangedSettingIsRestored()
        {
            var savedSettings = new ApplicationSettingsModel
            {
                EnableNotifications = true,
            };

            var editableSettings = (ApplicationSettingsModel)savedSettings.Clone();
            editableSettings.EnableNotifications = false;
            Assert.False(editableSettings.HasSameUserSettingsAs(savedSettings));

            editableSettings.EnableNotifications = true;

            Assert.True(editableSettings.HasSameUserSettingsAs(savedSettings));
        }

        [Fact]
        public void HasSameUserSettingsAs_IgnoresMetadataTimestamps()
        {
            var savedSettings = new ApplicationSettingsModel
            {
                UpdatedAt = new System.DateTime(2026, 5, 16, 10, 0, 0, System.DateTimeKind.Utc),
            };
            var editableSettings = (ApplicationSettingsModel)savedSettings.Clone();
            editableSettings.UpdatedAt = savedSettings.UpdatedAt.AddMinutes(5);

            Assert.True(editableSettings.HasSameUserSettingsAs(savedSettings));
        }
    }
}
