using System;

namespace Xiaosu.Platform
{
    /// <summary>Editor/unsupported-platform no-op overlay.</summary>
    internal sealed class NullDesktopWindow : IDesktopWindowOverlay
    {
        private bool _initialized;

        public bool IsInitialized { get { return _initialized; } }

        public bool IsAttached { get { return false; } }

        public event Action<DesktopWindowEvent> StateChanged;

        public void Initialize(DesktopWindowSettings settings)
        {
            _initialized = true;
            if (settings != null && settings.Log != null)
            {
                settings.Log("[Xiaosu.Platform] NullDesktopWindow: native overlay disabled in this context.");
            }
        }

        public void Tick(float deltaTime) { }

        public void SetAlwaysOnTop(bool enabled) { }

        public void SetClickThrough(bool enabled) { }

        public void BeginDrag() { }

        public void EndDrag() { }

        public void Dispose()
        {
            _initialized = false;
            StateChanged = null;
        }
    }
}
