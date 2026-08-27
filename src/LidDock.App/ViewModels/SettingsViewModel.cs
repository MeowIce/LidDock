using System;
using LidDock.Core.Contracts;
using LidDock.Core.Models;

namespace LidDock.App.ViewModels;

public class settingsViewModel : baseViewModel
{
    private readonly appSettings settings;
    private readonly iClamshellStateMachine stateMachine;

    public settingsViewModel(appSettings settings, iClamshellStateMachine stateMachine)
    {
        this.settings = settings;
        this.stateMachine = stateMachine;
    }

    public bool enableClamshell
    {
        get => settings.enableClamshell;
        set
        {
            if (settings.enableClamshell != value)
            {
                settings.enableClamshell = value;
                onPropertyChanged();
                notifySettingsChanged();
            }
        }
    }

    public bool startWithWindows
    {
        get => settings.startWithWindows;
        set
        {
            if (settings.startWithWindows != value)
            {
                settings.startWithWindows = value;
                onPropertyChanged();
                notifySettingsChanged();
            }
        }
    }

    public bool startMinimized
    {
        get => settings.startMinimized;
        set
        {
            if (settings.startMinimized != value)
            {
                settings.startMinimized = value;
                onPropertyChanged();
                notifySettingsChanged();
            }
        }
    }

    public int disconnectDelaySeconds
    {
        get => settings.disconnectDelaySeconds;
        set
        {
            if (settings.disconnectDelaySeconds != value)
            {
                settings.disconnectDelaySeconds = value;
                onPropertyChanged();
                notifySettingsChanged();
            }
        }
    }

    public bool ignoreVirtualDisplays
    {
        get => settings.ignoreVirtualDisplays;
        set
        {
            if (settings.ignoreVirtualDisplays != value)
            {
                settings.ignoreVirtualDisplays = value;
                onPropertyChanged();
                notifySettingsChanged();
            }
        }
    }

    public bool ignoreWirelessDisplays
    {
        get => settings.ignoreWirelessDisplays;
        set
        {
            if (settings.ignoreWirelessDisplays != value)
            {
                settings.ignoreWirelessDisplays = value;
                onPropertyChanged();
                notifySettingsChanged();
            }
        }
    }

    public bool sleepOnDisconnectWithLidClosed
    {
        get => settings.sleepOnDisconnectWithLidClosed;
        set
        {
            if (settings.sleepOnDisconnectWithLidClosed != value)
            {
                settings.sleepOnDisconnectWithLidClosed = value;
                onPropertyChanged();
                notifySettingsChanged();
            }
        }
    }

    public bool requireAcPower
    {
        get => settings.requireAcPower;
        set
        {
            if (settings.requireAcPower != value)
            {
                settings.requireAcPower = value;
                onPropertyChanged();
                notifySettingsChanged();
            }
        }
    }

    public byte minimumBatteryThreshold
    {
        get => settings.minimumBatteryThreshold;
        set
        {
            if (settings.minimumBatteryThreshold != value)
            {
                settings.minimumBatteryThreshold = value;
                onPropertyChanged();
                notifySettingsChanged();
            }
        }
    }

    public bool isProfileSmartDocked
    {
        get => settings.activeProfile == operationalProfileType.smartDocked;
        set
        {
            if (value)
            {
                settings.activeProfile = operationalProfileType.smartDocked;
                onPropertyChanged();
                notifySettingsChanged();
            }
        }
    }

    public bool isProfileAcOnly
    {
        get => settings.activeProfile == operationalProfileType.acOnly;
        set
        {
            if (value)
            {
                settings.activeProfile = operationalProfileType.acOnly;
                onPropertyChanged();
                notifySettingsChanged();
            }
        }
    }

    public bool isProfileAlwaysClamshell
    {
        get => settings.activeProfile == operationalProfileType.alwaysClamshell;
        set
        {
            if (value)
            {
                settings.activeProfile = operationalProfileType.alwaysClamshell;
                onPropertyChanged();
                notifySettingsChanged();
            }
        }
    }

    public bool isProfileCustom
    {
        get => settings.activeProfile == operationalProfileType.custom;
        set
        {
            if (value)
            {
                settings.activeProfile = operationalProfileType.custom;
                onPropertyChanged();
                notifySettingsChanged();
            }
        }
    }

    public void save()
    {
        notifySettingsChanged();
    }

    private void notifySettingsChanged()
    {
        stateMachine.updateSettings(settings);
        settingsManager.saveSettings(settings);
    }
}
