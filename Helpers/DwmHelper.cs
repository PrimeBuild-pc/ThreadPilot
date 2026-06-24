namespace ThreadPilot.Helpers
{
    using System;
    using System.Runtime.InteropServices;
    using System.Windows;
    using System.Windows.Interop;

    public static class DwmHelper
    {
        private const int DwmUseImmersiveDarkMode = 20;
        private const int DwmUseImmersiveDarkModeLegacy = 19;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        public static void ApplyWindowCaptionTheme(Window window, bool useDarkTheme)
        {
            ArgumentNullException.ThrowIfNull(window);

            var windowHandle = new WindowInteropHelper(window).Handle;
            if (windowHandle == IntPtr.Zero)
            {
                return;
            }

            var darkMode = useDarkTheme ? 1 : 0;
            var result = DwmSetWindowAttribute(windowHandle, DwmUseImmersiveDarkMode, ref darkMode, Marshal.SizeOf<int>());
            if (result != 0)
            {
                _ = DwmSetWindowAttribute(windowHandle, DwmUseImmersiveDarkModeLegacy, ref darkMode, Marshal.SizeOf<int>());
            }
        }
    }
}
