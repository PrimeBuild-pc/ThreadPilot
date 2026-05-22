namespace ThreadPilot.Core.Tests
{
    using System.Drawing;
    using ThreadPilot.Services;

    public sealed class SystemTrayPlacementHelperTests
    {
        [Fact]
        public void ResolveMenuOpenPoint_UsesCursorPositionOnFirstOpen()
        {
            var cursor = new Point(1200, 700);

            var result = SystemTrayMenuPlacement.ResolveMenuOpenPoint(cursor, Point.Empty);

            Assert.Equal(cursor, result);
        }

        [Fact]
        public void ResolveMenuOpenPoint_FallsBackToLastKnownPoint_WhenCursorIsUnavailable()
        {
            var lastKnown = new Point(1600, 900);

            var result = SystemTrayMenuPlacement.ResolveMenuOpenPoint(Point.Empty, lastKnown);

            Assert.Equal(lastKnown, result);
        }
    }
}
