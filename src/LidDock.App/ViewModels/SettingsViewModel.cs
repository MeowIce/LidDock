using System;
using System.Threading.Tasks;
using LidDock.Core.Contracts;
using LidDock.Core.Models;
using LidDock.Core.Services;

namespace LidDock.App.ViewModels;

public class settingsViewModel : baseViewModel
{
    private readonly appSettings settings;
    private readonly iClamshellStateMachine? stateMachine;
    private readonly iUpdateChecker updateCheckerInstance;

    public settingsViewModel(appSettings settings, iClamshellStateMachine? stateMachine = null, iUpdateChecker? updateChecker = null)
    {
        this.settings = settings;
        this.stateMachine = stateMachine;
        this.updateCheckerInstance = updateChecker ?? new updateChecker();
    }

    public string appVersion => "1.0.2";
    public string author => "MeowIce";
    public string githubUrl => "https://github.com/MeowIce/LidDock";

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
                settings.startMinimized = value;
                onPropertyChanged();
                onPropertyChanged(nameof(startMinimized));
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
                onPropertyChanged(nameof(isDisconnectDelayEnabled));
                onPropertyChanged(nameof(disconnectDelayNote));
                notifySettingsChanged();
            }
        }
    }

    public bool isDisconnectDelayEnabled => settings.sleepOnDisconnectWithLidClosed;

    public string disconnectDelayNote => settings.sleepOnDisconnectWithLidClosed
        ? "Grace delay before putting laptop to sleep when external display or charger (in AC Only mode) disconnects with lid closed."
        : "Not applicable: Sleep on disconnect is turned off.";

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

    public bool isBatteryThresholdEnabled => settings.activeProfile == operationalProfileType.smartDocked;

    public string batteryThresholdNote => settings.activeProfile switch
    {
        operationalProfileType.acOnly => "Not applicable: AC Power Only profile strictly requires AC wall power and never operates on battery.",
        operationalProfileType.alwaysClamshell => "Not applicable: Always Clamshell profile keeps clamshell active regardless of battery level.",
        _ => "Clamshell mode will disengage when battery drops below this threshold while undocked from AC power."
    };

    public bool isProfileSmartDocked
    {
        get => settings.activeProfile == operationalProfileType.smartDocked;
        set
        {
            if (value)
            {
                settings.activeProfile = operationalProfileType.smartDocked;
                onPropertyChanged();
                onPropertyChanged(nameof(isProfileAcOnly));
                onPropertyChanged(nameof(isProfileAlwaysClamshell));
                onPropertyChanged(nameof(isBatteryThresholdEnabled));
                onPropertyChanged(nameof(batteryThresholdNote));
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
                onPropertyChanged(nameof(isProfileSmartDocked));
                onPropertyChanged(nameof(isProfileAlwaysClamshell));
                onPropertyChanged(nameof(isBatteryThresholdEnabled));
                onPropertyChanged(nameof(batteryThresholdNote));
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
                onPropertyChanged(nameof(isProfileSmartDocked));
                onPropertyChanged(nameof(isProfileAcOnly));
                onPropertyChanged(nameof(isBatteryThresholdEnabled));
                onPropertyChanged(nameof(batteryThresholdNote));
                notifySettingsChanged();
            }
        }
    }

    public bool autoCheckForUpdates
    {
        get => settings.autoCheckForUpdates;
        set
        {
            if (settings.autoCheckForUpdates != value)
            {
                settings.autoCheckForUpdates = value;
                onPropertyChanged();
                notifySettingsChanged();
            }
        }
    }

    public bool isCheckingForUpdates { get; private set; }
    public bool canCheckForUpdates => !isCheckingForUpdates;
    public string updateStatusMessage { get; private set; } = "Check for updates from GitHub Releases.";
    public bool isUpdateAvailable { get; private set; }
    public string latestVersionUrl { get; private set; } = "https://github.com/MeowIce/LidDock/releases/latest";
    public string latestVersionString { get; private set; } = string.Empty;

    public async Task checkForUpdatesAsync()
    {
        if (isCheckingForUpdates)
        {
            return;
        }

        isCheckingForUpdates = true;
        updateStatusMessage = "Checking for updates...";
        isUpdateAvailable = false;
        onPropertyChanged(nameof(isCheckingForUpdates));
        onPropertyChanged(nameof(canCheckForUpdates));
        onPropertyChanged(nameof(updateStatusMessage));
        onPropertyChanged(nameof(isUpdateAvailable));

        try
        {
            var cleanVer = appVersion.Split('-')[0];
            var currentVer = Version.TryParse(cleanVer, out var parsed) ? parsed : new Version(1, 0, 1);
            var result = await updateCheckerInstance.checkForUpdatesAsync(currentVer);
            settings.lastUpdateCheckUtc = DateTime.UtcNow;
            notifySettingsChanged();

            if (result.isUpdateAvailable && result.latestVersion != null)
            {
                isUpdateAvailable = true;
                latestVersionString = $"v{result.latestVersion}";
                latestVersionUrl = string.IsNullOrEmpty(result.releaseUrl) ? githubUrl + "/releases/latest" : result.releaseUrl;
                updateStatusMessage = $"New version {latestVersionString} is available!";
            }
            else if (!string.IsNullOrEmpty(result.errorMessage))
            {
                updateStatusMessage = $"Check failed: {result.errorMessage}";
            }
            else
            {
                updateStatusMessage = "You are using the latest version of LidDock.";
            }
        }
        catch (Exception ex)
        {
            updateStatusMessage = $"Error checking updates: {ex.Message}";
        }
        finally
        {
            isCheckingForUpdates = false;
            onPropertyChanged(nameof(isCheckingForUpdates));
            onPropertyChanged(nameof(canCheckForUpdates));
            onPropertyChanged(nameof(updateStatusMessage));
            onPropertyChanged(nameof(isUpdateAvailable));
            onPropertyChanged(nameof(latestVersionString));
            onPropertyChanged(nameof(latestVersionUrl));
        }
    }

    public void save()
    {
        notifySettingsChanged();
    }

    private void notifySettingsChanged()
    {
        stateMachine?.updateSettings(settings);
        settingsManager.saveSettings(settings);
    }
}
