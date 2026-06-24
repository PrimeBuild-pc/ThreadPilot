namespace ThreadPilot.Core.Tests
{
    using ThreadPilot.Helpers;

    public sealed class WindowPlacementHelperTests
    {
        [Fact]
        public void CorrectWindowBounds_WhenValuesAreInvalid_CentersDefaultSizeOnPrimaryWorkingArea()
        {
            var monitors = new[]
            {
                new MonitorWorkingArea(0, 0, 1024, 768, true),
            };

            var result = WindowPlacementHelper.CorrectWindowBounds(
                new WindowBounds(double.NaN, double.NegativeInfinity, 0, double.PositiveInfinity),
                monitors);

            Assert.True(result.WasCorrected);
            Assert.Equal(0, result.Bounds.Left);
            Assert.Equal(0, result.Bounds.Top);
            Assert.Equal(1024, result.Bounds.Width);
            Assert.Equal(768, result.Bounds.Height);
        }

        [Fact]
        public void CorrectWindowBounds_WhenMostlyOffScreen_CentersOnNearestWorkingArea()
        {
            var monitors = new[]
            {
                new MonitorWorkingArea(0, 0, 1920, 1040, true),
                new MonitorWorkingArea(1920, 0, 1280, 984, false),
            };

            var result = WindowPlacementHelper.CorrectWindowBounds(
                new WindowBounds(3100, -700, 900, 600),
                monitors);

            Assert.True(result.WasCorrected);
            Assert.Equal(2110, result.Bounds.Left);
            Assert.Equal(192, result.Bounds.Top);
            Assert.Equal(900, result.Bounds.Width);
            Assert.Equal(600, result.Bounds.Height);
        }

        [Fact]
        public void CorrectWindowBounds_WhenPartiallyOutside_ClampsInsideIntersectingWorkingArea()
        {
            var monitors = new[]
            {
                new MonitorWorkingArea(-1080, 0, 1080, 1880, false),
                new MonitorWorkingArea(0, 0, 2560, 1400, true),
            };

            var result = WindowPlacementHelper.CorrectWindowBounds(
                new WindowBounds(-100, 120, 1280, 864),
                monitors);

            Assert.True(result.WasCorrected);
            Assert.Equal(0, result.Bounds.Left);
            Assert.Equal(120, result.Bounds.Top);
            Assert.Equal(1280, result.Bounds.Width);
            Assert.Equal(864, result.Bounds.Height);
        }

        [Fact]
        public void CorrectWindowBounds_WhenAlreadyVisibleOnSecondaryMonitor_DoesNotMoveWindow()
        {
            var monitors = new[]
            {
                new MonitorWorkingArea(0, 0, 1920, 1040, true),
                new MonitorWorkingArea(1920, 0, 1280, 984, false),
            };

            var result = WindowPlacementHelper.CorrectWindowBounds(
                new WindowBounds(2000, 100, 900, 600),
                monitors);

            Assert.False(result.WasCorrected);
            Assert.Equal(2000, result.Bounds.Left);
            Assert.Equal(100, result.Bounds.Top);
            Assert.Equal(900, result.Bounds.Width);
            Assert.Equal(600, result.Bounds.Height);
        }
    }
}
