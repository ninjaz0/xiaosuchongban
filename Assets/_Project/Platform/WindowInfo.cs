using System;

namespace Xiaosu.Platform
{
    public struct WindowPoint
    {
        public WindowPoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X;
        public int Y;
    }

    public struct WindowRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width { get { return Right - Left; } }

        public int Height { get { return Bottom - Top; } }

        public bool Contains(WindowPoint point)
        {
            return point.X >= Left && point.X < Right &&
                   point.Y >= Top && point.Y < Bottom;
        }

        public override string ToString()
        {
            return string.Format("({0},{1})-({2},{3})", Left, Top, Right, Bottom);
        }
    }

    /// <summary>A validated top-level application window.</summary>
    public struct WindowInfo
    {
        public IntPtr Hwnd;
        public uint ProcessId;
        public string ClassName;
        public WindowRect Rect;
        public bool IsVisible;
        public bool IsMinimized;
        public bool IsCloaked;
    }
}
