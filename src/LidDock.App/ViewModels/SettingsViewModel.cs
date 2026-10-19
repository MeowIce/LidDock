using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using LidDock.Core.Contracts;
using LidDock.Core.Models;
using LidDock.Core.Services;
using LidDock.Windows.Watchers;

namespace LidDock.App.ViewModels;

public class settingsViewModel : baseViewModel
{
    private readonly appSettings settings;
    private readonly iClamshellStateMachine? stateMachine;
    private readonly iUpdateChecker updateCheckerInstance;
    private readonly displayWatcher settingsDisplayWatcher = new displayWatcher();

    public settingsViewModel(appSettings settings, iClamshellStateMachine? stateMachine = null, iUpdateChecker? updateChecker = null)
    {
        this.settings = settings;
        this.stateMachine = stateMachine;
        this.updateCheckerInstance = updateChecker ?? new updateChecker();
        settings.allowedDisplayPaths ??= new List<string>();
    }

    public string appVersion => "1.0.3";
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

    public bool requireExternalDisplay
    {
        get => settings.requireExternalDisplay;
        set
        {
            if (settings.requireExternalDisplay != value)
            {
                settings.requireExternalDisplay = value;
                onPropertyChanged();
                onPropertyChanged(nameof(externalDisplayNote));
                notifySettingsChanged();
            }
        }
    }

    public string externalDisplayNote => settings.requireExternalDisplay
        ? "Clamshell mode engages only while an external display is connected."
        : "Laptop keeps running with the lid closed even without an external display. Keep it in a ventilated place; battery and AC rules still apply.";

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

    public bool onlySelectedDisplaysTrigger
    {
        get => settings.onlySelectedDisplaysTrigger;
        set
        {
            if (settings.onlySelectedDisplaysTrigger != value)
            {
                settings.onlySelectedDisplaysTrigger = value;
                onPropertyChanged();
                onPropertyChanged(nameof(displaySelectionNote));
                notifySettingsChanged();
            }
        }
    }

    public ObservableCollection<selectableDisplay> detectedDisplays { get; } = new ObservableCollection<selectableDisplay>();

    public bool hasNoDetectedDisplays => detectedDisplays.Count == 0;

    public void refreshDisplayList()
    {
        var displays = settingsDisplayWatcher.queryConnectedDisplays();
        detectedDisplays.Clear();
        foreach (var display in displays)
        {
            if (display.isInternal || string.IsNullOrEmpty(display.devicePath))
            {
                continue;
            }

            var subtitle = display.formattedTechnology;
            if (display.isVirtual)
            {
                subtitle += " (virtual)";
            }

            detectedDisplays.Add(new selectableDisplay(
                display.devicePath,
                display.friendlyName,
                subtitle,
                settings.allowedDisplayPaths.Contains(display.devicePath),
                onDisplaySelectionToggled));
        }

        onPropertyChanged(nameof(hasNoDetectedDisplays));
    }

    private void onDisplaySelectionToggled(string devicePath, bool isChecked)
    {
        if (isChecked)
        {
            if (!settings.allowedDisplayPaths.Contains(devicePath))
            {
                settings.allowedDisplayPaths.Add(devicePath);
            }
        }
        else
        {
            settings.allowedDisplayPaths.Remove(devicePath);
        }

        notifySettingsChanged();
    }

    public string displaySelectionNote => settings.onlySelectedDisplaysTrigger
        ? "Only displays turned on below trigger Clamshell Mode."
        : "Every external display triggers Clamshell Mode.";

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
        ? "Grace delay before putting laptop to sleep when the external display or charger disconnects with lid closed."
        : "Not applicable: Sleep on disconnect is turned off.";

    public bool amphetawinEnabled
    {
        get => settings.amphetawinUntilUtc.HasValue && settings.amphetawinUntilUtc.Value > DateTime.UtcNow;
        set
        {
            var wasEnabled = settings.amphetawinUntilUtc.HasValue && settings.amphetawinUntilUtc.Value > DateTime.UtcNow;
            if (wasEnabled != value)
            {
                settings.amphetawinUntilUtc = value ? DateTime.UtcNow.AddMinutes(Math.Max(1, settings.amphetawinMinutes)) : null;
                onPropertyChanged();
                onPropertyChanged(nameof(amphetawinNote));
                notifySettingsChanged();
            }
        }
    }

