using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Xiaosu.Platform.Windows
{
    /// <summary>
    /// Windows implementation: borderless topmost non-activating window, DWM alpha,
    /// circular SetWindowRgn click-through, raw Win32 drag, and top-edge attach/follow/fall.
    /// Pure C# (no UnityEngine) so it can live in Xiaosu.Platform.
    /// </summary>
    internal sealed class WindowsDesktopWindow : IDesktopWindowOverlay
    {
        private IntPtr _hwnd;
        private IntPtr _oldWndProc;
        private NativeMethods.WndProcDelegate _wndProc;
        private bool _initialized;
        private int _awareness;
        private uint _dpi = 96;
        private DesktopWindowSettings _settings = new DesktopWindowSettings();

        private WindowsDesktopInput _input;
        private WindowTracker _tracker;
        private AttachmentController _attachment;

        private bool _alwaysOnTop = true;
        private bool _clickThrough = true;
        private long _originalStyle;
        private long _originalExStyle;
        private int _regionWidth;
        private int _regionHeight;
        private int _lastRegionTick;

        public event Action<DesktopWindowEvent> StateChanged;

        public bool IsInitialized
        {
            get { return _initialized && _hwnd != IntPtr.Zero; }
        }

        public bool IsAttached
        {
            get { return _attachment != null && _attachment.IsAttached; }
        }

        public IntPtr Hwnd
        {
            get { return _hwnd; }
        }

        public float DpiScale
        {
            get { return _dpi <= 0 ? 1f : _dpi / 96f; }
        }

        public int Awareness
        {
            get { return _awareness; }
        }

        public void Initialize(DesktopWindowSettings settings)
        {
            if (_initialized) return;

            _settings = settings ?? new DesktopWindowSettings();
            _settings.Validate();

            _hwnd = FindOwnWindow();
            if (_hwnd == IntPtr.Zero)
            {
                LogError("Could not find the Unity player window.");
                Raise(DesktopWindowEventType.Error, "player window not found", IntPtr.Zero);
                return;
            }

            NativeMethods.GetProcessDpiAwareness(IntPtr.Zero, out _awareness);
            _dpi = NativeMethods.GetDpiForWindow(_hwnd);
            if (_dpi == 0) _dpi = 96;

            _originalStyle = NativeMethods.GetWindowLongPtr(_hwnd, NativeMethods.GWL_STYLE).ToInt64();
            _originalExStyle = NativeMethods.GetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();

            ApplyStyles();
            ApplyDwm();
            StabilizeClientSize();
            if (_clickThrough) ApplyRegion();
            SubclassWindow();
            RegisterQuitHotkey();

            _tracker = new WindowTracker(_hwnd, _awareness, _settings);
            _attachment = new AttachmentController(this, _tracker, _settings, OnAttachmentEvent);

            _input = new WindowsDesktopInput();
            _input.Initialize(this, _settings);
            _input.Clicked += OnInputClicked;
            _input.DragStarted += OnInputDragStarted;
            _input.DragEnded += OnInputDragEnded;
            _input.QuitRequested += OnInputQuitRequested;

            PositionBottomRight();
            if (_alwaysOnTop) SetWindowPosToTopmost();

            _initialized = true;
            LogDiagnostics();
        }

        public void Tick(float deltaTime)
        {
            if (!_initialized) return;

            EnsureRegion();
            if (_input != null) _input.Tick();
            if (_attachment != null) _attachment.Tick(deltaTime);
        }

        public void SetAlwaysOnTop(bool enabled)
        {
            _alwaysOnTop = enabled;
            if (_hwnd == IntPtr.Zero) return;

            NativeMethods.SetWindowPos(
                _hwnd,
                enabled ? NativeMethods.HWND_TOPMOST : NativeMethods.HWND_NOTOPMOST,
                0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE |
                NativeMethods.SWP_NOSIZE |
                NativeMethods.SWP_NOACTIVATE |
                NativeMethods.SWP_NOOWNERZORDER);
        }

        public void SetClickThrough(bool enabled)
        {
            _clickThrough = enabled;
            if (!_initialized) return;

            if (enabled) ApplyRegion();
            else RemoveRegion();
        }

        public void BeginDrag()
        {
            // The Windows backend drives drags from its own WndProc/cursor loop.
        }

        public void EndDrag()
        {
            // The Windows backend drives drags from its own WndProc/cursor loop.
        }

        public void MoveTo(int x, int y)
        {
            if (_hwnd == IntPtr.Zero) return;

            NativeMethods.SetWindowPos(
                _hwnd,
                _alwaysOnTop ? NativeMethods.HWND_TOPMOST : NativeMethods.HWND_TOP,
                x, y, 0, 0,
                NativeMethods.SWP_NOSIZE |
                NativeMethods.SWP_NOACTIVATE |
                NativeMethods.SWP_NOOWNERZORDER);
        }

        public bool TryGetWindowRect(out WindowRect rect)
        {
            rect = default(WindowRect);
            NativeMethods.RECT native;
            if (_hwnd == IntPtr.Zero || !NativeMethods.GetWindowRect(_hwnd, out native)) return false;
            rect = ToWindowRect(native);
            return true;
        }

        public bool TryGetClientRect(out WindowRect rect)
        {
            rect = default(WindowRect);
            NativeMethods.RECT native;
            if (_hwnd == IntPtr.Zero || !NativeMethods.GetClientRect(_hwnd, out native)) return false;
            rect = ToWindowRect(native);
            return true;
        }

        public void Dispose()
        {
            if (_hwnd == IntPtr.Zero) return;

            if (_input != null)
            {
                _input.Shutdown();
                _input = null;
            }

            _attachment = null;
            _tracker = null;

            if (_settings.EnableQuitHotkey)
            {
                NativeMethods.UnregisterHotKey(_hwnd, _settings.QuitHotkeyId);
            }

            if (_oldWndProc != IntPtr.Zero)
            {
                NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GWLP_WNDPROC, _oldWndProc);
                _oldWndProc = IntPtr.Zero;
            }

            NativeMethods.SetWindowRgn(_hwnd, IntPtr.Zero, true);
            NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GWL_STYLE, new IntPtr(_originalStyle));
            NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(_originalExStyle));
            NativeMethods.SetWindowPos(
                _hwnd,
                NativeMethods.HWND_NOTOPMOST,
                0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE |
                NativeMethods.SWP_NOSIZE |
                NativeMethods.SWP_FRAMECHANGED |
                NativeMethods.SWP_NOACTIVATE);

            GC.KeepAlive(_wndProc);
            _wndProc = null;
            _initialized = false;
            _hwnd = IntPtr.Zero;
            StateChanged = null;
        }

        private void OnInputClicked()
        {
            Raise(DesktopWindowEventType.Clicked, "clicked", IntPtr.Zero);
        }

        private void OnInputDragStarted()
        {
            if (_attachment != null) _attachment.OnDragStarted();
            Raise(DesktopWindowEventType.DragStarted, "drag started", IntPtr.Zero);
        }

        private void OnInputDragEnded()
        {
            if (_attachment != null) _attachment.OnDragEnded();
            Raise(DesktopWindowEventType.DragEnded, "drag ended", IntPtr.Zero);
        }

        private void OnInputQuitRequested()
        {
            Raise(DesktopWindowEventType.QuitRequested, "quit hotkey", IntPtr.Zero);
        }

        private IntPtr FindOwnWindow()
        {
            uint processId = (uint)Process.GetCurrentProcess().Id;
            IntPtr fallback = IntPtr.Zero;
            long fallbackArea = -1;

            IntPtr hwnd = NativeMethods.GetTopWindow(IntPtr.Zero);
            while (hwnd != IntPtr.Zero)
            {
                uint windowProcessId;
                NativeMethods.GetWindowThreadProcessId(hwnd, out windowProcessId);
                if (windowProcessId == processId && NativeMethods.IsWindowVisible(hwnd))
                {
                    var className = new StringBuilder(256);
                    NativeMethods.GetClassName(hwnd, className, className.Capacity);
                    if (className.ToString() == _settings.WindowClassName)
                    {
                        return hwnd;
                    }

                    NativeMethods.RECT rect;
                    if (NativeMethods.GetWindowRect(hwnd, out rect))
                    {
                        long area = (long)rect.Width * rect.Height;
                        if (area > fallbackArea)
                        {
                            fallbackArea = area;
                            fallback = hwnd;
                        }
                    }
                }

                hwnd = NativeMethods.GetWindow(hwnd, NativeMethods.GW_HWNDNEXT);
            }

            // No risky GetActiveWindow fallback: only a window of our own process.
            return fallback;
        }

        private void ApplyStyles()
        {
            long style = _originalStyle;
            style &= ~(NativeMethods.WS_CAPTION |
                       NativeMethods.WS_THICKFRAME |
                       NativeMethods.WS_MINIMIZEBOX |
                       NativeMethods.WS_MAXIMIZEBOX |
                       NativeMethods.WS_SYSMENU);
            style |= NativeMethods.WS_POPUP | NativeMethods.WS_VISIBLE;
            NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GWL_STYLE, new IntPtr(style));

            long exStyle = _originalExStyle;
            exStyle |= NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW;
            if (_alwaysOnTop) exStyle |= NativeMethods.WS_EX_TOPMOST;
            else exStyle &= ~NativeMethods.WS_EX_TOPMOST;
            exStyle &= ~NativeMethods.WS_EX_APPWINDOW;
            NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(exStyle));

            NativeMethods.SetWindowPos(
                _hwnd,
                _alwaysOnTop ? NativeMethods.HWND_TOPMOST : NativeMethods.HWND_NOTOPMOST,
                0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE |
                NativeMethods.SWP_NOSIZE |
                NativeMethods.SWP_FRAMECHANGED |
                NativeMethods.SWP_NOACTIVATE |
                NativeMethods.SWP_SHOWWINDOW |
                NativeMethods.SWP_NOOWNERZORDER);
        }

        private void ApplyDwm()
        {
            var margins = new NativeMethods.MARGINS
            {
                cxLeftWidth = -1,
                cxRightWidth = -1,
                cyTopHeight = -1,
                cyBottomHeight = -1
            };
            NativeMethods.DwmExtendFrameIntoClientArea(_hwnd, ref margins);

            int corner = NativeMethods.DWMWCP_DONOTROUND;
            NativeMethods.DwmSetWindowAttribute(
                _hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, 4);

            int border = NativeMethods.DWMWA_COLOR_NONE;
            NativeMethods.DwmSetWindowAttribute(
                _hwnd, NativeMethods.DWMWA_BORDER_COLOR, ref border, 4);
        }

        private void StabilizeClientSize()
        {
            int target = (int)Math.Round(_settings.WindowSizeDip * DpiScale);
            NativeMethods.SetWindowPos(
                _hwnd,
                _alwaysOnTop ? NativeMethods.HWND_TOPMOST : NativeMethods.HWND_TOP,
                0, 0, target, target,
                NativeMethods.SWP_NOMOVE |
                NativeMethods.SWP_NOACTIVATE |
                NativeMethods.SWP_FRAMECHANGED |
                NativeMethods.SWP_NOOWNERZORDER);
        }

        private void ApplyRegion()
        {
            NativeMethods.RECT client;
            if (!NativeMethods.GetClientRect(_hwnd, out client)) return;

            int width = client.Width;
            int height = client.Height;
            if (width <= 0 || height <= 0) return;

            int cx = width / 2;
            int cy = height / 2;
            NativeMethods.RECT window;
            if (NativeMethods.GetWindowRect(_hwnd, out window))
            {
                var origin = new NativeMethods.POINT { X = 0, Y = 0 };
                if (NativeMethods.ClientToScreen(_hwnd, ref origin))
                {
                    cx = (origin.X - window.Left) + width / 2;
                    cy = (origin.Y - window.Top) + height / 2;
                }
            }

            int radius = DesktopWindowGeometry.ComputeHitRadius(
                width, height, _settings.HitRadiusRatio);

            IntPtr region = NativeMethods.CreateEllipticRgn(
                cx - radius, cy - radius, cx + radius, cy + radius);
            if (region == IntPtr.Zero)
            {
                LogError("CreateEllipticRgn failed.");
                return;
            }

            // On success the system owns the region; do not DeleteObject it.
            NativeMethods.SetWindowRgn(_hwnd, region, true);
            _regionWidth = width;
            _regionHeight = height;
        }

        private void RemoveRegion()
        {
            NativeMethods.SetWindowRgn(_hwnd, IntPtr.Zero, true);
            _regionWidth = 0;
            _regionHeight = 0;
        }

        private void EnsureRegion()
        {
            if (_hwnd == IntPtr.Zero) return;

            long style = NativeMethods.GetWindowLongPtr(_hwnd, NativeMethods.GWL_STYLE).ToInt64();
            long exStyle = NativeMethods.GetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
            long requiredEx = NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW;
            if (_alwaysOnTop) requiredEx |= NativeMethods.WS_EX_TOPMOST;

            bool styleLost =
                (style & (NativeMethods.WS_CAPTION | NativeMethods.WS_THICKFRAME)) != 0 ||
                (exStyle & requiredEx) != requiredEx;
            if (styleLost)
            {
                ApplyStyles();
                ApplyDwm();
            }

            if (!_clickThrough) return;

            NativeMethods.RECT client;
            if (!NativeMethods.GetClientRect(_hwnd, out client)) return;

            int now = Environment.TickCount;
            bool regionCheck = _lastRegionTick == 0 || now - _lastRegionTick >= 1000;
            if (regionCheck) _lastRegionTick = now;

            if (styleLost ||
                regionCheck ||
                client.Width != _regionWidth ||
                client.Height != _regionHeight)
            {
                ApplyRegion();
            }
        }

        private void SubclassWindow()
        {
            _wndProc = WindowProc;
            IntPtr newProc = Marshal.GetFunctionPointerForDelegate(_wndProc);
            _oldWndProc = NativeMethods.SetWindowLongPtr(
                _hwnd, NativeMethods.GWLP_WNDPROC, newProc);

            if (_oldWndProc == IntPtr.Zero)
            {
                LogError("SetWindowLongPtr(GWLP_WNDPROC) failed: " + Marshal.GetLastWin32Error());
            }
        }

        private void RegisterQuitHotkey()
        {
            if (!_settings.EnableQuitHotkey) return;

            bool ok = NativeMethods.RegisterHotKey(
                _hwnd,
                _settings.QuitHotkeyId,
                _settings.QuitHotkeyModifiers,
                _settings.QuitHotkeyVirtualKey);
            if (!ok)
            {
                LogWarning("RegisterHotKey failed: " + Marshal.GetLastWin32Error());
            }
        }

        private void PositionBottomRight()
        {
            NativeMethods.RECT window;
            if (!NativeMethods.GetWindowRect(_hwnd, out window)) return;

            IntPtr monitor = NativeMethods.MonitorFromWindow(
                _hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
            var info = new NativeMethods.MONITORINFO
            {
                cbSize = Marshal.SizeOf(typeof(NativeMethods.MONITORINFO))
            };
            if (!NativeMethods.GetMonitorInfo(monitor, ref info)) return;

            int margin = (int)Math.Round(_settings.InitialMarginDip * DpiScale);
            int x = info.rcWork.Right - window.Width - margin;
            int y = info.rcWork.Bottom - window.Height - margin;

            NativeMethods.SetWindowPos(
                _hwnd,
                _alwaysOnTop ? NativeMethods.HWND_TOPMOST : NativeMethods.HWND_TOP,
                x, y, 0, 0,
                NativeMethods.SWP_NOSIZE |
                NativeMethods.SWP_NOACTIVATE |
                NativeMethods.SWP_NOOWNERZORDER);
        }

        private void SetWindowPosToTopmost()
        {
            NativeMethods.SetWindowPos(
                _hwnd,
                NativeMethods.HWND_TOPMOST,
                0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE |
                NativeMethods.SWP_NOSIZE |
                NativeMethods.SWP_NOACTIVATE |
                NativeMethods.SWP_NOOWNERZORDER);
        }

        private IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            switch (msg)
            {
                case NativeMethods.WM_MOUSEACTIVATE:
                    return (IntPtr)NativeMethods.MA_NOACTIVATE;

                case NativeMethods.WM_NCHITTEST:
                    // The window region already rejects points outside the pet.
                    return (IntPtr)NativeMethods.HTCLIENT;

                case NativeMethods.WM_LBUTTONDOWN:
                    if (_input != null)
                    {
                        long packed = lParam.ToInt64();
                        int clientX = (short)(packed & 0xFFFF);
                        int clientY = (short)((packed >> 16) & 0xFFFF);
                        _input.HandleLeftDown(new NativeMethods.POINT { X = clientX, Y = clientY });
                    }
                    break;

                case NativeMethods.WM_LBUTTONUP:
                    if (_input != null) _input.HandleLeftUp();
                    break;

                case NativeMethods.WM_MOUSEMOVE:
                    break;

                case NativeMethods.WM_HOTKEY:
                    if (_settings.EnableQuitHotkey &&
                        wParam.ToInt32() == _settings.QuitHotkeyId)
                    {
                        if (_input != null) _input.RequestQuit();
                        return IntPtr.Zero;
                    }
                    break;
            }

            if (_oldWndProc == IntPtr.Zero) return IntPtr.Zero;
            return NativeMethods.CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
        }

        private void OnAttachmentEvent(DesktopWindowEvent windowEvent)
        {
            if (windowEvent == null) return;
            Raise(windowEvent.Type, windowEvent.Message, windowEvent.TargetWindow);
        }

        private void Raise(DesktopWindowEventType type, string message, IntPtr target)
        {
            var handler = StateChanged;
            if (handler == null) return;

            try
            {
                handler(new DesktopWindowEvent(type, message, target));
            }
            catch (Exception exception)
            {
                LogError("StateChanged handler failed: " + exception);
            }
        }

        private void Log(string message)
        {
            if (_settings.Log != null) _settings.Log("[Xiaosu.Platform] " + message);
        }

        private void LogWarning(string message)
        {
            Log("WARN " + message);
        }

        private void LogError(string message)
        {
            Log("ERROR " + message);
        }

        private void LogDiagnostics()
        {
            NativeMethods.RECT window;
            NativeMethods.GetWindowRect(_hwnd, out window);
            long style = NativeMethods.GetWindowLongPtr(_hwnd, NativeMethods.GWL_STYLE).ToInt64();
            long exStyle = NativeMethods.GetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();

            Log(string.Format(
                "hwnd=0x{0:X} awareness={1} dpi={2} rect=({3},{4},{5},{6}) style=0x{7:X} ex=0x{8:X}",
                _hwnd.ToInt64(), _awareness, _dpi,
                window.Left, window.Top, window.Width, window.Height,
                style, exStyle));
        }

        private static WindowRect ToWindowRect(NativeMethods.RECT rect)
        {
            return new WindowRect
            {
                Left = rect.Left,
                Top = rect.Top,
                Right = rect.Right,
                Bottom = rect.Bottom
            };
        }
    }
}
