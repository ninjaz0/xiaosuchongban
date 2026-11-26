using System;
using System.Runtime.InteropServices;

namespace Xiaosu.Platform.Mac
{
    /// <summary>
    /// macOS backend scaffolding. Requires the native PetMac.bundle built on macOS
    /// (Native~/macOS/build-macos.sh). Window setup/click-through are wired; top-edge
    /// attach/follow is still TODO. This class is UNVERIFIED in this environment.
    /// </summary>
    internal sealed class MacDesktopWindow : IDesktopWindowOverlay
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct PetRect
        {
            public double x;
            public double y;
            public double width;
            public double height;
        }

        [DllImport("PetMac", CallingConvention = CallingConvention.Cdecl)]
        private static extern int petmac_setup();

        [DllImport("PetMac", CallingConvention = CallingConvention.Cdecl)]
        private static extern void petmac_shutdown();

        [DllImport("PetMac", CallingConvention = CallingConvention.Cdecl)]
        private static extern void petmac_set_interactive(int interactive);

        [DllImport("PetMac", CallingConvention = CallingConvention.Cdecl)]
        private static extern void petmac_get_cursor(out double x, out double y);

        [DllImport("PetMac", CallingConvention = CallingConvention.Cdecl)]
        private static extern int petmac_get_own_frame(out PetRect rect);

        [DllImport("PetMac", CallingConvention = CallingConvention.Cdecl)]
        private static extern int petmac_get_work_area(out PetRect rect);

        [DllImport("PetMac", CallingConvention = CallingConvention.Cdecl)]
        private static extern void petmac_set_frame(double x, double y, double width, double height);

        private DesktopWindowSettings _settings = new DesktopWindowSettings();
        private bool _ready;
        private bool _clickThrough = true;

        public event Action<DesktopWindowEvent> StateChanged;

        public bool IsInitialized
        {
            get { return _ready; }
        }

        public bool IsAttached
        {
            get { return false; }
        }

        public void Initialize(DesktopWindowSettings settings)
        {
            _settings = settings ?? new DesktopWindowSettings();
            _settings.Validate();

            try
            {
                if (petmac_setup() == 0)
                {
                    LogError("petmac_setup failed.");
                    Raise(DesktopWindowEventType.Error, "petmac_setup failed", IntPtr.Zero);
                    return;
                }

                PetRect work;
                if (petmac_get_work_area(out work) != 0)
                {
                    double size = _settings.WindowSizeDip;
                    double margin = _settings.InitialMarginDip;
                    double x = work.x + work.width - size - margin;
                    double y = work.y + work.height - size - margin;
                    petmac_set_frame(x, y, size, size);
                }

                _ready = true;
            }
            catch (Exception exception)
            {
                LogError("PetMac load failed: " + exception.Message);
                Raise(DesktopWindowEventType.Error, exception.Message, IntPtr.Zero);
            }
        }

        public void Tick(float deltaTime)
        {
            if (!_ready) return;

            if (!_clickThrough)
            {
                petmac_set_interactive(1);
                return;
            }

            double cursorX;
            double cursorY;
            petmac_get_cursor(out cursorX, out cursorY);

            PetRect own;
            if (petmac_get_own_frame(out own) == 0) return;

            double centerX = own.x + own.width * 0.5;
            double centerY = own.y + own.height * 0.5;
            double radius = own.height * _settings.HitRadiusRatio;
            double dx = cursorX - centerX;
            double dy = cursorY - centerY;
            petmac_set_interactive(dx * dx + dy * dy <= radius * radius ? 1 : 0);
        }

        public void SetAlwaysOnTop(bool enabled)
        {
            // The native plugin sets the floating window level in petmac_setup.
        }

        public void SetClickThrough(bool enabled)
        {
            _clickThrough = enabled;
            if (!_ready) return;
            if (!enabled) petmac_set_interactive(1);
        }

        public void BeginDrag() { }

        public void EndDrag() { }

        public void Dispose()
        {
            _ready = false;
            try
            {
                petmac_shutdown();
            }
            catch (Exception)
            {
                // Plugin may not be present in an unsupported build.
            }
            StateChanged = null;
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
                LogError("StateChanged handler failed: " + exception.Message);
            }
        }

        private void LogError(string message)
        {
            if (_settings.Log != null) _settings.Log("[Xiaosu.Platform.Mac] ERROR " + message);
        }
    }
}
