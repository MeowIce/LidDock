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
    private lidState currentLidState = lidState.open;
    private IReadOnlyList<physicalDisplayInfo> currentDisplays = Array.Empty<physicalDisplayInfo>();
    private systemPowerInfo currentPowerInfo = new systemPowerInfo(powerSourceType.unknown, 100, false);
    private appSettings settings = new appSettings();

    public event Action<clamshellState>? onStateChanged;

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

    public void updateSettings(appSettings newSettings)
    {
        lock (syncLock)
        {
            settings = newSettings;
            evaluateState();
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

    public void forceReevaluate()
    {
        lock (syncLock)
        {
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
            return true;
        });
    }

    private void evaluateState()
    {
        var hasExternal = hasEligibleExternalDisplay();
        var isClamshellPermitted = profileEvaluator.shouldAllowClamshell(settings, currentPowerInfo, hasExternal);
        var isLidClosed = currentLidState == lidState.closed;

        if (hasExternal && isClamshellPermitted)
        {
            cancelGracePeriod();

            if (isLidClosed)
            {
                transitionTo(clamshellState.clamshellActive);
                powerManager.applyClamshellAction(true, currentPowerInfo.powerSource == powerSourceType.battery);
            }
            else
            {
                transitionTo(clamshellState.dockedLidOpen);
                powerManager.applyClamshellAction(true, currentPowerInfo.powerSource == powerSourceType.battery);
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
                    if (settings.sleepOnDisconnectWithLidClosed)
                    {
                        startGracePeriod();
                    }
                    else
                    {
                        transitionTo(clamshellState.normalMode);
                        powerManager.restoreOriginalSettings();
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

    private void startGracePeriod()
    {
        cancelGracePeriod();
        transitionTo(clamshellState.disconnectPending);
        gracePeriodCts = new CancellationTokenSource();
        var token = gracePeriodCts.Token;
        var delayMs = Math.Max(1, settings.disconnectDelaySeconds) * 1000;

        Task.Delay(delayMs, token).ContinueWith(task =>
        {
            if (task.IsCanceled)
            {
                return;
            }

            lock (syncLock)
            {
                var hasExternal = hasEligibleExternalDisplay();
                var isLidClosed = currentLidState == lidState.closed;

                if (!hasExternal && isLidClosed)
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
            gracePeriodCts.Cancel();
            gracePeriodCts.Dispose();
            gracePeriodCts = null;
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
