using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using LidDock.Core.Contracts;
using LidDock.Core.Models;
using LidDock.Diagnostics;

namespace LidDock.App.ViewModels;

public class diagnosticsViewModel : baseViewModel
{
    private readonly iClamshellStateMachine stateMachine;
    private readonly iDisplayWatcher displayWatcher;
    private readonly iLidWatcher lidWatcher;
    private readonly iPowerWatcher powerWatcher;
    private readonly iPowerSchemeManager powerSchemeManager;

    private clamshellState clamshellStateVal;
    private lidState lidStateVal;
    private systemPowerInfo currentPowerInfo = new systemPowerInfo(powerSourceType.unknown, 100, false);
    private string displaysSummaryVal = "0 Displays Detected";
    private string powerPlanSummaryVal = "Unknown";
    private string gracePeriodCountdownVal = "Not running";
    private string lastDisplayEventVal = "-";
    private string lastLidEventVal = "-";

    public ObservableCollection<logEntry> logEntries { get; } = new ObservableCollection<logEntry>();
    public ObservableCollection<physicalDisplayInfo> displaysList { get; } = new ObservableCollection<physicalDisplayInfo>();

    public clamshellState clamshellState
    {
        get => clamshellStateVal;
        set
        {
            if (setField(ref clamshellStateVal, value))
            {
                onPropertyChanged(nameof(formattedClamshellState));
            }
        }
    }

    public string formattedClamshellState => displayFormatters.formatClamshellState(clamshellState);

    public lidState lidState
    {
        get => lidStateVal;
        set
        {
            if (setField(ref lidStateVal, value))
            {
                onPropertyChanged(nameof(formattedLidState));
            }
        }
    }

    public string formattedLidState => displayFormatters.formatLidState(lidState);

    public string formattedPowerSource => displayFormatters.formatPowerSource(currentPowerInfo);

    public byte batteryPercent => currentPowerInfo.batteryPercent;

    public string displaysSummary
    {
        get => displaysSummaryVal;
        set => setField(ref displaysSummaryVal, value);
    }

    public string powerPlanSummary
    {
        get => powerPlanSummaryVal;
        set => setField(ref powerPlanSummaryVal, value);
    }

    public string gracePeriodCountdown
    {
        get => gracePeriodCountdownVal;
        set => setField(ref gracePeriodCountdownVal, value);
    }

    public string lastDisplayEvent
    {
        get => lastDisplayEventVal;
        set => setField(ref lastDisplayEventVal, value);
    }

    public string lastLidEvent
    {
        get => lastLidEventVal;
        set => setField(ref lastLidEventVal, value);
    }

    public diagnosticsViewModel(
        iClamshellStateMachine stateMachine,
        iDisplayWatcher displayWatcher,
        iLidWatcher lidWatcher,
        iPowerWatcher powerWatcher,
        iPowerSchemeManager powerSchemeManager)
    {
        this.stateMachine = stateMachine;
        this.displayWatcher = displayWatcher;
        this.lidWatcher = lidWatcher;
        this.powerWatcher = powerWatcher;
        this.powerSchemeManager = powerSchemeManager;

        diagnosticsLogger.instance.onNewLogEntry += handleNewLogEntry;
        stateMachine.onStateChanged += handleStateChanged;
        displayWatcher.onDisplaysChanged += handleDisplaysChanged;
        lidWatcher.onLidStateChanged += handleLidChanged;
        powerWatcher.onPowerStatusChanged += handlePowerChanged;

        refreshAll();
    }

    public void refreshAll()
    {
        clamshellState = stateMachine.getCurrentState();
        lidState = lidWatcher.queryLidState();
        currentPowerInfo = powerWatcher.queryPowerStatus();
        onPropertyChanged(nameof(formattedPowerSource));
        onPropertyChanged(nameof(batteryPercent));

        var displays = displayWatcher.queryConnectedDisplays();
        displaysList.Clear();
        foreach (var d in displays)
        {
            displaysList.Add(d);
        }
        displaysSummary = $"{displays.Count} Display Output(s)";

        var acAction = powerSchemeManager.getCurrentAcLidAction();
        powerPlanSummary = formatLidActionSummary(acAction);
        updateGracePeriodCountdown();

        logEntries.Clear();
        foreach (var entry in diagnosticsLogger.instance.getEntries())
        {
            logEntries.Add(entry);
        }
    }

