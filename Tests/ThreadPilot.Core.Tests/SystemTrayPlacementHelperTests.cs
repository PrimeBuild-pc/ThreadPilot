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

            var result = SystemTrayMenuPlacement.ResolveMenuOpenPoint(
                cursor,
                Point.Empty,
                Rectangle.Empty,
                new Rectangle(0, 0, 1920, 1080));

            Assert.Equal(cursor, result);
            Assert.NotEqual(Point.Empty, result);
        }

        [Fact]
        public void ResolveMenuOpenPoint_FallsBackToLastKnownPoint_WhenCursorIsUnavailable()
        {
            var lastKnown = new Point(1600, 900);

            var result = SystemTrayMenuPlacement.ResolveMenuOpenPoint(
                Point.Empty,
                lastKnown,
                Rectangle.Empty,
                new Rectangle(0, 0, 1920, 1080));

            Assert.Equal(lastKnown, result);
        }

        [Fact]
        public void ResolveMenuOpenPoint_WhenFirstCursorIsUnavailable_UsesTaskbarAreaInsteadOfTopLeft()
        {
            var result = SystemTrayMenuPlacement.ResolveMenuOpenPoint(
                Point.Empty,
                Point.Empty,
                Rectangle.Empty,
                new Rectangle(0, 0, 1920, 1080));

            Assert.NotEqual(Point.Empty, result);
            Assert.True(result.X > 0);
            Assert.True(result.Y > 0);
        }

        [Fact]
        public void ResolveMenuOpenPoint_WhenTrayBoundsAreInvalid_FallsBackToCursor()
        {
            var cursor = new Point(900, 500);

            var result = SystemTrayMenuPlacement.ResolveMenuOpenPoint(
                cursor,
                Point.Empty,
                Rectangle.Empty,
                new Rectangle(0, 0, 1920, 1080));

            Assert.Equal(cursor, result);
        }
    }
}
