using System;
using System.Collections.Generic;
using LidDock.Core.Models;

namespace LidDock.Core.Contracts;

public interface iClamshellStateMachine : IDisposable
{
    event Action<clamshellState>? onStateChanged;
    event Action<string, string>? onNotificationRequested;
    clamshellState getCurrentState();
    void updateLidState(lidState state);
    void updateDisplays(IReadOnlyList<physicalDisplayInfo> displays);
    void updatePowerInfo(systemPowerInfo powerInfo);
    void updateSettings(appSettings settings);
}
