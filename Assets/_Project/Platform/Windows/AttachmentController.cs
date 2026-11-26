using System;

namespace Xiaosu.Platform.Windows
{
    /// <summary>Free / Attached / Falling state machine for the Windows overlay.</summary>
    internal sealed class AttachmentController
    {
        private enum State
        {
            Free,
            Attached,
            Falling
        }

        private readonly WindowsDesktopWindow _window;
        private readonly WindowTracker _tracker;
        private readonly DesktopWindowSettings _settings;
        private readonly Action<DesktopWindowEvent> _raise;

        private State _state = State.Free;
        private WindowInfo _target;
        private int _anchorX;
        private int _centerOffsetX;
        private int _bottomOffsetY;
        private int _lastX;
        private int _lastY;
        private bool _hasLastPosition;

        private float _fallTime;
        private int _fallStartX;
        private int _fallStartY;
        private int _fallEndX;
        private int _fallEndY;

        public AttachmentController(
            WindowsDesktopWindow window,
            WindowTracker tracker,
            DesktopWindowSettings settings,
            Action<DesktopWindowEvent> raise)
        {
            _window = window;
            _tracker = tracker;
            _settings = settings;
            _raise = raise;
        }

        public bool IsAttached
        {
            get { return _state == State.Attached; }
        }

        public void OnDragStarted()
        {
            if (_state == State.Attached || _state == State.Falling)
            {
                IntPtr detached = _state == State.Attached ? _target.Hwnd : IntPtr.Zero;
                _state = State.Free;
                _hasLastPosition = false;
                Raise(DesktopWindowEventType.Detached, "detached by drag", detached);
            }
        }

        public void OnDragEnded()
        {
            if (_state == State.Falling) return;
            TryAttach();
        }

        public void Tick(float deltaTime)
        {
            if (_window == null || !_window.IsInitialized) return;

            if (_state == State.Attached)
            {
                FollowTarget();
            }
            else if (_state == State.Falling)
            {
                TickFall(deltaTime);
            }
        }

        private void TryAttach()
        {
            WindowRect windowRect;
            WindowRect client;
            if (!_window.TryGetWindowRect(out windowRect) ||
                !_window.TryGetClientRect(out client))
            {
                return;
            }

            var clientOrigin = new NativeMethods.POINT { X = 0, Y = 0 };
            if (!NativeMethods.ClientToScreen(_window.Hwnd, ref clientOrigin)) return;
            if (client.Width <= 0 || client.Height <= 0) return;

            int petCenterX = clientOrigin.X + client.Width / 2;
            int petDiscBottom = clientOrigin.Y +
                DesktopWindowGeometry.PetDiscBottomOffset(client.Height, _settings.HitRadiusRatio);
            int tolerance = (int)Math.Round(_settings.AttachToleranceDip * _window.DpiScale);

            WindowInfo target;
            int anchorX;
            if (!_tracker.TryFindTopEdgeTarget(
                    new WindowPoint(petCenterX, petDiscBottom),
                    tolerance,
                    out target,
                    out anchorX))
            {
                _state = State.Free;
                return;
            }

            int deltaX = (target.Rect.Left + anchorX) - petCenterX;
            int deltaY = target.Rect.Top - petDiscBottom;
            _window.MoveTo(windowRect.Left + deltaX, windowRect.Top + deltaY);

            _target = target;
            _anchorX = anchorX;
            _centerOffsetX = petCenterX - windowRect.Left;
            _bottomOffsetY = petDiscBottom - windowRect.Top;
            _hasLastPosition = false;
            _state = State.Attached;

            Raise(
                DesktopWindowEventType.Attached,
                string.Format("attached to {0} {1}", target.ClassName, target.ProcessId),
                target.Hwnd);
        }

        private void FollowTarget()
        {
            if (!_tracker.IsTargetAlive(_target))
            {
                StartFall("target gone");
                return;
            }

            WindowRect targetRect;
            if (!_tracker.TryGetTargetRect(_target, out targetRect))
            {
                StartFall("target rect unavailable");
                return;
            }

            WindowRect work;
            if (NativeMethods.IsZoomed(_target.Hwnd) ||
                !_tracker.TryGetWorkArea(_target.Hwnd, out work) ||
                targetRect.Top <= work.Top + 2)
            {
                StartFall("target reached screen top");
                return;
            }

            WindowPoint position = DesktopWindowGeometry.ComputeFollowPosition(
                targetRect, _anchorX, _centerOffsetX, _bottomOffsetY);
            _anchorX = DesktopWindowGeometry.ClampAnchor(_anchorX, targetRect.Width);

            if (!_hasLastPosition || position.X != _lastX || position.Y != _lastY)
            {
                _window.MoveTo(position.X, position.Y);
                _lastX = position.X;
                _lastY = position.Y;
                _hasLastPosition = true;
            }
        }

        private void StartFall(string reason)
        {
            WindowRect windowRect;
            WindowRect client;
            if (!_window.TryGetWindowRect(out windowRect) ||
                !_window.TryGetClientRect(out client))
            {
                _state = State.Free;
                return;
            }

            var clientOrigin = new NativeMethods.POINT { X = 0, Y = 0 };
            if (!NativeMethods.ClientToScreen(_window.Hwnd, ref clientOrigin))
            {
                _state = State.Free;
                return;
            }

            WindowRect work;
            if (!_tracker.TryGetWorkArea(_window.Hwnd, out work))
            {
                _state = State.Free;
                return;
            }

            int petDiscBottomOffset =
                (clientOrigin.Y +
                 DesktopWindowGeometry.PetDiscBottomOffset(client.Height, _settings.HitRadiusRatio)) -
                windowRect.Top;
            WindowPoint end = DesktopWindowGeometry.ComputeFallEnd(
                windowRect, work, petDiscBottomOffset);

            _fallStartX = windowRect.Left;
            _fallStartY = windowRect.Top;
            _fallEndX = end.X;
            _fallEndY = end.Y;
            _fallTime = 0f;
            _hasLastPosition = false;
            _state = State.Falling;

            Raise(DesktopWindowEventType.FallStarted, reason, _target.Hwnd);
        }

        private void TickFall(float deltaTime)
        {
            _fallTime += deltaTime;
            float t = _fallTime / _settings.FallDurationSeconds;
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;

            float eased = 1f - (float)Math.Pow(1f - t, 3f);
            int x = (int)Math.Round(_fallStartX + (_fallEndX - _fallStartX) * (double)eased);
            int y = (int)Math.Round(_fallStartY + (_fallEndY - _fallStartY) * (double)eased);
            _window.MoveTo(x, y);

            if (t >= 1f)
            {
                _state = State.Free;
                _hasLastPosition = false;
                Raise(DesktopWindowEventType.FallCompleted, "fall complete", IntPtr.Zero);
            }
        }

        private void Raise(DesktopWindowEventType type, string message, IntPtr target)
        {
            if (_raise == null) return;
            _raise(new DesktopWindowEvent(type, message, target));
        }
    }
}
