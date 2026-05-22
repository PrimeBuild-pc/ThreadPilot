namespace ThreadPilot.Services
{
    using System.Drawing;

    public static class SystemTrayMenuPlacement
    {
        public static Point ResolveMenuOpenPoint(Point cursorPosition, Point lastKnownPosition)
        {
            if (!cursorPosition.IsEmpty)
            {
                return cursorPosition;
            }

            return lastKnownPosition.IsEmpty ? new Point(1, 1) : lastKnownPosition;
        }
    }
}
