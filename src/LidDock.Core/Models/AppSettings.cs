namespace LidDock.Core.Models;

public enum operationalProfileType
{
    smartDocked,
    acOnly,
    alwaysClamshell,
    custom
}

public class appSettings
{
    public bool startWithWindows { get; set; } = true;
    public bool startMinimized { get; set; } = false;
    public bool enableClamshell { get; set; } = true;
    public operationalProfileType activeProfile { get; set; } = operationalProfileType.smartDocked;
    public int disconnectDelaySeconds { get; set; } = 5;
    public bool ignoreVirtualDisplays { get; set; } = true;
    public bool ignoreWirelessDisplays { get; set; } = true;
    public bool sleepOnDisconnectWithLidClosed { get; set; } = true;
    public bool requireAcPower { get; set; } = false;
    public byte minimumBatteryThreshold { get; set; } = 15;
    public bool restoreOnExit { get; set; } = true;
    public bool autoCheckForUpdates { get; set; } = true;
    public DateTime? lastUpdateCheckUtc { get; set; } = null;
}
