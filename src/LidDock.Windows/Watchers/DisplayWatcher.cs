using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using LidDock.Core.Contracts;
using LidDock.Core.Models;
using LidDock.Windows.Native;

namespace LidDock.Windows.Watchers;

public class displayWatcher : iDisplayWatcher
{
    public event Action<IReadOnlyList<physicalDisplayInfo>>? onDisplaysChanged;

    public IReadOnlyList<physicalDisplayInfo> queryConnectedDisplays()
    {
        var displayList = new List<physicalDisplayInfo>();
        try
        {
            var sizeResult = nativeMethods.getDisplayConfigBufferSizes(
                nativeConstants.qdcOnlyActivePaths,
                out var pathCount,
                out var modeCount);

            if (sizeResult != nativeConstants.errorSuccess || pathCount == 0)
            {
                return displayList;
            }

            var paths = new displayConfigPathInfo[pathCount];
            var modes = new displayConfigModeInfo[modeCount];

            var queryResult = nativeMethods.queryDisplayConfig(
                nativeConstants.qdcOnlyActivePaths,
                ref pathCount,
                paths,
                ref modeCount,
                modes,
                IntPtr.Zero);

            if (queryResult != nativeConstants.errorSuccess)
            {
                return displayList;
            }

            for (var i = 0; i < pathCount; i++)
            {
                var target = paths[i].targetInfo;
                var deviceName = new displayConfigTargetDeviceName
                {
                    type = displayConfigDeviceInfoType.getTargetName,
                    size = (uint)Marshal.SizeOf<displayConfigTargetDeviceName>(),
                    adapterId = target.adapterId,
                    id = target.id
                };

                var nameResult = nativeMethods.displayConfigGetDeviceInfo(ref deviceName);
                var monitorName = nameResult == nativeConstants.errorSuccess && !string.IsNullOrWhiteSpace(deviceName.monitorFriendlyDeviceName)
                    ? deviceName.monitorFriendlyDeviceName
                    : "External Monitor";

                var pathStr = deviceName.monitorDevicePath ?? string.Empty;

                var isInternal = target.outputTechnology == displayTechnology.internalDisplay ||
                                 target.outputTechnology == displayTechnology.displayPortEmbedded ||
                                 target.outputTechnology == displayTechnology.udiEmbedded;

                var isVirtual = target.outputTechnology == displayTechnology.indirectVirtual ||
                                target.outputTechnology == displayTechnology.miracast ||
                                pathStr.Contains("ROOT#RDP", StringComparison.OrdinalIgnoreCase) ||
                                pathStr.Contains("IddCx", StringComparison.OrdinalIgnoreCase);

                displayList.Add(new physicalDisplayInfo(
                    monitorName,
                    target.outputTechnology,
                    isInternal,
                    isVirtual,
                    pathStr));
            }
        }
        catch
        {
        }

        return displayList;
    }

    public void notifyDisplayConfigurationChanged()
    {
        try
        {
            var displays = queryConnectedDisplays();
            onDisplaysChanged?.Invoke(displays);
        }
        catch
        {
        }
    }
}
