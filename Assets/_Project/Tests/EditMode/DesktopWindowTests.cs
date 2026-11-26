using NUnit.Framework;
using Xiaosu.Platform;

namespace Xiaosu.Tests
{
    public sealed class DesktopWindowTests
    {
        [Test]
        public void TopEdgeCandidateRequiresOverlapAndTolerance()
        {
            var window = new WindowInfo
            {
                Hwnd = new System.IntPtr(1),
                ProcessId = 42,
                ClassName = "Notepad",
                Rect = new WindowRect { Left = 100, Top = 100, Right = 500, Bottom = 500 },
                IsVisible = true
            };

            Assert.That(DesktopWindowGeometry.IsTopEdgeCandidate(window, 300, 110, 24), Is.True);
            Assert.That(DesktopWindowGeometry.IsTopEdgeCandidate(window, 50, 110, 24), Is.False);
            Assert.That(DesktopWindowGeometry.IsTopEdgeCandidate(window, 300, 200, 24), Is.False);

            window.IsMinimized = true;
            Assert.That(DesktopWindowGeometry.IsTopEdgeCandidate(window, 300, 110, 24), Is.False);
        }

        [Test]
        public void AnchorIsClampedToTargetWidth()
        {
            Assert.That(DesktopWindowGeometry.ClampAnchor(-5, 600), Is.EqualTo(0));
            Assert.That(DesktopWindowGeometry.ClampAnchor(250, 600), Is.EqualTo(250));
            Assert.That(DesktopWindowGeometry.ClampAnchor(900, 600), Is.EqualTo(600));
        }

        [Test]
        public void FollowPositionUsesAnchorAndOffsets()
        {
            var target = new WindowRect { Left = 100, Top = 100, Right = 600, Bottom = 500 };
            WindowPoint point = DesktopWindowGeometry.ComputeFollowPosition(target, 250, 100, 75);
            Assert.That(point.X, Is.EqualTo(250));
            Assert.That(point.Y, Is.EqualTo(25));
        }

        [Test]
        public void FallEndLandsOnWorkAreaBottom()
        {
            var window = new WindowRect { Left = 800, Top = 200, Right = 1000, Bottom = 400 };
            var work = new WindowRect { Left = 0, Top = 0, Right = 1920, Bottom = 1040 };
            WindowPoint end = DesktopWindowGeometry.ComputeFallEnd(window, work, 75);
            Assert.That(end.X, Is.EqualTo(800));
            Assert.That(end.Y, Is.EqualTo(965));

            var offRight = new WindowRect { Left = 1800, Top = 200, Right = 2000, Bottom = 400 };
            WindowPoint clamped = DesktopWindowGeometry.ComputeFallEnd(offRight, work, 75);
            Assert.That(clamped.X, Is.EqualTo(1720));
        }

        [Test]
        public void HitRadiusMatchesHalfHeightDisc()
        {
            Assert.That(DesktopWindowGeometry.ComputeHitRadius(250, 250, 0.25f), Is.EqualTo(64));
            Assert.That(DesktopWindowGeometry.ComputeHitRadius(100, 300, 0.25f), Is.EqualTo(50));
            Assert.That(DesktopWindowGeometry.PetDiscBottomOffset(250, 0.25f), Is.EqualTo(188));
        }
    }
}
