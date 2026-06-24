namespace ThreadPilot.Services
{
    using System.Drawing;

    public static class SystemTrayMenuPlacement
    {
        public static Point ResolveMenuOpenPoint(Point cursorPosition, Point lastKnownPosition)
        {
            return ResolveMenuOpenPoint(
                cursorPosition,
                lastKnownPosition,
                Rectangle.Empty,
                Rectangle.Empty);
        }

        public static Point ResolveMenuOpenPoint(
            Point cursorPosition,
            Point lastKnownPosition,
            Rectangle trayBounds,
            Rectangle fallbackWorkingArea)
        {
            if (!cursorPosition.IsEmpty)
            {
                return cursorPosition;
            }

            if (!lastKnownPosition.IsEmpty)
            {
                return lastKnownPosition;
            }

            if (!trayBounds.IsEmpty)
            {
                return new Point(trayBounds.Left + (trayBounds.Width / 2), trayBounds.Top + (trayBounds.Height / 2));
            }

            if (!fallbackWorkingArea.IsEmpty)
            {
                return new Point(
                    Math.Max(fallbackWorkingArea.Left + 1, fallbackWorkingArea.Right - 8),
                    Math.Max(fallbackWorkingArea.Top + 1, fallbackWorkingArea.Bottom - 8));
            }

            return new Point(16, 16);
        }
    }
}
