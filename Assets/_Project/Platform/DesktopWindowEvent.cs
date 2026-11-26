using System;

namespace Xiaosu.Platform
{
    public enum DesktopWindowEventType
    {
        Clicked,
        DragStarted,
        DragEnded,
        Attached,
        Detached,
        FallStarted,
        FallCompleted,
        QuitRequested,
        Error
    }

    /// <summary>Event raised by <see cref="IDesktopWindowOverlay"/>.</summary>
    public sealed class DesktopWindowEvent
    {
        public DesktopWindowEvent(DesktopWindowEventType type, string message)
            : this(type, message, IntPtr.Zero)
        {
        }

        public DesktopWindowEvent(DesktopWindowEventType type, string message, IntPtr targetWindow)
        {
            Type = type;
            Message = message ?? string.Empty;
            TargetWindow = targetWindow;
        }

        public DesktopWindowEventType Type { get; private set; }

        public string Message { get; private set; }

        public IntPtr TargetWindow { get; private set; }
    }
}
