using System;
using System.Collections.Generic;
using LidDock.Core.Models;

namespace LidDock.Core.Contracts;

public interface iDisplayWatcher
{
    event Action<IReadOnlyList<physicalDisplayInfo>>? onDisplaysChanged;
    IReadOnlyList<physicalDisplayInfo> queryConnectedDisplays();
    void notifyDisplayConfigurationChanged();
}
