using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Xiaosu.Platform.Windows;

namespace Xiaosu.Platform
{
    /// <summary>
    /// Pure C# DPI helper so the Unity host can size the window in physical pixels
    /// before the backend applies the final DIP size. No UnityEngine reference.
    /// </summary>
    public static class DesktopWindowMetrics
    {
        public static bool IsWindows
        {
            get { return RuntimeInformation.IsOSPlatform(OSPlatform.Windows); }
        }

        /// <summary>DPI of the current process's largest visible top-level window, or 96.</summary>
        public static int GetCurrentWindowDpi()
        {
            if (!IsWindows) return 96;

            try
            {
                uint processId = (uint)Process.GetCurrentProcess().Id;
                IntPtr best = IntPtr.Zero;
                long bestArea = -1;
                IntPtr hwnd = NativeMethods.GetTopWindow(IntPtr.Zero);

                while (hwnd != IntPtr.Zero)
                {
                    uint windowProcessId;
                    NativeMethods.GetWindowThreadProcessId(hwnd, out windowProcessId);
                    if (windowProcessId == processId && NativeMethods.IsWindowVisible(hwnd))
                    {
                        NativeMethods.RECT rect;
                        if (NativeMethods.GetWindowRect(hwnd, out rect))
                        {
                            long area = (long)rect.Width * rect.Height;
                            var className = new StringBuilder(256);
                            NativeMethods.GetClassName(hwnd, className, className.Capacity);
                            bool isUnity = className.ToString() == "UnityWndClass";
                            if (best == IntPtr.Zero ||
                                (isUnity && area >= 0) ||
                                area > bestArea)
                            {
                                best = hwnd;
                                bestArea = area;
                                if (isUnity && bestArea > 0) break;
                            }
                        }
                    }

                    hwnd = NativeMethods.GetWindow(hwnd, NativeMethods.GW_HWNDNEXT);
                }

                if (best != IntPtr.Zero)
                {
                    uint dpi = NativeMethods.GetDpiForWindow(best);
                    if (dpi > 0) return (int)dpi;
                }
            }
            catch (Exception)
            {
                // Fall through to 96 if the platform APIs are unavailable.
            }

            return 96;
        }

        public static int DipToPixels(int dip)
        {
            int dpi = GetCurrentWindowDpi();
            return (int)Math.Round(dip * (dpi / 96.0));
        }
    }
}
