using System;
using LidDock.Core.Contracts;
using LidDock.Core.Models;

namespace LidDock.Windows.Watchers;

public class lidWatcher : iLidWatcher
{
    private lidState currentLidState = lidState.open;

    public event Action<lidState>? onLidStateChanged;

    public lidState queryLidState()
    {
        return currentLidState;
    }

    public void notifyLidStateChanged(bool isOpen)
    {
        currentLidState = isOpen ? lidState.open : lidState.closed;
        onLidStateChanged?.Invoke(currentLidState);
    }
}
