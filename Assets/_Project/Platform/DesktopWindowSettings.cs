using System;

namespace Xiaosu.Platform
{
    /// <summary>
    /// Platform-independent settings for the desktop pet overlay. Kept in the Platform
    /// assembly (no UnityEngine) so both the native window and tests can use it.
    /// </summary>
    public sealed class DesktopWindowSettings
    {
        /// <summary>Square window size in DIP (96 DPI units).</summary>
        public int WindowSizeDip = 200;

        /// <summary>Initial bottom-right margin in DIP.</summary>
        public int InitialMarginDip = 40;

        /// <summary>
        /// Hit-region radius as a fraction of the client height. The placeholder pet is
        /// 50% of the client height, so 0.25 plus the built-in 2 px tolerance matches it.
        /// </summary>
        public float HitRadiusRatio = 0.25f;

        /// <summary>Top-edge attach tolerance in DIP.</summary>
        public int AttachToleranceDip = 24;

        /// <summary>Click vs drag movement threshold in DIP (Manhattan distance).</summary>
        public float ClickMaxMoveDip = 4f;

        /// <summary>Click vs drag maximum press duration in milliseconds.</summary>
        public int ClickMaxMilliseconds = 300;

        /// <summary>Fall-to-desktop duration in seconds.</summary>
        public float FallDurationSeconds = 0.42f;

        /// <summary>Minimum target window width/height in DIP.</summary>
        public int TargetMinWidthDip = 120;

        public int TargetMinHeightDip = 60;

        public bool EnableQuitHotkey = true;

        public int QuitHotkeyId = 0x5853;

        /// <summary>MOD_CONTROL | MOD_ALT | MOD_NOREPEAT.</summary>
        public uint QuitHotkeyModifiers = 0x4003;

        /// <summary>Virtual key 'Q'.</summary>
        public uint QuitHotkeyVirtualKey = 0x51;

        /// <summary>Unity player top-level window class (Unity 2022/6 on Windows).</summary>
        public string WindowClassName = "UnityWndClass";

        /// <summary>Optional log sink; the Unity host passes Debug.Log.</summary>
        public Action<string> Log;

        public void Validate()
        {
            if (WindowSizeDip < 64) WindowSizeDip = 64;
            if (InitialMarginDip < 0) InitialMarginDip = 0;
            if (HitRadiusRatio < 0.05f) HitRadiusRatio = 0.05f;
            if (HitRadiusRatio > 0.5f) HitRadiusRatio = 0.5f;
            if (AttachToleranceDip < 0) AttachToleranceDip = 0;
            if (ClickMaxMoveDip < 1f) ClickMaxMoveDip = 1f;
            if (ClickMaxMilliseconds < 50) ClickMaxMilliseconds = 50;
            if (FallDurationSeconds < 0.1f) FallDurationSeconds = 0.1f;
            if (FallDurationSeconds > 2f) FallDurationSeconds = 2f;
            if (TargetMinWidthDip < 1) TargetMinWidthDip = 1;
            if (TargetMinHeightDip < 1) TargetMinHeightDip = 1;
            if (string.IsNullOrEmpty(WindowClassName)) WindowClassName = "UnityWndClass";
        }
    }
}
