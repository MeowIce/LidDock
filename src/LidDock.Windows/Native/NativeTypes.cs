using System;
using System.Runtime.InteropServices;
using LidDock.Core.Models;

namespace LidDock.Windows.Native;

public static class nativeConstants
{
    public const int qdcOnlyActivePaths = 0x00000002;
    public const int errorSuccess = 0;
    public const int wmDisplayChange = 0x007E;
    public const int wmPowerBroadcast = 0x0218;
    public const int pbtPowerSettingChange = 0x8013;
    public const int pbtApmPowerStatusChange = 0x000A;
    public const int deviceNotifyWindowHandle = 0x00000000;
    public const int hwndBroadcast = 0xffff;

    public static readonly Guid guidSystemButtonSubgroup = new Guid("4f971e89-eebd-4455-a8de-9e59040e7347");
    public static readonly Guid guidLidCloseAction = new Guid("5ca83367-6e45-459f-a27b-476b1d01c936");
    public static readonly Guid guidLidSwitchStateChange = new Guid("ba3e0f4d-b817-4094-a2d1-d56379e6a0f3");
    public static readonly Guid guidAcdcPowerSource = new Guid("5d3e9a59-e9d5-4b00-a6bd-ff34ff516548");
    public static readonly Guid guidBatteryPercentageRemaining = new Guid("a7ad8041-b45a-4cae-87a3-eecbb468a9e1");
}

public enum displayConfigModeInfoType : uint
{
    zero = 0,
    target = 1,
    source = 2,
    desktopImage = 3
}

public enum displayConfigDeviceInfoType : uint
{
    getTargetName = 2
}

[StructLayout(LayoutKind.Sequential)]
public struct luid
{
    public uint lowPart;
    public int highPart;
}

[StructLayout(LayoutKind.Sequential)]
public struct displayConfigRational
{
    public uint numerator;
    public uint denominator;
}

[StructLayout(LayoutKind.Sequential)]
public struct displayConfigPathSourceInfo
{
    public luid adapterId;
    public uint id;
    public uint modeInfoIdx;
    public uint statusFlags;
}

[StructLayout(LayoutKind.Sequential)]
public struct displayConfigPathTargetInfo
{
    public luid adapterId;
    public uint id;
    public uint modeInfoIdx;
    public displayTechnology outputTechnology;
    public uint rotation;
    public uint scaling;
    public displayConfigRational refreshRate;
    public uint scanLineOrdering;
    public int targetAvailable;
    public uint statusFlags;
}

[StructLayout(LayoutKind.Sequential)]
public struct displayConfigPathInfo
{
    public displayConfigPathSourceInfo sourceInfo;
    public displayConfigPathTargetInfo targetInfo;
    public uint flags;
}

[StructLayout(LayoutKind.Explicit, Size = 64)]
public struct displayConfigModeInfo
{
    [FieldOffset(0)] public displayConfigModeInfoType infoType;
    [FieldOffset(4)] public uint id;
    [FieldOffset(8)] public luid adapterId;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct displayConfigTargetDeviceNameFlags
{
    public uint value;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct displayConfigTargetDeviceName
{
    public displayConfigDeviceInfoType type;
    public uint size;
    public luid adapterId;
    public uint id;
    public displayConfigTargetDeviceNameFlags flags;
    public displayTechnology outputTechnology;
    public ushort edidManufactureId;
    public ushort edidProductCodeId;
    public uint connectorInstance;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string monitorFriendlyDeviceName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string monitorDevicePath;
}

[StructLayout(LayoutKind.Sequential)]
public struct powerBroadcastSetting
{
    public Guid powerSetting;
    public uint dataLength;
    public byte data;
}

[StructLayout(LayoutKind.Sequential)]
public struct systemPowerStatus
{
    public byte acLineStatus;
    public byte batteryFlag;
    public byte batteryLifePercent;
    public byte systemStatusFlag;
    public uint batteryLifeTime;
    public uint batteryFullLifeTime;
}
