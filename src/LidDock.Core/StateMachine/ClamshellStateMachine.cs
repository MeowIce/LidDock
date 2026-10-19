using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LidDock.Core.Contracts;
using LidDock.Core.Models;
using LidDock.Core.Profiles;

namespace LidDock.Core.StateMachine;

public class clamshellStateMachine : iClamshellStateMachine
{
    private readonly iPowerSchemeManager powerManager;
    private readonly object syncLock = new object();
    private clamshellState currentState = clamshellState.normalMode;
    private CancellationTokenSource? gracePeriodCts;
    private DateTime gracePeriodDeadlineUtc = DateTime.MinValue;
    private CancellationTokenSource? emergencyWatchdogCts;
    private lidState currentLidState = lidState.open;
    private IReadOnlyList<physicalDisplayInfo> currentDisplays = Array.Empty<physicalDisplayInfo>();
    private systemPowerInfo currentPowerInfo = new systemPowerInfo(powerSourceType.unknown, 100, false);
    private appSettings settings = new appSettings();

    public event Action<clamshellState>? onStateChanged;
    public event Action<string, string>? onNotificationRequested;

    public clamshellStateMachine(iPowerSchemeManager powerManager)
    {
        this.powerManager = powerManager;
    }

    public clamshellState getCurrentState()
    {
        lock (syncLock)
        {
            return currentState;
        }
    }

    public int getGraceRemainingSeconds()
    {
        lock (syncLock)
        {
            if (currentState != clamshellState.disconnectPending)
            {
                return 0;
            }

            var remaining = (gracePeriodDeadlineUtc - DateTime.UtcNow).TotalSeconds;
            return remaining <= 0 ? 0 : (int)Math.Ceiling(remaining);
        }
    }

    public void updateSettings(appSettings newSettings)
    {
        lock (syncLock)
        {
            var wasPending = currentState == clamshellState.disconnectPending;
            settings = newSettings;
            evaluateState();
            if (wasPending && currentState == clamshellState.disconnectPending)
            {
                startGracePeriod();
            }
        }
    }

    public void updateLidState(lidState state)
    {
        lock (syncLock)
        {
            currentLidState = state;
            evaluateState();
        }
    }

    public void updateDisplays(IReadOnlyList<physicalDisplayInfo> displays)
    {
        lock (syncLock)
        {
            currentDisplays = displays;
            evaluateState();
        }
    }

    public void updatePowerInfo(systemPowerInfo powerInfo)
    {
        lock (syncLock)
        {
            currentPowerInfo = powerInfo;
            evaluateState();
        }
    }

    private bool hasEligibleExternalDisplay()
    {
        return currentDisplays.Any(d =>
        {
            if (d.isInternal)
            {
                return false;
            }
            if (settings.ignoreVirtualDisplays && d.isVirtual)
            {
                return false;
            }
            if (settings.ignoreWirelessDisplays && d.technology == displayTechnology.miracast)
            {
                return false;
            }
            if (settings.onlySelectedDisplaysTrigger && (settings.allowedDisplayPaths is null || !settings.allowedDisplayPaths.Contains(d.devicePath)))
            {
                return false;
            }
            return true;
        });
    }

    private void evaluateState()
    {
        var hasExternal = hasEligibleExternalDisplay();
        var isClamshellPermitted = profileEvaluator.shouldAllowClamshell(settings, currentPowerInfo, hasExternal);
        var isLidClosed = currentLidState == lidState.closed;

        if ((hasExternal || !settings.requireExternalDisplay) && isClamshellPermitted)
        {
            if (currentState == clamshellState.disconnectPending &&
                profileEvaluator.requiresAcPower(settings))
            {
                onNotificationRequested?.Invoke(
                    "LidDock - AC Power Restored",
                    "AC power reconnected. Clamshell mode resumed.");
            }

            cancelGracePeriod();

            var allowBattery = settings.activeProfile switch
            {
                operationalProfileType.alwaysClamshell => true,
                operationalProfileType.smartDocked => currentPowerInfo.batteryPercent >= settings.minimumBatteryThreshold,
                operationalProfileType.acOnly => false,
                operationalProfileType.custom => !settings.customRequireAcPower && currentPowerInfo.batteryPercent >= settings.minimumBatteryThreshold,
                _ => false
            };

            if (isLidClosed)
            {
                transitionTo(clamshellState.clamshellActive);
                powerManager.applyClamshellAction(true, allowBattery);
            }
            else
            {
                transitionTo(clamshellState.dockedLidOpen);
                powerManager.applyClamshellAction(true, allowBattery);
            }
        }
        else
        {
            if (!isLidClosed)
            {
                cancelGracePeriod();
                transitionTo(clamshellState.normalMode);
                powerManager.restoreOriginalSettings();
            }
            else
            {
                if (currentState == clamshellState.clamshellActive)
                {
                    if (profileEvaluator.requiresAcPower(settings) &&
                        currentPowerInfo.powerSource != powerSourceType.acPower)
                    {
                        var delay = Math.Max(1, settings.disconnectDelaySeconds);
                        var delayText = settings.sleepOnDisconnectWithLidClosed
                            ? $"Laptop will sleep in {delay}s to prevent overheating."
                            : "Laptop is entering sleep mode.";
                        onNotificationRequested?.Invoke(
                            "LidDock - AC Power Disconnected",
                            $"AC power is required in Clamshell mode but is not available. {delayText}");
                    }

                    if (settings.sleepOnDisconnectWithLidClosed || isAmphetawinActive())
                    {
                        startGracePeriod();
                    }
                    else
                    {
                        transitionTo(clamshellState.enteringSleep);
                        powerManager.restoreOriginalSettings();
                        powerManager.triggerImmediateSleep();
                    }
                }
                else if (currentState != clamshellState.disconnectPending && currentState != clamshellState.enteringSleep)
                {
                    transitionTo(clamshellState.normalMode);
                    powerManager.restoreOriginalSettings();
                }
            }
        }
    }

