namespace ThreadPilot.Services
{
    using System;
    using System.Runtime.InteropServices;

    public sealed class WindowsForegroundWindowProvider : IForegroundWindowProvider
    {
        private const int DwmwaCloaked = 14;

        public bool TryGetForegroundWindow(out ForegroundWindowSnapshot snapshot)
        {
            snapshot = default;

            var windowHandle = GetForegroundWindow();
            if (windowHandle == IntPtr.Zero)
            {
                return false;
            }

            _ = GetWindowThreadProcessId(windowHandle, out var processId);
            if (processId == 0)
            {
                return false;
            }

            snapshot = new ForegroundWindowSnapshot(
                windowHandle,
                unchecked((int)processId),
                IsWindowVisible(windowHandle),
                IsWindowCloaked(windowHandle));
            return true;
        }

        private static bool IsWindowCloaked(IntPtr windowHandle)
        {
            var result = DwmGetWindowAttribute(
                windowHandle,
                DwmwaCloaked,
                out int cloaked,
                Marshal.SizeOf<int>());

            return result == 0 && cloaked != 0;
        }

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmGetWindowAttribute(
            IntPtr hwnd,
            int dwAttribute,
            out int pvAttribute,
            int cbAttribute);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);
    }
}
