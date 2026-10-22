namespace ThreadPilot.Core.Tests
{
    using System.Windows;
    using ThreadPilot.Helpers;

    public sealed class StartupWindowBehaviorTests
    {
        [Fact]
        public void Resolve_ShowsNormalWindow_ForManualLaunchWithoutStartMinimized()
        {
            var behavior = StartupWindowBehavior.Resolve(isAutostart: false, startMinimized: false);

            Assert.True(behavior.ShouldShowWindow);
            Assert.True(behavior.ShowInTaskbar);
            Assert.Equal(Visibility.Visible, behavior.Visibility);
            Assert.Equal(WindowState.Normal, behavior.WindowState);
            Assert.False(behavior.HideAfterShow);
            Assert.True(behavior.ActivateAfterShow);
        }

        [Fact]
        public void Resolve_KeepsAWindowTheUserLeftMaximized()
        {
            // App applies this state after MainWindow restored its saved placement, so a plain
            // Normal here turned every saved maximized window back into a normal one.
            var visible = StartupWindowBehavior.Resolve(isAutostart: false, startMinimized: false, restoreMaximized: true);
            var hidden = StartupWindowBehavior.Resolve(isAutostart: false, startMinimized: true, restoreMaximized: true);

            Assert.Equal(WindowState.Maximized, visible.WindowState);
            Assert.Equal(WindowState.Minimized, hidden.WindowState);
        }

        [Fact]
        public void Resolve_HidesToTray_ForManualLaunchWithStartMinimized()
        {
            var behavior = StartupWindowBehavior.Resolve(isAutostart: false, startMinimized: true);

            Assert.False(behavior.ShouldShowWindow);
            Assert.False(behavior.ShowInTaskbar);
            Assert.Equal(Visibility.Hidden, behavior.Visibility);
            Assert.Equal(WindowState.Minimized, behavior.WindowState);
            Assert.False(behavior.HideAfterShow);
            Assert.False(behavior.ActivateAfterShow);
        }

        [Fact]
        public void Resolve_HidesToTray_ForAutostartWithStartMinimized()
        {
            var behavior = StartupWindowBehavior.Resolve(isAutostart: true, startMinimized: true);

            Assert.False(behavior.ShouldShowWindow);
            Assert.False(behavior.ShowInTaskbar);
            Assert.Equal(Visibility.Hidden, behavior.Visibility);
            Assert.Equal(WindowState.Minimized, behavior.WindowState);
            Assert.False(behavior.HideAfterShow);
            Assert.False(behavior.ActivateAfterShow);
        }

        [Fact]
        public void Resolve_ShowsNormalWindow_ForAutostartWithStartMinimizedDisabled()
        {
            var behavior = StartupWindowBehavior.Resolve(isAutostart: true, startMinimized: false);

            Assert.True(behavior.ShouldShowWindow);
            Assert.True(behavior.ShowInTaskbar);
            Assert.Equal(Visibility.Visible, behavior.Visibility);
            Assert.Equal(WindowState.Normal, behavior.WindowState);
            Assert.False(behavior.HideAfterShow);
            Assert.True(behavior.ActivateAfterShow);
        }
    }
}
