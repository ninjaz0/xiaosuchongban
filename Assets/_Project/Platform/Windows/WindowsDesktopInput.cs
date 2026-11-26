using System;

namespace Xiaosu.Platform.Windows
{
    /// <summary>
    /// Raw Win32 pointer handling for the pet window. Bypasses Unity Input/EventSystem so
    /// a WS_EX_NOACTIVATE window still receives drags while another app keeps focus.
    /// </summary>
    internal sealed class WindowsDesktopInput
    {
        private WindowsDesktopWindow _window;
        private DesktopWindowSettings _settings;
        private bool _leftDown;
        private bool _dragging;
        private bool _dragNotified;
        private bool _quitRequested;
        private NativeMethods.POINT _grabCursor;
        private WindowRect _grabWindow;
        private int _downTick;

        public event Action Clicked;
        public event Action DragStarted;
        public event Action DragEnded;
        public event Action QuitRequested;

        public void Initialize(WindowsDesktopWindow window, DesktopWindowSettings settings)
        {
            _window = window;
            _settings = settings;
            _leftDown = false;
            _dragging = false;
            _dragNotified = false;
            _quitRequested = false;
        }

        public void Shutdown()
        {
            NativeMethods.ReleaseCapture();
            _window = null;
            _settings = null;
            _leftDown = false;
            _dragging = false;
            _dragNotified = false;
            _quitRequested = false;
        }

        public void RequestQuit()
        {
            _quitRequested = true;
        }

        public void HandleLeftDown(NativeMethods.POINT clientPoint)
        {
            if (_window == null) return;

            NativeMethods.POINT cursor = clientPoint;
            if (!NativeMethods.ClientToScreen(_window.Hwnd, ref cursor))
            {
                NativeMethods.GetCursorPos(out cursor);
            }

            WindowRect windowRect;
            if (!_window.TryGetWindowRect(out windowRect)) return;

            _leftDown = true;
            _dragging = false;
            _dragNotified = false;
            _grabCursor = cursor;
            _grabWindow = windowRect;
            _downTick = Environment.TickCount;
            NativeMethods.SetCapture(_window.Hwnd);
        }

        public void HandleLeftUp()
        {
            if (!_leftDown) return;

            _leftDown = false;
            NativeMethods.ReleaseCapture();

            int elapsed = Environment.TickCount - _downTick;
            if (_dragging)
            {
                _dragging = false;
                _dragNotified = false;
                Action handler = DragEnded;
                if (handler != null) handler();
            }
            else if (elapsed >= 0 && elapsed <= _settings.ClickMaxMilliseconds)
            {
                Action handler = Clicked;
                if (handler != null) handler();
            }
        }

        public void Tick()
        {
            if (_quitRequested)
            {
                _quitRequested = false;
                Action handler = QuitRequested;
                if (handler != null) handler();
                return;
            }

            if (_window == null || !_window.IsInitialized) return;

            bool leftPressed =
                (NativeMethods.GetAsyncKeyState(NativeMethods.VK_LBUTTON) & 0x8000) != 0;

            if (_leftDown && !leftPressed)
            {
                // WM_LBUTTONUP can be lost after a capture change / alt-tab.
                _leftDown = false;
                NativeMethods.ReleaseCapture();
                if (_dragging)
                {
                    _dragging = false;
                    _dragNotified = false;
                    Action handler = DragEnded;
                    if (handler != null) handler();
                }
                return;
            }

            if (!_leftDown) return;

            NativeMethods.POINT cursor;
            if (!NativeMethods.GetCursorPos(out cursor)) return;

            int dx = cursor.X - _grabCursor.X;
            int dy = cursor.Y - _grabCursor.Y;

            if (!_dragging)
            {
                int threshold = (int)Math.Round(_settings.ClickMaxMoveDip * _window.DpiScale);
                if (threshold < 1) threshold = 1;
                if (Math.Abs(dx) + Math.Abs(dy) >= threshold)
                {
                    _dragging = true;
                    if (!_dragNotified)
                    {
                        _dragNotified = true;
                        Action handler = DragStarted;
                        if (handler != null) handler();
                    }
                }
            }

            if (_dragging)
            {
                _window.MoveTo(_grabWindow.Left + dx, _grabWindow.Top + dy);
            }
        }
    }
}
