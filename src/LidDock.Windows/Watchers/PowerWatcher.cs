using System;
using LidDock.Core.Contracts;
using LidDock.Core.Models;
using LidDock.Windows.Native;

namespace LidDock.Windows.Watchers;

public class powerWatcher : iPowerWatcher
{
    public event Action<systemPowerInfo>? onPowerStatusChanged;

    public systemPowerInfo queryPowerStatus()
    {
        if (!nativeMethods.getSystemPowerStatus(out var status))
        {
            return new systemPowerInfo(powerSourceType.unknown, 100, false);
        }

        var source = status.acLineStatus switch
        {
            0 => powerSourceType.battery,
            1 => powerSourceType.acPower,
            _ => powerSourceType.unknown
        };

        var isCharging = (status.batteryFlag & 8) != 0;
        var percent = status.batteryLifePercent > 100 ? (byte)100 : status.batteryLifePercent;

        return new systemPowerInfo(source, percent, isCharging);
    }

    public void notifyPowerStatusChanged()
    {
        var info = queryPowerStatus();
        onPowerStatusChanged?.Invoke(info);
    }
}
