using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using LidDock.Windows.Native;

namespace LidDock.Windows.Watchers;

public class nativeMessageWindow : IDisposable
{
    private HwndSource? hwndSource;
    private IntPtr lidNotificationHandle;
    private IntPtr powerNotificationHandle;
    private uint taskbarCreatedMessage;

    public event Action? onDisplayChangedMessage;
    public event Action<bool>? onLidStateChangedMessage;
    public event Action? onPowerSourceChangedMessage;
    public event Action? onTaskbarCreatedMessage;

    public void initialize()
    {
        var parameters = new HwndSourceParameters("LidDockNativeListener")
        {
            WindowStyle = 0,
            ExtendedWindowStyle = 0,
            ParentWindow = new IntPtr(-3)
        };

        hwndSource = new HwndSource(parameters);
        hwndSource.AddHook(wndProc);

        var lidGuid = nativeConstants.guidLidSwitchStateChange;
        lidNotificationHandle = nativeMethods.registerPowerSettingNotification(
            hwndSource.Handle,
            ref lidGuid,
            nativeConstants.deviceNotifyWindowHandle);

        var powerGuid = nativeConstants.guidAcdcPowerSource;
        powerNotificationHandle = nativeMethods.registerPowerSettingNotification(
            hwndSource.Handle,
            ref powerGuid,
            nativeConstants.deviceNotifyWindowHandle);

        taskbarCreatedMessage = nativeMethods.registerWindowMessage("TaskbarCreated");
    }

    private IntPtr wndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == nativeConstants.wmDisplayChange)
        {
            onDisplayChangedMessage?.Invoke();
            handled = true;
            return IntPtr.Zero;
        }

        if (taskbarCreatedMessage != 0 && (uint)msg == taskbarCreatedMessage)
        {
            onTaskbarCreatedMessage?.Invoke();
            handled = true;
            return IntPtr.Zero;
        }

        if (msg == nativeConstants.wmPowerBroadcast && wParam.ToInt32() == nativeConstants.pbtPowerSettingChange)
        {
            var setting = Marshal.PtrToStructure<powerBroadcastSetting>(lParam);
            if (setting.powerSetting == nativeConstants.guidLidSwitchStateChange)
            {
                var isLidOpen = setting.data != 0;
                onLidStateChangedMessage?.Invoke(isLidOpen);
                handled = true;
            }
            else if (setting.powerSetting == nativeConstants.guidAcdcPowerSource)
            {
                onPowerSourceChangedMessage?.Invoke();
                handled = true;
            }
        }

        return IntPtr.Zero;
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

        if (hwndSource != null)
        {
            hwndSource.RemoveHook(wndProc);
            hwndSource.Dispose();
            hwndSource = null;
        }
    }
}
