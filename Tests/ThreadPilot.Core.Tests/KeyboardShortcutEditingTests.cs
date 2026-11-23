namespace ThreadPilot.Core.Tests
{
    using System.Windows.Input;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using ThreadPilot.Models;
    using ThreadPilot.Services;

    public class KeyboardShortcutEditingTests
    {
        [Fact]
        public async Task ApplyAndSaveAsync_DuplicateGestureIsRejectedWithoutUnregisteringEitherAction()
        {
            using var service = CreateService();
            await service.LoadShortcutsFromSettingsAsync();
            var edited = service.GetConfiguredShortcuts().ToList();
            edited.Single(s => s.ActionName == ShortcutActions.ShowMainWindow).Key = Key.H;

            Assert.Equal(Key.H, (await service.ApplyAndSaveAsync(
                edited, () => throw new InvalidOperationException("Unexpected save")))?.Key);

            var registered = await service.GetRegisteredShortcutsAsync();
            Assert.Equal(Key.T, registered[ShortcutActions.ShowMainWindow].Key);
            Assert.Equal(Key.H, registered[ShortcutActions.PowerPlanHighPerformance].Key);
        }

        [Fact]
        public async Task ApplyAndSaveAsync_ReleasesOldGestureAndPersistsDisabledActions()
        {
            using var service = CreateService();
            await service.LoadShortcutsFromSettingsAsync();
            var edited = service.GetConfiguredShortcuts().ToList();
            edited.Single(s => s.ActionName == ShortcutActions.ShowMainWindow).Key = Key.F8;
            edited.Single(s => s.ActionName == ShortcutActions.PowerPlanHighPerformance).IsEnabled = false;
            var saved = false;

            Assert.Null(await service.ApplyAndSaveAsync(edited, () =>
            {
                saved = true;
                return Task.CompletedTask;
            }));

            var registered = await service.GetRegisteredShortcutsAsync();
            Assert.True(saved);
            Assert.Equal(Key.F8, registered[ShortcutActions.ShowMainWindow].Key);
            Assert.DoesNotContain(ShortcutActions.PowerPlanHighPerformance, registered.Keys);
        }

        [Fact]
        public async Task ApplyAndSaveAsync_ConflictKeepsPreviousRegistrationAndSkipsSave()
        {
            using var service = CreateService(blockedKey: Key.F8);
            await service.LoadShortcutsFromSettingsAsync();
            var edited = service.GetConfiguredShortcuts().ToList();
            edited.Single(s => s.ActionName == ShortcutActions.ShowMainWindow).Key = Key.F8;

            Assert.Equal(Key.F8, (await service.ApplyAndSaveAsync(
                edited, () => throw new InvalidOperationException("Unexpected save")))?.Key);

            Assert.Equal(Key.T, (await service.GetRegisteredShortcutsAsync())[ShortcutActions.ShowMainWindow].Key);
        }

        [Fact]
        public async Task ApplyAndSaveAsync_PersistenceFailureRestoresPreviousRegistration()
        {
            using var service = CreateService();
            await service.LoadShortcutsFromSettingsAsync();
            var edited = service.GetConfiguredShortcuts().ToList();
            edited.Single(s => s.ActionName == ShortcutActions.ShowMainWindow).IsEnabled = false;

            await Assert.ThrowsAsync<IOException>(() =>
                service.ApplyAndSaveAsync(edited, () => Task.FromException(new IOException())));

            Assert.Equal(Key.T, (await service.GetRegisteredShortcutsAsync())[ShortcutActions.ShowMainWindow].Key);
        }

        [Fact]
        public async Task LoadShortcutsFromSettingsAsync_ExplicitlyDisabledDefaultsStayDisabled()
        {
            var settings = new ApplicationSettingsModel();
            using var service = CreateService(settings);
            settings.KeyboardShortcuts = service.GetConfiguredShortcuts().Select(s =>
            {
                s.IsEnabled = false;
                return s;
            }).ToList();

            await service.LoadShortcutsFromSettingsAsync();

            Assert.Empty(await service.GetRegisteredShortcutsAsync());
        }

        private static KeyboardShortcutService CreateService(ApplicationSettingsModel? settings = null, Key blockedKey = Key.None)
        {
            var profile = new Mock<IApplicationSettingsService>();
            profile.SetupGet(s => s.Settings).Returns(settings ?? new ApplicationSettingsModel());
            var service = new KeyboardShortcutService(NullLogger<KeyboardShortcutService>.Instance, profile.Object,
                (_, _, _, key) => key != KeyInterop.VirtualKeyFromKey(blockedKey), (_, _) => true);
            service.SetWindowHandle(new nint(123));
            return service;
        }
    }
}
