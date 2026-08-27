using System;
using System.Collections.ObjectModel;
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
        powerPlanSummary = acAction == 0 ? "Do Nothing (Clamshell Active)" : "Sleep (Windows Default)";

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
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            clamshellState = state;
            var acAction = powerSchemeManager.getCurrentAcLidAction();
            powerPlanSummary = acAction == 0 ? "Do Nothing (Clamshell Active)" : "Sleep (Windows Default)";
        });
    }

    private void handleDisplaysChanged(System.Collections.Generic.IReadOnlyList<physicalDisplayInfo> displays)
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            displaysList.Clear();
            foreach (var d in displays)
            {
                displaysList.Add(d);
            }
            displaysSummary = $"{displays.Count} Display Output(s)";
        });
    }

    private void handleLidChanged(lidState state)
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            lidState = state;
        });
    }

    private void handlePowerChanged(systemPowerInfo power)
    {
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
            var acAction = powerSchemeManager.getCurrentAcLidAction();
            powerPlanSummary = acAction == 0 ? "Do Nothing (Clamshell Active)" : "Sleep (Windows Default)";
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
            var expectedPlanSummary = acAction == 0 ? "Do Nothing (Clamshell Active)" : "Sleep (Windows Default)";
            if (powerPlanSummary != expectedPlanSummary)
            {
                powerPlanSummary = expectedPlanSummary;
            }

            var state = stateMachine.getCurrentState();
            if (clamshellState != state)
            {
                clamshellState = state;
            }
        });
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
