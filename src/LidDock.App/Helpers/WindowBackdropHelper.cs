using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace LidDock.App.Helpers;

public static class windowBackdropHelper
{
    private const int dwmwaUseImmersiveDarkMode = 20;
    private const int dwmwaUseImmersiveDarkModeBefore20H1 = 19;
    private const int dwmwaWindowCornerPreference = 33;
    private const int dwmwaSystemBackdropType = 38;

    private const int dwmwcpRound = 2;
    private const int dwmsbtMainWindow = 2;
    private const int dwmsbtTransientWindow = 3;

    private const int wcaAccentPolicy = 19;
    private const int accentEnableBlurbehind = 3;

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

    public static void applyBackdrop(Window window, bool useAcrylic, bool? darkMode = null)
    {
        var helper = new WindowInteropHelper(window);
        var handle = helper.EnsureHandle();

        var isDark = darkMode ?? themeManager.isSystemDarkMode();
        var darkVal = isDark ? 1 : 0;
        if (dwmSetWindowAttribute(handle, dwmwaUseImmersiveDarkMode, ref darkVal, sizeof(int)) != 0)
        {
            dwmSetWindowAttribute(handle, dwmwaUseImmersiveDarkModeBefore20H1, ref darkVal, sizeof(int));
        }

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

        var isWindows11OrGreater = Environment.OSVersion.Version.Build >= 22000;
        if (isWindows11OrGreater)
        {
            var cornerVal = dwmwcpRound;
            dwmSetWindowAttribute(handle, dwmwaWindowCornerPreference, ref cornerVal, sizeof(int));

            var backdropVal = useAcrylic ? dwmsbtTransientWindow : dwmsbtMainWindow;
            dwmSetWindowAttribute(handle, dwmwaSystemBackdropType, ref backdropVal, sizeof(int));
        }
        else
        {
            setWindows10Blur(handle);
        }
    }

    public static void updateWindowDarkMode(Window window, bool darkMode)
    {
        var helper = new WindowInteropHelper(window);
        if (helper.Handle == IntPtr.Zero)
        {
            return;
        }

        var darkVal = darkMode ? 1 : 0;
        if (dwmSetWindowAttribute(helper.Handle, dwmwaUseImmersiveDarkMode, ref darkVal, sizeof(int)) != 0)
        {
            dwmSetWindowAttribute(helper.Handle, dwmwaUseImmersiveDarkModeBefore20H1, ref darkVal, sizeof(int));
        }
    }

    public static void handleSizeMove(IntPtr handle, bool isMoving)
    {
    }

    private static void setWindows10Blur(IntPtr handle)
    {
        var policy = new accentPolicy
        {
            accentState = accentEnableBlurbehind,
            accentFlags = 0,
            gradientColor = 0
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