    private bool isAmphetawinActive()
    {
        return settings.amphetawinUntilUtc.HasValue && settings.amphetawinUntilUtc.Value > DateTime.UtcNow;
    }

    private int getEffectiveSleepDelayMs()
    {
        var delayMs = Math.Max(1, settings.disconnectDelaySeconds) * 1000;
        if (isAmphetawinActive())
        {
            var remainingMs = (settings.amphetawinUntilUtc!.Value - DateTime.UtcNow).TotalMilliseconds;
            var emergencyMs = (double)Math.Max(1, settings.emergencyFallbackSeconds) * 1000;
            delayMs = (int)Math.Max(delayMs, Math.Min(remainingMs, emergencyMs));
        }

        return delayMs;
    }

    private void startGracePeriod()
    {
        cancelGracePeriod();
        transitionTo(clamshellState.disconnectPending);
        var cts = new CancellationTokenSource();
        gracePeriodCts = cts;
        var delayMs = getEffectiveSleepDelayMs();
        gracePeriodDeadlineUtc = DateTime.UtcNow.AddMilliseconds(delayMs);

        Task.Delay(delayMs, cts.Token).ContinueWith(task =>
        {
            if (task.IsCanceled || !task.IsCompletedSuccessfully)
            {
                return;
            }

            lock (syncLock)
            {
                if (gracePeriodCts != cts || cts.IsCancellationRequested)
                {
                    return;
                }

                var hasExternal = hasEligibleExternalDisplay();
                var isLidClosed = currentLidState == lidState.closed;
                var isClamshellPermitted = profileEvaluator.shouldAllowClamshell(settings, currentPowerInfo, hasExternal);

                if (!isClamshellPermitted && isLidClosed)
                {
                    transitionTo(clamshellState.enteringSleep);
                    powerManager.restoreOriginalSettings();
                    powerManager.triggerImmediateSleep();
                }
                else if (!isClamshellPermitted && !isLidClosed)
                {
                    transitionTo(clamshellState.normalMode);
                    powerManager.restoreOriginalSettings();
                }
                else if (isClamshellPermitted)
                {
                    transitionTo(isLidClosed ? clamshellState.clamshellActive : clamshellState.dockedLidOpen);
                }
            }
        }, TaskScheduler.Default);

        var emergencyCts = new CancellationTokenSource();
        emergencyWatchdogCts = emergencyCts;
        var emergencyMs = Math.Max(1, settings.emergencyFallbackSeconds) * 1000;

        Task.Delay(emergencyMs, emergencyCts.Token).ContinueWith(task =>
        {
            if (task.IsCanceled || !task.IsCompletedSuccessfully)
            {
                return;
            }

            lock (syncLock)
            {
                if (emergencyWatchdogCts != emergencyCts || emergencyCts.IsCancellationRequested)
                {
                    return;
                }

                var hasExternal = hasEligibleExternalDisplay();
                var isLidClosed = currentLidState == lidState.closed;
                var isClamshellPermitted = profileEvaluator.shouldAllowClamshell(settings, currentPowerInfo, hasExternal);

                if (currentState == clamshellState.disconnectPending && isLidClosed && !isClamshellPermitted)
                {
                    transitionTo(clamshellState.enteringSleep);
                    powerManager.restoreOriginalSettings();
                    powerManager.triggerImmediateSleep();
                }
            }
        }, TaskScheduler.Default);
    }

    private void cancelGracePeriod()
    {
        if (gracePeriodCts != null)
        {
            try
            {
                gracePeriodCts.Cancel();
                gracePeriodCts.Dispose();
            }
            catch
            {
            }
            gracePeriodCts = null;
        }

        if (emergencyWatchdogCts != null)
        {
            try
            {
                emergencyWatchdogCts.Cancel();
                emergencyWatchdogCts.Dispose();
            }
            catch
            {
            }
            emergencyWatchdogCts = null;
        }
    }

    private void transitionTo(clamshellState newState)
    {
        if (currentState == newState)
        {
            return;
        }

        currentState = newState;
        onStateChanged?.Invoke(newState);
    }

    void IDisposable.Dispose() => dispose();

    public void dispose()
    {
        cancelGracePeriod();
    }
}