    public int amphetawinMinutes
    {
        get => settings.amphetawinMinutes;
        set
        {
            if (settings.amphetawinMinutes != value)
            {
                settings.amphetawinMinutes = value;
                onPropertyChanged();
                onPropertyChanged(nameof(amphetawinNote));
                notifySettingsChanged();
            }
        }
    }

    public string amphetawinNote => amphetawinEnabled
        ? $"Active until {settings.amphetawinUntilUtc!.Value.ToLocalTime():HH:mm}. The system will not sleep automatically; safety sleep is postponed but never beyond the emergency fallback."
        : "Disables automatic sleep for the chosen duration so the system stays awake. Safety sleep on disconnect is postponed, but never beyond the emergency fallback.";

    public int emergencyFallbackMinutes
    {
        get => Math.Max(1, settings.emergencyFallbackSeconds / 60);
        set
        {
            var seconds = Math.Max(60, value * 60);
            if (settings.emergencyFallbackSeconds != seconds)
            {
                settings.emergencyFallbackSeconds = seconds;
                onPropertyChanged();
                notifySettingsChanged();
            }
        }
    }

    public string emergencyFallbackNote => "Hard cutoff: even while Amphetawin is active, the laptop is put to sleep after this long when the lid is closed without clamshell permission.";

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

    public bool customRequireAcPower
    {
        get => settings.customRequireAcPower;
        set
        {
            if (settings.customRequireAcPower != value)
            {
                settings.customRequireAcPower = value;
                onPropertyChanged();
                onPropertyChanged(nameof(isCustomThresholdEnabled));
                onPropertyChanged(nameof(batteryThresholdNote));
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
                onPropertyChanged(nameof(isProfileSmartDocked));
                onPropertyChanged(nameof(isProfileAcOnly));
                onPropertyChanged(nameof(isProfileAlwaysClamshell));
                onPropertyChanged(nameof(isBatteryThresholdEnabled));
                onPropertyChanged(nameof(batteryThresholdNote));
                onPropertyChanged(nameof(safetyBatteryThresholdNote));
                notifySettingsChanged();
            }
        }
    }

    public bool isBatteryThresholdEnabled => settings.activeProfile == operationalProfileType.smartDocked;

    public bool isCustomThresholdEnabled => !settings.customRequireAcPower;

    public string batteryThresholdNote => settings.activeProfile switch
    {
        operationalProfileType.acOnly => "Not applicable: AC Power Only profile strictly requires AC wall power and never operates on battery.",
        operationalProfileType.alwaysClamshell => "Not applicable: Always Clamshell profile keeps clamshell active regardless of battery level.",
        operationalProfileType.custom when settings.customRequireAcPower =>
            "Not applicable: Custom profile requires AC power. The threshold applies only when AC power is optional.",
        _ => "Clamshell mode will disengage when battery drops below this threshold while undocked from AC power."
    };

    public string safetyBatteryThresholdNote => settings.activeProfile == operationalProfileType.custom
        ? "Set in the Profiles tab for the Custom profile."
        : batteryThresholdNote;

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
                onPropertyChanged(nameof(isProfileCustom));
                onPropertyChanged(nameof(isBatteryThresholdEnabled));
                onPropertyChanged(nameof(batteryThresholdNote));
                onPropertyChanged(nameof(safetyBatteryThresholdNote));
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
                onPropertyChanged(nameof(isProfileCustom));
                onPropertyChanged(nameof(isBatteryThresholdEnabled));
                onPropertyChanged(nameof(batteryThresholdNote));
                onPropertyChanged(nameof(safetyBatteryThresholdNote));
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
                onPropertyChanged(nameof(isProfileCustom));
                onPropertyChanged(nameof(isBatteryThresholdEnabled));
                onPropertyChanged(nameof(batteryThresholdNote));
                onPropertyChanged(nameof(safetyBatteryThresholdNote));
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
