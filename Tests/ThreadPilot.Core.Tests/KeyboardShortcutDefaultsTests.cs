namespace ThreadPilot.Core.Tests
{
    using System.Windows.Input;
    using Microsoft.Extensions.Logging;
    using Moq;
    using ThreadPilot.Models;
    using ThreadPilot.Services;

    public sealed class KeyboardShortcutDefaultsTests
    {
        [Fact]
        public async Task LoadShortcutsFromSettingsAsync_WithEmptyList_AttemptsTheDefaultShortcuts()
        {
            var logger = new RecordingLogger<KeyboardShortcutService>();
            using var service = CreateService(logger, new ApplicationSettingsModel());

            await service.LoadShortcutsFromSettingsAsync();

            var attempted = logger.Messages.Count(message => message.Contains("Skipped registering shortcut", StringComparison.Ordinal));
            Assert.Equal(service.GetDefaultShortcuts().Count, attempted);
        }

        [Fact]
        public async Task LoadShortcutsFromSettingsAsync_WithConfiguredShortcuts_DoesNotFallBackToDefaults()
        {
            var logger = new RecordingLogger<KeyboardShortcutService>();
            var settings = new ApplicationSettingsModel
            {
                KeyboardShortcuts =
                [
                    new KeyboardShortcut
                    {
                        ActionName = ShortcutActions.ShowMainWindow,
                        Key = Key.F8,
                        Modifiers = ModifierKeys.Control,
                        IsEnabled = true,
                        IsGlobal = true,
                    },
                ],
            };
            using var service = CreateService(logger, settings);

            await service.LoadShortcutsFromSettingsAsync();

            var attempted = logger.Messages.Count(message => message.Contains("Skipped registering shortcut", StringComparison.Ordinal));
            Assert.Equal(1, attempted);
        }

        [Fact]
        public void GetDefaultShortcuts_AreAllEnabledAndGlobal()
        {
            var logger = new RecordingLogger<KeyboardShortcutService>();
            using var service = CreateService(logger, new ApplicationSettingsModel());

            var defaults = service.GetDefaultShortcuts();

            Assert.NotEmpty(defaults);
            Assert.All(defaults.Values, shortcut =>
            {
                Assert.True(shortcut.IsEnabled);
                Assert.True(shortcut.IsGlobal);
                Assert.False(string.IsNullOrWhiteSpace(shortcut.ActionName));
            });
        }

        private static KeyboardShortcutService CreateService(
            ILogger<KeyboardShortcutService> logger,
            ApplicationSettingsModel settings)
        {
            var settingsService = new Mock<IApplicationSettingsService>(MockBehavior.Strict);
            settingsService.SetupGet(service => service.Settings).Returns(settings);
            return new KeyboardShortcutService(logger, settingsService.Object);
        }

        private sealed class RecordingLogger<T> : ILogger<T>
        {
            public List<string> Messages { get; } = new();

            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                this.Messages.Add(formatter(state, exception));
            }
        }
    }
}
