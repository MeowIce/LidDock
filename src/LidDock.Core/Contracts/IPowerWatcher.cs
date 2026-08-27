using System;
using LidDock.Core.Models;

namespace LidDock.Core.Contracts;

public interface iPowerWatcher
{
    event Action<systemPowerInfo>? onPowerStatusChanged;
    systemPowerInfo queryPowerStatus();
    void notifyPowerStatusChanged();
}
