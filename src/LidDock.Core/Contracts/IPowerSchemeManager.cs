using System;

namespace LidDock.Core.Contracts;

public interface iPowerSchemeManager : IDisposable
{
    void backupOriginalSettings();
    bool applyClamshellAction(bool enableClamshell, bool applyToBattery);
    void restoreOriginalSettings();
    void triggerImmediateSleep();
    uint? getOriginalAcLidAction();
    uint? getOriginalDcLidAction();
    uint? getCurrentAcLidAction();
}
