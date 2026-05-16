namespace ThreadPilot.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows;
    using System.Windows.Forms;
    using System.Windows.Media;
    using DrawingRectangle = System.Drawing.Rectangle;
    using WpfPoint = System.Windows.Point;

    public readonly record struct WindowBounds(double Left, double Top, double Width, double Height)
    {
        public double Right => this.Left + this.Width;

        public double Bottom => this.Top + this.Height;

        public double Area => Math.Max(0, this.Width) * Math.Max(0, this.Height);
    }

    public readonly record struct MonitorWorkingArea(double Left, double Top, double Width, double Height, bool IsPrimary)
    {
        public double Right => this.Left + this.Width;

        public double Bottom => this.Top + this.Height;

        public double Area => Math.Max(0, this.Width) * Math.Max(0, this.Height);
    }

    public readonly record struct WindowPlacementCorrection(WindowBounds Bounds, bool WasCorrected);

    public static class WindowPlacementHelper
    {
        private const double DefaultWindowWidth = 1280;
        private const double DefaultWindowHeight = 864;
        private const double MinimumVisibleAreaRatio = 0.25;
        private const double Epsilon = 0.001;

        public static WindowPlacementCorrection CorrectWindowBounds(
            WindowBounds currentBounds,
            IReadOnlyCollection<MonitorWorkingArea> workingAreas)
        {
            var validWorkingAreas = workingAreas
                .Where(IsValidWorkingArea)
                .ToArray();

            if (validWorkingAreas.Length == 0)
            {
                var fallbackBounds = new WindowBounds(
                    IsFinite(currentBounds.Left) ? currentBounds.Left : 0,
                    IsFinite(currentBounds.Top) ? currentBounds.Top : 0,
                    ResolveDimension(currentBounds.Width, DefaultWindowWidth),
                    ResolveDimension(currentBounds.Height, DefaultWindowHeight));

                return new WindowPlacementCorrection(fallbackBounds, !BoundsAreEquivalent(currentBounds, fallbackBounds));
            }

            var targetArea = SelectTargetWorkingArea(currentBounds, validWorkingAreas);
            var correctedWidth = Math.Min(ResolveDimension(currentBounds.Width, DefaultWindowWidth), targetArea.Width);
            var correctedHeight = Math.Min(ResolveDimension(currentBounds.Height, DefaultWindowHeight), targetArea.Height);
            var sanitizedBounds = new WindowBounds(
                currentBounds.Left,
                currentBounds.Top,
                correctedWidth,
                correctedHeight);

            var hasSufficientIntersection = HasSufficientIntersection(sanitizedBounds, targetArea);
            double correctedLeft;
            double correctedTop;

            if (!hasSufficientIntersection || !IsFinite(sanitizedBounds.Left) || !IsFinite(sanitizedBounds.Top))
            {
                correctedLeft = targetArea.Left + ((targetArea.Width - correctedWidth) / 2);
                correctedTop = targetArea.Top + ((targetArea.Height - correctedHeight) / 2);
            }
            else
            {
                correctedLeft = Clamp(sanitizedBounds.Left, targetArea.Left, targetArea.Right - correctedWidth);
                correctedTop = Clamp(sanitizedBounds.Top, targetArea.Top, targetArea.Bottom - correctedHeight);
            }

            var correctedBounds = new WindowBounds(
                Math.Round(correctedLeft),
                Math.Round(correctedTop),
                Math.Round(correctedWidth),
                Math.Round(correctedHeight));

            return new WindowPlacementCorrection(correctedBounds, !BoundsAreEquivalent(currentBounds, correctedBounds));
        }

        public static bool TryCorrectWindowPlacement(Window window)
        {
            ArgumentNullException.ThrowIfNull(window);

            try
            {
                if (window.WindowState == WindowState.Maximized)
                {
                    return false;
                }

                var workingAreas = GetWorkingAreasInWindowDips(window);
                var currentBounds = new WindowBounds(
                    window.Left,
                    window.Top,
                    ResolveWindowDimension(window.ActualWidth, window.Width, DefaultWindowWidth),
                    ResolveWindowDimension(window.ActualHeight, window.Height, DefaultWindowHeight));

                var correction = CorrectWindowBounds(currentBounds, workingAreas);
                if (!correction.WasCorrected)
                {
                    return false;
                }

                window.Width = correction.Bounds.Width;
                window.Height = correction.Bounds.Height;
                window.Left = correction.Bounds.Left;
                window.Top = correction.Bounds.Top;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static MonitorWorkingArea[] GetWorkingAreasInWindowDips(Window window)
        {
            var transformFromDevice = GetTransformFromDevice(window);
            var screens = Screen.AllScreens;
            if (screens.Length == 0)
            {
                return new[]
                {
                    new MonitorWorkingArea(
                        SystemParameters.WorkArea.Left,
                        SystemParameters.WorkArea.Top,
                        SystemParameters.WorkArea.Width,
                        SystemParameters.WorkArea.Height,
                        true),
                };
            }

            return screens
                .Select(screen => ConvertWorkingAreaToDips(screen.WorkingArea, screen.Primary, transformFromDevice))
                .Where(IsValidWorkingArea)
                .ToArray();
        }

        private static Matrix GetTransformFromDevice(Window window)
        {
            try
            {
                var source = PresentationSource.FromVisual(window);
                return source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
            }
            catch
            {
                return Matrix.Identity;
            }
        }

        private static MonitorWorkingArea ConvertWorkingAreaToDips(
            DrawingRectangle workingArea,
            bool isPrimary,
            Matrix transformFromDevice)
        {
            var topLeft = transformFromDevice.Transform(new WpfPoint(workingArea.Left, workingArea.Top));
            var bottomRight = transformFromDevice.Transform(new WpfPoint(workingArea.Right, workingArea.Bottom));

            return new MonitorWorkingArea(
                topLeft.X,
                topLeft.Y,
                bottomRight.X - topLeft.X,
                bottomRight.Y - topLeft.Y,
                isPrimary);
        }

        private static MonitorWorkingArea SelectTargetWorkingArea(
            WindowBounds currentBounds,
            IReadOnlyList<MonitorWorkingArea> workingAreas)
        {
            var bestIntersection = workingAreas
                .Select(area => new
                {
                    Area = area,
                    IntersectionArea = GetIntersectionArea(currentBounds, area),
                })
                .OrderByDescending(candidate => candidate.IntersectionArea)
                .ThenByDescending(candidate => candidate.Area.IsPrimary)
                .First();

            if (bestIntersection.IntersectionArea > 0 && HasSufficientIntersection(currentBounds, bestIntersection.Area))
            {
                return bestIntersection.Area;
            }

            if (!IsFinite(currentBounds.Left) || !IsFinite(currentBounds.Top))
            {
                return workingAreas.FirstOrDefault(area => area.IsPrimary, workingAreas[0]);
            }

            var centerX = currentBounds.Left + (ResolveDimension(currentBounds.Width, DefaultWindowWidth) / 2);
            var centerY = currentBounds.Top + (ResolveDimension(currentBounds.Height, DefaultWindowHeight) / 2);

            return workingAreas
                .OrderBy(area => GetSquaredDistanceToArea(centerX, centerY, area))
                .ThenByDescending(area => area.IsPrimary)
                .First();
        }

        private static double GetSquaredDistanceToArea(double x, double y, MonitorWorkingArea area)
        {
            var nearestX = Clamp(x, area.Left, area.Right);
            var nearestY = Clamp(y, area.Top, area.Bottom);
            var deltaX = x - nearestX;
            var deltaY = y - nearestY;

            return (deltaX * deltaX) + (deltaY * deltaY);
        }

        private static bool HasSufficientIntersection(WindowBounds bounds, MonitorWorkingArea area)
        {
            var intersectionArea = GetIntersectionArea(bounds, area);
            var boundedWindowArea = Math.Max(1, Math.Min(bounds.Width, area.Width) * Math.Min(bounds.Height, area.Height));

            return intersectionArea / boundedWindowArea >= MinimumVisibleAreaRatio;
        }

        private static double GetIntersectionArea(WindowBounds bounds, MonitorWorkingArea area)
        {
            if (!IsFinite(bounds.Left) || !IsFinite(bounds.Top) || !IsFinite(bounds.Width) || !IsFinite(bounds.Height))
            {
                return 0;
            }

            var left = Math.Max(bounds.Left, area.Left);
            var top = Math.Max(bounds.Top, area.Top);
            var right = Math.Min(bounds.Right, area.Right);
            var bottom = Math.Min(bounds.Bottom, area.Bottom);
            var width = Math.Max(0, right - left);
            var height = Math.Max(0, bottom - top);

            return width * height;
        }

        private static bool IsValidWorkingArea(MonitorWorkingArea workingArea)
        {
            return IsFinite(workingArea.Left)
                && IsFinite(workingArea.Top)
                && IsFinite(workingArea.Width)
                && IsFinite(workingArea.Height)
                && workingArea.Width > 0
                && workingArea.Height > 0;
        }

        private static double ResolveWindowDimension(double actualValue, double configuredValue, double fallback)
        {
            if (IsFinite(actualValue) && actualValue > 0)
            {
                return actualValue;
            }

            return ResolveDimension(configuredValue, fallback);
        }

        private static double ResolveDimension(double value, double fallback)
        {
            return IsFinite(value) && value > 0 ? value : fallback;
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            if (maximum < minimum)
            {
                return minimum;
            }

            return Math.Min(Math.Max(value, minimum), maximum);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool BoundsAreEquivalent(WindowBounds left, WindowBounds right)
        {
            return Math.Abs(left.Left - right.Left) < Epsilon
                && Math.Abs(left.Top - right.Top) < Epsilon
                && Math.Abs(left.Width - right.Width) < Epsilon
                && Math.Abs(left.Height - right.Height) < Epsilon;
        }
    }
}
