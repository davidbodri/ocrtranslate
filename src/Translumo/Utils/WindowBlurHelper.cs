using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Translumo.Utils
{
    internal enum AccentState
    {
        ACCENT_DISABLED = 0,
        ACCENT_ENABLE_GRADIENT = 1,
        ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
        ACCENT_ENABLE_BLURBEHIND = 3,
        ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
        ACCENT_ENABLE_HOSTBACKDROP = 5
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct AccentPolicy
    {
        public AccentState AccentState;
        public int AccentFlags;
        public uint GradientColor;
        public int AnimationId;
    }

    internal enum WindowCompositionAttribute
    {
        WCA_ACCENT_POLICY = 19
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WindowCompositionAttributeData
    {
        public WindowCompositionAttribute Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    public static class WindowBlurHelper
    {
        [DllImport("user32.dll")]
        private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        public static bool Enable(Window window, Color tint, float opacity, bool performanceBlur = true)
        {
            try
            {
                var handle = new WindowInteropHelper(window).Handle;
                if (handle == IntPtr.Zero)
                {
                    return false;
                }

                if (opacity < 0.0f) opacity = 0.0f;
                if (opacity > 1.0f) opacity = 1.0f;
                byte a = (byte)(opacity * 255);

                // Tint color packed as AABBGGRR:
                // byte 3 = Alpha, byte 2 = Blue, byte 1 = Green, byte 0 = Red
                uint gradientColor = (uint)((a << 24) | (tint.B << 16) | (tint.G << 8) | tint.R);

                var accent = new AccentPolicy
                {
                    AccentState = performanceBlur ? AccentState.ACCENT_ENABLE_BLURBEHIND : AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND,
                    AccentFlags = 2,
                    GradientColor = gradientColor,
                    AnimationId = 0
                };

                return SetAccentPolicy(handle, accent);
            }
            catch
            {
                // Fall back safely on unsupported Windows versions
                return false;
            }
        }

        public static bool Disable(Window window)
        {
            try
            {
                var handle = new WindowInteropHelper(window).Handle;
                if (handle == IntPtr.Zero)
                {
                    return false;
                }

                var accent = new AccentPolicy
                {
                    AccentState = AccentState.ACCENT_DISABLED,
                    AccentFlags = 0,
                    GradientColor = 0,
                    AnimationId = 0
                };

                return SetAccentPolicy(handle, accent);
            }
            catch
            {
                // Fall back safely
                return false;
            }
        }

        private static bool SetAccentPolicy(IntPtr hwnd, AccentPolicy policy)
        {
            int sizeOfPolicy = Marshal.SizeOf(policy);
            IntPtr policyPtr = Marshal.AllocHGlobal(sizeOfPolicy);
            try
            {
                Marshal.StructureToPtr(policy, policyPtr, false);
                var data = new WindowCompositionAttributeData
                {
                    Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                    Data = policyPtr,
                    SizeOfData = sizeOfPolicy
                };
                int result = SetWindowCompositionAttribute(hwnd, ref data);
                return result != 0;
            }
            finally
            {
                Marshal.FreeHGlobal(policyPtr);
            }
        }
    }
}
