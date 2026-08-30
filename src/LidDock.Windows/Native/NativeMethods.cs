using System;
using System.Runtime.InteropServices;

namespace LidDock.Windows.Native;

public static class nativeMethods
{
    [DllImport("user32.dll", EntryPoint = "GetDisplayConfigBufferSizes")]
    public static extern int getDisplayConfigBufferSizes(
        int flags,
        out uint numPathArrayElements,
        out uint numModeInfoArrayElements);

    [DllImport("user32.dll", EntryPoint = "QueryDisplayConfig")]
    public static extern int queryDisplayConfig(
        int flags,
        ref uint numPathArrayElements,
        [Out] displayConfigPathInfo[] pathInfoArray,
        ref uint numModeInfoArrayElements,
        [Out] displayConfigModeInfo[] modeInfoArray,
        IntPtr currentTopologyId);

    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")]
    public static extern int displayConfigGetDeviceInfo(
        ref displayConfigTargetDeviceName deviceInformation);

    [DllImport("user32.dll", EntryPoint = "RegisterPowerSettingNotification", SetLastError = true)]
    public static extern IntPtr registerPowerSettingNotification(
        IntPtr hRecipient,
        ref Guid powerSettingGuid,
        int flags);

    [DllImport("user32.dll", EntryPoint = "UnregisterPowerSettingNotification", SetLastError = true)]
    public static extern bool unregisterPowerSettingNotification(
        IntPtr handle);

    [DllImport("user32.dll", EntryPoint = "RegisterWindowMessageW", CharSet = CharSet.Unicode)]
    public static extern uint registerWindowMessage(
        string lpString);

    [DllImport("user32.dll", EntryPoint = "MessageBoxW", CharSet = CharSet.Unicode)]
    public static extern int messageBox(
        IntPtr hWnd,
        string text,
        string caption,
        uint type);

    [DllImport("kernel32.dll", EntryPoint = "GetSystemPowerStatus")]
    public static extern bool getSystemPowerStatus(
        out systemPowerStatus systemPowerStatus);

    [DllImport("powrprof.dll", EntryPoint = "PowerGetActiveScheme")]
    public static extern uint powerGetActiveScheme(
        IntPtr userRootPowerKey,
        out IntPtr activePolicyGuid);

    [DllImport("powrprof.dll", EntryPoint = "PowerReadACValueIndex")]
    public static extern uint powerReadAcValueIndex(
        IntPtr rootPowerKey,
        ref Guid schemeGuid,
        ref Guid subGroupOfPowerSettingsGuid,
        ref Guid powerSettingGuid,
        out uint acValueIndex);

    [DllImport("powrprof.dll", EntryPoint = "PowerReadDCValueIndex")]
    public static extern uint powerReadDcValueIndex(
        IntPtr rootPowerKey,
        ref Guid schemeGuid,
        ref Guid subGroupOfPowerSettingsGuid,
        ref Guid powerSettingGuid,
        out uint dcValueIndex);

    [DllImport("powrprof.dll", EntryPoint = "PowerWriteACValueIndex")]
    public static extern uint powerWriteAcValueIndex(
        IntPtr rootPowerKey,
        ref Guid schemeGuid,
        ref Guid subGroupOfPowerSettingsGuid,
        ref Guid powerSettingGuid,
        uint acValueIndex);

    [DllImport("powrprof.dll", EntryPoint = "PowerWriteDCValueIndex")]
    public static extern uint powerWriteDcValueIndex(
        IntPtr rootPowerKey,
        ref Guid schemeGuid,
        ref Guid subGroupOfPowerSettingsGuid,
        ref Guid powerSettingGuid,
        uint dcValueIndex);

    [DllImport("powrprof.dll", EntryPoint = "PowerSetActiveScheme")]
    public static extern uint powerSetActiveScheme(
        IntPtr userRootPowerKey,
        ref Guid schemeGuid);

    [DllImport("powrprof.dll", EntryPoint = "SetSuspendState")]
    public static extern bool setSuspendState(
        bool hibernate,
        bool forceCritical,
        bool disableWakeEvent);

    [DllImport("kernel32.dll", EntryPoint = "LocalFree")]
    public static extern IntPtr localFree(
        IntPtr hMem);

    [DllImport("kernel32.dll", EntryPoint = "SetProcessWorkingSetSize")]
    public static extern bool setProcessWorkingSetSize(
        IntPtr process,
        IntPtr minimumWorkingSetSize,
        IntPtr maximumWorkingSetSize);

    [DllImport("kernel32.dll", EntryPoint = "GetCurrentProcess")]
    public static extern IntPtr getCurrentProcess();

    [DllImport("psapi.dll", EntryPoint = "EmptyWorkingSet")]
    public static extern bool emptyWorkingSet(
        IntPtr hProcess);

    [DllImport("user32.dll", EntryPoint = "GetMessageW")]
    public static extern int getMessage(
        out nativeMsg lpMsg,
        IntPtr hWnd,
        uint wMsgFilterMin,
        uint wMsgFilterMax);

    [DllImport("user32.dll", EntryPoint = "TranslateMessage")]
    public static extern bool translateMessage(
        ref nativeMsg lpMsg);

    [DllImport("user32.dll", EntryPoint = "DispatchMessageW")]
    public static extern IntPtr dispatchMessage(
        ref nativeMsg lpMsg);

    [DllImport("user32.dll", EntryPoint = "PostQuitMessage")]
    public static extern void postQuitMessage(
        int nExitCode);

    [DllImport("user32.dll", EntryPoint = "SetForegroundWindow")]
    public static extern bool setForegroundWindow(
        IntPtr hWnd);

    [DllImport("user32.dll", EntryPoint = "ShowWindow")]
    public static extern bool showWindow(
        IntPtr hWnd,
        int nCmdShow);

    [DllImport("user32.dll", EntryPoint = "PostMessageW")]
    public static extern bool postMessage(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "FindWindowW", CharSet = CharSet.Unicode)]
    public static extern IntPtr findWindow(
        string? lpClassName,
        string? lpWindowName);
}

[StructLayout(LayoutKind.Sequential)]
public struct nativeMsg
{
    public IntPtr hwnd;
    public uint message;
    public IntPtr wParam;
    public IntPtr lParam;
    public uint time;
    public int ptX;
    public int ptY;
    public uint lPrivate;
}
