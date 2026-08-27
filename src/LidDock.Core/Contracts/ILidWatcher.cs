using System;
using LidDock.Core.Models;

namespace LidDock.Core.Contracts;

public interface iLidWatcher
{
    event Action<lidState>? onLidStateChanged;
    lidState queryLidState();
    void notifyLidStateChanged(bool isOpen);
}
