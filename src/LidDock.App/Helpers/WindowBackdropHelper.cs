using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace LidDock.App.Helpers;

public static class windowBackdropHelper
{
    private const int dwmwaUseImmersiveDarkMode = 20;
    private const int dwmwaWindowCornerPreference = 33;
    private const int dwmwaSystemBackdropType = 38;

    private const int dwmwcpRound = 2;
    private const int dwmsbtMainWindow = 2;
    private const int dwmsbtTransientWindow = 3;

    [DllImport("dwmapi.dll", EntryPoint = "DwmSetWindowAttribute")]
    private static extern int dwmSetWindowAttribute(
        IntPtr hwnd,
        int dwAttribute,
        ref int pvAttribute,
        int cbAttribute);

    public static void applyBackdrop(Window window, bool useAcrylic, bool darkMode)
    {
        var helper = new WindowInteropHelper(window);
        var handle = helper.EnsureHandle();

        var darkVal = darkMode ? 1 : 0;
        dwmSetWindowAttribute(handle, dwmwaUseImmersiveDarkMode, ref darkVal, sizeof(int));

        var cornerVal = dwmwcpRound;
        dwmSetWindowAttribute(handle, dwmwaWindowCornerPreference, ref cornerVal, sizeof(int));

        var backdropVal = useAcrylic ? dwmsbtTransientWindow : dwmsbtMainWindow;
        dwmSetWindowAttribute(handle, dwmwaSystemBackdropType, ref backdropVal, sizeof(int));
    }
}
