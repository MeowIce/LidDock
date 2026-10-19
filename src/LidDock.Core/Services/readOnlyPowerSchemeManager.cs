using System;
using LidDock.Core.Contracts;

namespace LidDock.Core.Services;

public class readOnlyPowerSchemeManager : iPowerSchemeManager
{
    private readonly iPowerSchemeManager inner;

    public readOnlyPowerSchemeManager(iPowerSchemeManager inner)
    {
        this.inner = inner;
    }

    public void backupOriginalSettings()
    {
    }

    public bool applyClamshellAction(bool enableClamshell, bool applyToBattery)
    {
        return true;
    }

    public void restoreOriginalSettings()
    {
    }

    public void triggerImmediateSleep()
    {
    }

    public uint? getOriginalAcLidAction()
    {
        return inner.getOriginalAcLidAction();
    }

    public uint? getOriginalDcLidAction()
    {
        return inner.getOriginalDcLidAction();
    }

    public uint? getCurrentAcLidAction()
    {
        return inner.getCurrentAcLidAction();
    }

    void IDisposable.Dispose()
    {
    }

    public void dispose()
    {
    }
}
