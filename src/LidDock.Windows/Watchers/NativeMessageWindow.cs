using System;
using System.Runtime.InteropServices;
using LidDock.Windows.Native;

namespace LidDock.Windows.Watchers;

public class nativeMessageWindow : IDisposable
{
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr wndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct wndClassEx
    {
        public uint cbSize;
        public uint style;
        public wndProcDelegate lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [DllImport("user32.dll", EntryPoint = "RegisterClassExW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort registerClassEx(ref wndClassEx lpwcx);

    [DllImport("user32.dll", EntryPoint = "UnregisterClassW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool unregisterClass(string lpClassName, IntPtr hInstance);

    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr createWindowEx(
        uint dwExStyle,
        string lpClassName,
        string lpWindowName,
        uint dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll", EntryPoint = "DestroyWindow", SetLastError = true)]
    private static extern bool destroyWindow(IntPtr hWnd);

    [DllImport("user32.dll", EntryPoint = "DefWindowProcW")]
    private static extern IntPtr defWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode)]
    private static extern IntPtr getModuleHandle(string? lpModuleName);

    private const string windowClassName = "LidDockNativeMessageListenerClass";
    private const uint csDblClks = 0x0008;
    private readonly IntPtr hwndMessageParent = new IntPtr(-3);

    private IntPtr windowHandle = IntPtr.Zero;
    private IntPtr moduleHandle = IntPtr.Zero;
    private static wndProcDelegate? cachedWndProc;
    private IntPtr lidNotificationHandle = IntPtr.Zero;
    private IntPtr powerNotificationHandle = IntPtr.Zero;
    private IntPtr batteryNotificationHandle = IntPtr.Zero;
    private uint taskbarCreatedMessage;
    private uint settingsChangedMessage;

    public IntPtr handle => windowHandle;
    public event Action? onDisplayChangedMessage;
    public event Action<bool>? onLidStateChangedMessage;
    public event Action? onPowerSourceChangedMessage;
    public event Action? onTaskbarCreatedMessage;
    public event Action? onSettingsChangedMessage;
    public event Action<int, IntPtr, IntPtr>? onGenericMessage;

    public void initialize()
    {
        moduleHandle = getModuleHandle(null);
        cachedWndProc = wndProc;

        var wcx = new wndClassEx
        {
            cbSize = (uint)Marshal.SizeOf<wndClassEx>(),
            style = csDblClks,
            lpfnWndProc = cachedWndProc,
            cbClsExtra = 0,
            cbWndExtra = 0,
            hInstance = moduleHandle,
            hIcon = IntPtr.Zero,
            hCursor = IntPtr.Zero,
            hbrBackground = IntPtr.Zero,
            lpszMenuName = null,
            lpszClassName = windowClassName,
            hIconSm = IntPtr.Zero
        };

        registerClassEx(ref wcx);

        windowHandle = createWindowEx(
            0,
            windowClassName,
            "LidDockNativeListener",
            0,
            0,
            0,
            0,
            0,
            hwndMessageParent,
            IntPtr.Zero,
            moduleHandle,
            IntPtr.Zero);

        if (windowHandle != IntPtr.Zero)
        {
            var lidGuid = nativeConstants.guidLidSwitchStateChange;
            lidNotificationHandle = nativeMethods.registerPowerSettingNotification(
                windowHandle,
                ref lidGuid,
                nativeConstants.deviceNotifyWindowHandle);

            var powerGuid = nativeConstants.guidAcdcPowerSource;
            powerNotificationHandle = nativeMethods.registerPowerSettingNotification(
                windowHandle,
                ref powerGuid,
                nativeConstants.deviceNotifyWindowHandle);

            var batteryGuid = nativeConstants.guidBatteryPercentageRemaining;
            batteryNotificationHandle = nativeMethods.registerPowerSettingNotification(
                windowHandle,
                ref batteryGuid,
                nativeConstants.deviceNotifyWindowHandle);

            taskbarCreatedMessage = nativeMethods.registerWindowMessage("TaskbarCreated");
            settingsChangedMessage = nativeMethods.registerWindowMessage("LidDock_SettingsChanged_Event");
        }
    }

    private IntPtr wndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            onGenericMessage?.Invoke((int)msg, wParam, lParam);

            if (settingsChangedMessage != 0 && msg == settingsChangedMessage)
            {
                onSettingsChangedMessage?.Invoke();
                return IntPtr.Zero;
            }

            if (msg == nativeConstants.wmDisplayChange)
            {
                onDisplayChangedMessage?.Invoke();
                return IntPtr.Zero;
            }

            if (taskbarCreatedMessage != 0 && msg == taskbarCreatedMessage)
            {
                onTaskbarCreatedMessage?.Invoke();
                return IntPtr.Zero;
            }

            if (msg == nativeConstants.wmPowerBroadcast)
            {
                var wParamVal = wParam.ToInt64();
                if (wParamVal == nativeConstants.pbtApmPowerStatusChange)
                {
                    onPowerSourceChangedMessage?.Invoke();
                    return IntPtr.Zero;
                }

                if (wParamVal == nativeConstants.pbtPowerSettingChange && lParam != IntPtr.Zero)
                {
                    var setting = Marshal.PtrToStructure<powerBroadcastSetting>(lParam);
                    if (setting.powerSetting == nativeConstants.guidLidSwitchStateChange)
                    {
                        var isLidOpen = (setting.dataLength == 1)
                            ? Marshal.ReadByte(lParam, 20) != 0
                            : Marshal.ReadInt32(lParam, 20) != 0;
                        onLidStateChangedMessage?.Invoke(isLidOpen);
                        return IntPtr.Zero;
                    }
                    else if (setting.powerSetting == nativeConstants.guidAcdcPowerSource ||
                             setting.powerSetting == nativeConstants.guidBatteryPercentageRemaining)
                    {
                        onPowerSourceChangedMessage?.Invoke();
                        return IntPtr.Zero;
                    }
                }
            }
        }
        catch
        {
        }

        return defWindowProc(hwnd, msg, wParam, lParam);
    }

    void IDisposable.Dispose() => dispose();

    public void dispose()
    {
        if (lidNotificationHandle != IntPtr.Zero)
        {
            nativeMethods.unregisterPowerSettingNotification(lidNotificationHandle);
            lidNotificationHandle = IntPtr.Zero;
        }

        if (powerNotificationHandle != IntPtr.Zero)
        {
            nativeMethods.unregisterPowerSettingNotification(powerNotificationHandle);
            powerNotificationHandle = IntPtr.Zero;
        }

        if (batteryNotificationHandle != IntPtr.Zero)
        {
            nativeMethods.unregisterPowerSettingNotification(batteryNotificationHandle);
            batteryNotificationHandle = IntPtr.Zero;
        }

        if (windowHandle != IntPtr.Zero)
        {
            destroyWindow(windowHandle);
            windowHandle = IntPtr.Zero;
        }

        if (moduleHandle != IntPtr.Zero)
        {
            unregisterClass(windowClassName, moduleHandle);
            moduleHandle = IntPtr.Zero;
        }
    }
}
