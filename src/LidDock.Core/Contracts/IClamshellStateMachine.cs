using System;
using System.Collections.Generic;
using LidDock.Core.Models;

namespace LidDock.Core.Contracts;

public interface iClamshellStateMachine : IDisposable
{
    event Action<clamshellState>? onStateChanged;
    clamshellState getCurrentState();
    void updateLidState(lidState state);
    void updateDisplays(IReadOnlyList<physicalDisplayInfo> displays);
    void updatePowerInfo(systemPowerInfo powerInfo);
    void updateSettings(appSettings settings);
    void forceReevaluate();
}
