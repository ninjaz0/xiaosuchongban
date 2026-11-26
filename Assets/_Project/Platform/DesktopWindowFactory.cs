using System.Runtime.InteropServices;

namespace Xiaosu.Platform
{
    public static class DesktopWindowFactory
    {
        /// <summary>
        /// Creates the native overlay for the current OS. Pass allowNative=false in the
        /// Unity Editor so the Editor window is never restyled.
        /// </summary>
        public static IDesktopWindowOverlay Create(bool allowNative)
        {
            if (!allowNative) return new NullDesktopWindow();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new Windows.WindowsDesktopWindow();
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return new Mac.MacDesktopWindow();
            }

            return new NullDesktopWindow();
        }
    }
}
