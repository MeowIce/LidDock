using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace LidDock.App.Helpers;

public static class windowBackdropHelper
{
    private const int dwmwaUseImmersiveDarkMode = 20;
    private const int dwmwaWindowCornerPreference = 33;
    private const int dwmwaSystemBackdropType = 38;

    private const int dwmwcpRound = 2;
    private const int dwmsbtMainWindow = 2;
    private const int dwmsbtTransientWindow = 3;

    private const int wcaAccentPolicy = 19;
    private const int accentEnableAcrylicBlurbehind = 4;

    [StructLayout(LayoutKind.Sequential)]
    private struct margins
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct accentPolicy
    {
        public int accentState;
        public int accentFlags;
        public int gradientColor;
        public int animationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct windowCompositionAttributeData
    {
        public int attribute;
        public IntPtr data;
        public int sizeOfData;
    }

    [DllImport("dwmapi.dll", EntryPoint = "DwmSetWindowAttribute")]
    private static extern int dwmSetWindowAttribute(
        IntPtr hwnd,
        int dwAttribute,
        ref int pvAttribute,
        int cbAttribute);

    [DllImport("dwmapi.dll", EntryPoint = "DwmExtendFrameIntoClientArea")]
    private static extern int dwmExtendFrameIntoClientArea(
        IntPtr hWnd,
        ref margins pMarInset);

    [DllImport("user32.dll", EntryPoint = "SetWindowCompositionAttribute")]
    private static extern int setWindowCompositionAttribute(
        IntPtr hwnd,
        ref windowCompositionAttributeData data);

    public static void applyBackdrop(Window window, bool useAcrylic, bool darkMode)
    {
        var helper = new WindowInteropHelper(window);
        var handle = helper.EnsureHandle();

        var source = HwndSource.FromHwnd(handle);
        if (source?.CompositionTarget != null)
        {
            source.CompositionTarget.BackgroundColor = Colors.Transparent;
        }

        var margins = new margins
        {
            cxLeftWidth = -1,
            cxRightWidth = -1,
            cyTopHeight = -1,
            cyBottomHeight = -1
        };
        dwmExtendFrameIntoClientArea(handle, ref margins);

        var darkVal = darkMode ? 1 : 0;
        dwmSetWindowAttribute(handle, dwmwaUseImmersiveDarkMode, ref darkVal, sizeof(int));

        var cornerVal = dwmwcpRound;
        dwmSetWindowAttribute(handle, dwmwaWindowCornerPreference, ref cornerVal, sizeof(int));

        var backdropVal = useAcrylic ? dwmsbtTransientWindow : dwmsbtMainWindow;
        var hr = dwmSetWindowAttribute(handle, dwmwaSystemBackdropType, ref backdropVal, sizeof(int));

        if (hr != 0)
        {
            applyAcrylicFallback(handle);
        }
    }

    private static void applyAcrylicFallback(IntPtr handle)
    {
        var policy = new accentPolicy
        {
            accentState = accentEnableAcrylicBlurbehind,
            accentFlags = 2,
            gradientColor = unchecked((int)0x99202020)
        };

        var size = Marshal.SizeOf<accentPolicy>();
        var ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(policy, ptr, false);
            var data = new windowCompositionAttributeData
            {
                attribute = wcaAccentPolicy,
                data = ptr,
                sizeOfData = size
            };
            setWindowCompositionAttribute(handle, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }
}
