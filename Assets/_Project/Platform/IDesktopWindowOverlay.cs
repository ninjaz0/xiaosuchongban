using System;

namespace Xiaosu.Platform
{
    /// <summary>
    /// Optional extension of <see cref="IDesktopWindow"/> for a desktop pet overlay:
    /// window styling, hit-region click-through, raw pointer drag, real-window top-edge
    /// attachment/follow, and lifecycle/settings. Implementations live in the Platform
    /// assembly and must not reference UnityEngine.
    /// </summary>
    public interface IDesktopWindowOverlay : IDesktopWindow
    {
        bool IsInitialized { get; }

        bool IsAttached { get; }

        void Initialize(DesktopWindowSettings settings);

        void Tick(float deltaTime);

        /// <summary>
        /// Starts/ends a drag initiated by the host instead of the native pointer loop.
        /// The Windows implementation also handles raw mouse input internally.
        /// </summary>
        void BeginDrag();

        void EndDrag();

        event Action<DesktopWindowEvent> StateChanged;
    }
}
