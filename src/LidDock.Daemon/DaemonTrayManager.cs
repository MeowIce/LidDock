using System;
using System.Runtime.InteropServices;
using LidDock.Core.Models;
using LidDock.Core.StateMachine;

namespace LidDock.Daemon;

public class daemonTrayManager : IDisposable
{
    public const int wmLbuttonUp = 0x0202;
    public const int wmRbuttonUp = 0x0205;
    public const int wmLbuttonDblClk = 0x0203;
    public const int wmTrayCallback = 0x0800 + 10;

    private const int nimAdd = 0x00000000;
    private const int nimModify = 0x00000001;
    private const int nimDelete = 0x00000002;

    private const uint nifMessage = 0x00000001;
    private const uint nifIcon = 0x00000002;
    private const uint nifTip = 0x00000004;
    private const uint nifInfo = 0x00000010;
    private const uint niifInfo = 0x00000001;
    private const uint niifWarning = 0x00000002;
    private const uint niifLargeIcon = 0x00000020;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct notifyIconData
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct iconInfo
    {
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct rect
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW")]
    private static extern bool shellNotifyIcon(int dwMessage, ref notifyIconData lpData);

    [DllImport("user32.dll", EntryPoint = "CreateIconIndirect")]
    private static extern IntPtr createIconIndirect(ref iconInfo pIconInfo);

    [DllImport("user32.dll", EntryPoint = "DestroyIcon")]
    private static extern bool destroyIcon(IntPtr hIcon);

    [DllImport("user32.dll", EntryPoint = "GetDC")]
    private static extern IntPtr getDc(IntPtr hWnd);

    [DllImport("user32.dll", EntryPoint = "ReleaseDC")]
    private static extern int releaseDc(IntPtr hWnd, IntPtr hDc);

    [DllImport("gdi32.dll", EntryPoint = "CreateCompatibleDC")]
    private static extern IntPtr createCompatibleDc(IntPtr hDc);

    [DllImport("gdi32.dll", EntryPoint = "DeleteDC")]
    private static extern bool deleteDc(IntPtr hDc);

    [DllImport("gdi32.dll", EntryPoint = "CreateCompatibleBitmap")]
    private static extern IntPtr createCompatibleBitmap(IntPtr hDc, int cx, int cy);

    [DllImport("gdi32.dll", EntryPoint = "SelectObject")]
    private static extern IntPtr selectObject(IntPtr hDc, IntPtr h);

    [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
    private static extern bool deleteObject(IntPtr ho);

    [DllImport("gdi32.dll", EntryPoint = "CreateSolidBrush")]
    private static extern IntPtr createSolidBrush(uint color);

    [DllImport("user32.dll", EntryPoint = "FillRect")]
    private static extern int fillRect(IntPtr hDc, ref rect lprc, IntPtr hbr);

    private IntPtr windowHandle;
    private IntPtr currentIconHandle;
    private bool isIconAdded;
    private clamshellState lastState = clamshellState.normalMode;
    private string lastMonitor = "None";
    private lidState lastLid = lidState.open;
    private systemPowerInfo lastPower = new systemPowerInfo(powerSourceType.unknown, 100, false);

    public void initialize(IntPtr hWnd)
    {
        windowHandle = hWnd;
        currentIconHandle = generateStateIcon(clamshellState.normalMode);

        var data = new notifyIconData
        {
            cbSize = (uint)Marshal.SizeOf<notifyIconData>(),
            hWnd = windowHandle,
            uID = 1,
            uFlags = nifMessage | nifIcon | nifTip,
            uCallbackMessage = wmTrayCallback,
            hIcon = currentIconHandle,
            szTip = "LidDock: Standard (Undocked)"
        };

        isIconAdded = shellNotifyIcon(nimAdd, ref data);
    }

    public void updateStatus(
        clamshellState state,
        string externalMonitorName,
        lidState lid,
        systemPowerInfo power)
    {
        lastState = state;
        lastMonitor = externalMonitorName;
        lastLid = lid;
        lastPower = power;

        if (!isIconAdded)
        {
            return;
        }

        var oldIcon = currentIconHandle;
        currentIconHandle = generateStateIcon(state);

        var stateStr = displayFormatters.formatClamshellState(state);
        var lidStr = displayFormatters.formatLidState(lid);
        var powerStr = displayFormatters.formatPowerSource(power);
        var tip = $"LidDock: {stateStr}\nLid: {lidStr} | Display: {externalMonitorName}\nPower: {powerStr}";
        if (tip.Length > 127)
        {
            tip = tip.Substring(0, 127);
        }

        var data = new notifyIconData
        {
            cbSize = (uint)Marshal.SizeOf<notifyIconData>(),
            hWnd = windowHandle,
            uID = 1,
            uFlags = nifIcon | nifTip,
            hIcon = currentIconHandle,
            szTip = tip
        };

        shellNotifyIcon(nimModify, ref data);

        if (oldIcon != IntPtr.Zero)
        {
            destroyIcon(oldIcon);
        }
    }

    public void handleWindowMessage(
        int msg,
        IntPtr wParam,
        IntPtr lParam,
        appSettings settings,
        clamshellStateMachine stateMachine,
        Action onExitRequested)
    {
        if (msg != wmTrayCallback)
        {
            return;
        }

        var eventType = lParam.ToInt32();
        if (eventType == wmRbuttonUp)
        {
            nativeTrayMenu.showContextMenu(
                windowHandle,
                settings,
                lastState,
                lastMonitor,
                lastLid,
                lastPower,
                stateMachine,
                onExitRequested);
        }
        else if (eventType == wmLbuttonDblClk)
        {
            nativeTrayMenu.launchUi(string.Empty);
        }
    }

    public void showToastNotification(string title, string message, bool isWarning = true)
    {
        if (!isIconAdded || windowHandle == IntPtr.Zero)
        {
            return;
        }

        var safeTitle = title.Length > 63 ? title.Substring(0, 63) : title;
        var safeMessage = message.Length > 255 ? message.Substring(0, 255) : message;

        var data = new notifyIconData
        {
            cbSize = (uint)Marshal.SizeOf<notifyIconData>(),
            hWnd = windowHandle,
            uID = 1,
            uFlags = nifInfo,
            szInfoTitle = safeTitle,
            szInfo = safeMessage,
            dwInfoFlags = isWarning ? (niifWarning | niifLargeIcon) : (niifInfo | niifLargeIcon)
        };

        shellNotifyIcon(nimModify, ref data);
    }

    public void recreateIcon()
    {
        if (windowHandle == IntPtr.Zero)
        {
            return;
        }

        var data = new notifyIconData
        {
            cbSize = (uint)Marshal.SizeOf<notifyIconData>(),
            hWnd = windowHandle,
            uID = 1,
            uFlags = nifMessage | nifIcon | nifTip,
            uCallbackMessage = wmTrayCallback,
            hIcon = currentIconHandle,
            szTip = "LidDock"
        };

        shellNotifyIcon(nimAdd, ref data);
        isIconAdded = true;
    }

    private IntPtr generateStateIcon(clamshellState state)
    {
        uint color = state switch
        {
            clamshellState.clamshellActive => 0x0000C800,
            clamshellState.dockedLidOpen => 0x00D08000,
            clamshellState.disconnectPending => 0x0000A5FF,
            clamshellState.enteringSleep => 0x00800080,
            clamshellState.errorFallback => 0x000000FF,
            _ => 0x00808080
        };

        var screenDc = getDc(IntPtr.Zero);
        var memDc = createCompatibleDc(screenDc);
        var colorBmp = createCompatibleBitmap(screenDc, 16, 16);
        var maskBmp = createCompatibleBitmap(screenDc, 16, 16);

        var oldBmp = selectObject(memDc, colorBmp);
        var brush = createSolidBrush(color);
        var rect = new rect { left = 2, top = 2, right = 14, bottom = 14 };
        fillRect(memDc, ref rect, brush);
        deleteObject(brush);

        selectObject(memDc, maskBmp);
        var maskBrush = createSolidBrush(0x00000000);
        var fullRect = new rect { left = 0, top = 0, right = 16, bottom = 16 };
        fillRect(memDc, ref fullRect, maskBrush);
        deleteObject(maskBrush);

        selectObject(memDc, oldBmp);
        deleteDc(memDc);
        releaseDc(IntPtr.Zero, screenDc);

        var info = new iconInfo
        {
            fIcon = true,
            xHotspot = 0,
            yHotspot = 0,
            hbmMask = maskBmp,
            hbmColor = colorBmp
        };

        var hIcon = createIconIndirect(ref info);
        deleteObject(colorBmp);
        deleteObject(maskBmp);

        return hIcon;
    }

    void IDisposable.Dispose() => dispose();

    public void dispose()
    {
        if (isIconAdded)
        {
            var data = new notifyIconData
            {
                cbSize = (uint)Marshal.SizeOf<notifyIconData>(),
                hWnd = windowHandle,
                uID = 1
            };
            shellNotifyIcon(nimDelete, ref data);
            isIconAdded = false;
        }

        if (currentIconHandle != IntPtr.Zero)
        {
            destroyIcon(currentIconHandle);
            currentIconHandle = IntPtr.Zero;
        }
    }
}