    public string exportReport()
    {
        var snapshot = new systemDiagnosticsSnapshot
        {
            currentClamshellState = clamshellState,
            currentLidState = lidState,
            currentPowerInfo = powerWatcher.queryPowerStatus(),
            originalAcLidAction = powerSchemeManager.getOriginalAcLidAction(),
            originalDcLidAction = powerSchemeManager.getOriginalDcLidAction(),
            currentAcLidAction = powerSchemeManager.getCurrentAcLidAction(),
            connectedDisplays = displayWatcher.queryConnectedDisplays()
        };

        var targetFolder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        if (string.IsNullOrEmpty(targetFolder))
        {
            targetFolder = Path.Combine(Path.GetTempPath(), "LidDock");
        }

        return diagnosticsExporter.exportToFile(snapshot, targetFolder);
    }

    private void handleNewLogEntry(logEntry entry)
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            logEntries.Add(entry);
        });
    }

    private void handleStateChanged(clamshellState state)
    {
        diagnosticsLogger.instance.logInfo($"UI Event: State changed to {displayFormatters.formatClamshellState(state)}");
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            clamshellState = state;
            var acAction = powerSchemeManager.getCurrentAcLidAction();
            powerPlanSummary = formatLidActionSummary(acAction);
            updateGracePeriodCountdown();
        });
    }

    private void handleDisplaysChanged(System.Collections.Generic.IReadOnlyList<physicalDisplayInfo> displays)
    {
        diagnosticsLogger.instance.logInfo($"UI Event: Display topology changed ({displays.Count} output(s))");
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            displaysList.Clear();
            foreach (var d in displays)
            {
                displaysList.Add(d);
            }
            displaysSummary = $"{displays.Count} Display Output(s)";
            lastDisplayEvent = DateTime.Now.ToString("HH:mm:ss");
        });
    }

    private void handleLidChanged(lidState state)
    {
        diagnosticsLogger.instance.logInfo($"UI Event: Lid {displayFormatters.formatLidState(state)}");
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            lidState = state;
            lastLidEvent = DateTime.Now.ToString("HH:mm:ss");
        });
    }

    private void handlePowerChanged(systemPowerInfo power)
    {
        diagnosticsLogger.instance.logInfo($"UI Event: Power {displayFormatters.formatPowerSource(power)}");
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            currentPowerInfo = power;
            onPropertyChanged(nameof(formattedPowerSource));
            onPropertyChanged(nameof(batteryPercent));
        });
    }

    public void updateLidState(lidState state)
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            lidState = state;
            lidWatcher.notifyLidStateChanged(state == lidState.open);
            stateMachine.updateLidState(state);
            clamshellState = stateMachine.getCurrentState();
        });
    }

    public void onDisplayTopologyChanged()
    {
        displayWatcher.notifyDisplayConfigurationChanged();
    }

    public void onPowerStatusChanged()
    {
        powerWatcher.notifyPowerStatusChanged();
        pollDynamicMetrics();
    }

    public void onSettingsChanged()
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            stateMachine.updateSettings(settingsManager.loadSettings());
            var acAction = powerSchemeManager.getCurrentAcLidAction();
            powerPlanSummary = formatLidActionSummary(acAction);
            clamshellState = stateMachine.getCurrentState();
        });
    }

    public void pollDynamicMetrics()
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            var power = powerWatcher.queryPowerStatus();
            if (power.batteryPercent != currentPowerInfo.batteryPercent ||
                power.powerSource != currentPowerInfo.powerSource ||
                power.isCharging != currentPowerInfo.isCharging)
            {
                currentPowerInfo = power;
                onPropertyChanged(nameof(formattedPowerSource));
                onPropertyChanged(nameof(batteryPercent));
                stateMachine.updatePowerInfo(power);
            }

            var acAction = powerSchemeManager.getCurrentAcLidAction();
            var expectedPlanSummary = formatLidActionSummary(acAction);
            if (powerPlanSummary != expectedPlanSummary)
            {
                powerPlanSummary = expectedPlanSummary;
            }

            var state = stateMachine.getCurrentState();
            if (clamshellState != state)
            {
                clamshellState = state;
            }

            updateGracePeriodCountdown();
        });
    }

    private static string formatLidActionSummary(uint? acAction)
    {
        if (!acAction.HasValue)
        {
            return "Unknown";
        }

        return acAction.Value == 0 ? "Do Nothing (Clamshell Active)" : "Sleep (Windows Default)";
    }

    private void updateGracePeriodCountdown()
    {
        var seconds = stateMachine.getGraceRemainingSeconds();
        gracePeriodCountdown = seconds > 0 ? $"{seconds}s remaining" : "Not running";
    }

    public void unsubscribe()
    {
        diagnosticsLogger.instance.onNewLogEntry -= handleNewLogEntry;
        stateMachine.onStateChanged -= handleStateChanged;
        displayWatcher.onDisplaysChanged -= handleDisplaysChanged;
        lidWatcher.onLidStateChanged -= handleLidChanged;
        powerWatcher.onPowerStatusChanged -= handlePowerChanged;
        logEntries.Clear();
        displaysList.Clear();
    }
}
