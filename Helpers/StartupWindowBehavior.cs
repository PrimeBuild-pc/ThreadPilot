namespace ThreadPilot.Helpers
{
    using System.Windows;

    public readonly record struct StartupWindowBehavior(
        bool ShouldShowWindow,
        bool ShowInTaskbar,
        Visibility Visibility,
        WindowState WindowState,
        bool HideAfterShow,
        bool ActivateAfterShow)
    {
        public static StartupWindowBehavior Resolve(bool isAutostart, bool startMinimized)
        {
            if (isAutostart && startMinimized)
            {
                return new StartupWindowBehavior(
                    ShouldShowWindow: false,
                    ShowInTaskbar: false,
                    Visibility: Visibility.Hidden,
                    WindowState: WindowState.Minimized,
                    HideAfterShow: false,
                    ActivateAfterShow: false);
            }

            if (startMinimized)
            {
                return new StartupWindowBehavior(
                    ShouldShowWindow: false,
                    ShowInTaskbar: false,
                    Visibility: Visibility.Hidden,
                    WindowState: WindowState.Minimized,
                    HideAfterShow: false,
                    ActivateAfterShow: false);
            }

            return new StartupWindowBehavior(
                ShouldShowWindow: true,
                ShowInTaskbar: true,
                Visibility: Visibility.Visible,
                WindowState: WindowState.Normal,
                HideAfterShow: false,
                ActivateAfterShow: true);
        }
    }
}
