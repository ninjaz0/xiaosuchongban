using System;

namespace Xiaosu.Platform
{
    /// <summary>
    /// Pure geometry helpers shared by the native backend and EditMode tests.
    /// No UnityEngine and no P/Invoke here.
    /// </summary>
    public static class DesktopWindowGeometry
    {
        public static bool IsTopEdgeCandidate(
            WindowInfo info,
            int petCenterX,
            int petDiscBottom,
            int tolerancePixels)
        {
            if (!info.IsVisible || info.IsMinimized || info.IsCloaked) return false;
            if (petCenterX < info.Rect.Left || petCenterX >= info.Rect.Right) return false;
            return Math.Abs(info.Rect.Top - petDiscBottom) <= tolerancePixels;
        }

        public static int ClampAnchor(int anchorX, int targetWidth)
        {
            if (targetWidth < 0) targetWidth = 0;
            if (anchorX < 0) return 0;
            if (anchorX > targetWidth) return targetWidth;
            return anchorX;
        }

        public static WindowPoint ComputeFollowPosition(
            WindowRect targetRect,
            int anchorX,
            int centerOffsetX,
            int bottomOffsetY)
        {
            int clamped = ClampAnchor(anchorX, targetRect.Width);
            return new WindowPoint(
                targetRect.Left + clamped - centerOffsetX,
                targetRect.Top - bottomOffsetY);
        }

        public static WindowPoint ComputeFallEnd(
            WindowRect windowRect,
            WindowRect workArea,
            int petDiscBottomOffset)
        {
            int maxX = workArea.Right - windowRect.Width;
            int x = windowRect.Left;
            if (x < workArea.Left) x = workArea.Left;
            if (x > maxX) x = maxX;
            return new WindowPoint(x, workArea.Bottom - petDiscBottomOffset);
        }

        public static int ComputeHitRadius(int clientWidth, int clientHeight, float hitRadiusRatio)
        {
            int radius = (int)Math.Round(clientHeight * hitRadiusRatio) + 2;
            int maxRadius = Math.Min(clientWidth, clientHeight) / 2;
            if (radius > maxRadius) radius = maxRadius;
            if (radius < 1) radius = 1;
            return radius;
        }

        /// <summary>Visual disc bottom relative to the client top (default 0.75 for a 50% disc).</summary>
        public static int PetDiscBottomOffset(int clientHeight, float hitRadiusRatio)
        {
            return (int)Math.Round(clientHeight * (0.5f + hitRadiusRatio));
        }
    }
}
