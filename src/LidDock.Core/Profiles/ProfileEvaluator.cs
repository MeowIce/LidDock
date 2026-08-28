namespace LidDock.Core.Profiles;

using LidDock.Core.Models;

public static class profileEvaluator
{
    public static bool shouldAllowClamshell(
        appSettings settings,
        systemPowerInfo powerInfo,
        bool hasExternalDisplay)
    {
        if (!settings.enableClamshell || !hasExternalDisplay)
        {
            return false;
        }

        switch (settings.activeProfile)
        {
            case operationalProfileType.smartDocked:
                if (powerInfo.powerSource == powerSourceType.battery && powerInfo.batteryPercent < settings.minimumBatteryThreshold)
                {
                    return false;
                }
                return true;

            case operationalProfileType.acOnly:
                return powerInfo.powerSource == powerSourceType.acPower;

            case operationalProfileType.alwaysClamshell:
                return true;

            default:
                return true;
        }
    }
}
