/*
 * ThreadPilot - Advanced Windows Process and Power Plan Manager
 * Copyright (C) 2025 Prime Build
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, version 3 only.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
namespace ThreadPilot.Helpers
{
    using System;
    using System.Runtime.InteropServices;
    using System.Windows;
    using System.Windows.Interop;

    /// <summary>
    /// Desktop Window Manager helper methods.
    /// </summary>
    public static class DwmHelper
    {
        private const int DwmUseImmersiveDarkMode = 20;
        private const int DwmUseImmersiveDarkModeLegacy = 19;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        /// <summary>
        /// Applies dark/light title-bar styling through DWM attributes.
        /// </summary>
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
