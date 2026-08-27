namespace LidDock.Core.Models;

public enum powerSourceType
{
    battery,
    acPower,
    unknown
}

public record systemPowerInfo(
    powerSourceType powerSource,
    byte batteryPercent,
    bool isCharging);
