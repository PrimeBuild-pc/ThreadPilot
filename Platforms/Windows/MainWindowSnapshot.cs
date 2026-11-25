namespace ThreadPilot.Platforms.Windows
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;

    /// <summary>
    /// The main window of every process from one EnumWindows pass. Process.MainWindowHandle runs its own
    /// EnumWindows over the whole desktop for each process, so a list refresh cost processes × windows.
    /// Same rule as .NET's MainWindowFinder: the first visible, unowned top-level window in Z order.
    /// </summary>
    internal static partial class MainWindowSnapshot
    {
        private const uint GwOwner = 4;

        public static unsafe Dictionary<int, nint> Capture()
        {
            var windows = new Dictionary<int, nint>();
            var handle = GCHandle.Alloc(windows);
            try
            {
                EnumWindows(&OnWindow, GCHandle.ToIntPtr(handle));
            }
            finally
            {
                handle.Free();
            }

            return windows;
        }

        /// <summary>Gets the window title the way Process.MainWindowTitle reads it.</summary>
        public static unsafe string GetTitle(nint window)
        {
            var length = GetWindowTextLengthW(window);
            if (length <= 0)
            {
                return string.Empty;
            }

            var buffer = new char[length + 1];
            fixed (char* text = buffer)
            {
                var copied = GetWindowTextW(window, text, buffer.Length);
                return copied > 0 ? new string(text, 0, copied) : string.Empty;
            }
        }

        [UnmanagedCallersOnly]
        private static int OnWindow(nint window, nint state)
        {
            if (GetWindow(window, GwOwner) == 0 && IsWindowVisible(window))
            {
                _ = GetWindowThreadProcessId(window, out var processId);
                ((Dictionary<int, nint>)GCHandle.FromIntPtr(state).Target!).TryAdd(processId, window);
            }

            return 1;
        }

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static unsafe partial bool EnumWindows(delegate* unmanaged<nint, nint, int> callback, nint state);

        [LibraryImport("user32.dll")]
        private static partial nint GetWindow(nint window, uint command);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool IsWindowVisible(nint window);

        [LibraryImport("user32.dll")]
        private static partial uint GetWindowThreadProcessId(nint window, out int processId);

        [LibraryImport("user32.dll")]
        private static partial int GetWindowTextLengthW(nint window);

        [LibraryImport("user32.dll")]
        private static unsafe partial int GetWindowTextW(nint window, char* text, int maxCount);
    }
}
