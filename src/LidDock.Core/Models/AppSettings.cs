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
    public List<string> allowedDisplayPaths { get; set; } = new List<string>();
    public bool onlySelectedDisplaysTrigger { get; set; }
    public bool requireExternalDisplay { get; set; } = true;
    public bool sleepOnDisconnectWithLidClosed { get; set; } = true;
    public byte minimumBatteryThreshold { get; set; } = 15;
    public bool customRequireAcPower { get; set; } = false;
    public bool autoCheckForUpdates { get; set; } = true;
    public DateTime? lastUpdateCheckUtc { get; set; } = null;
    public DateTime? amphetawinUntilUtc { get; set; } = null;
    public int amphetawinMinutes { get; set; } = 15;
    public bool autoAmphetawinEnabled { get; set; } = false;
    public string? autoAmphetawinExecutablePath { get; set; } = null;
    public string? autoAmphetawinProcessName { get; set; } = null;
    public bool autoAmphetawinInfinite { get; set; } = false;
    public int autoAmphetawinDurationMinutes { get; set; } = 30;
    public bool autoAmphetawinDurationInHours { get; set; } = false;
    public bool autoAmphetawinOwned { get; set; } = false;
    public int emergencyFallbackSeconds { get; set; } = 1800;
}
