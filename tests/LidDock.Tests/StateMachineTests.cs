using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using LidDock.Core.Contracts;
using LidDock.Core.Models;
using LidDock.Core.StateMachine;

namespace LidDock.Tests;

public class fakePowerSchemeManager : iPowerSchemeManager
{
    public bool isClamshellActive { get; private set; }
    public bool isSleepTriggered { get; private set; }
    public int restoreCallCount { get; private set; }

    public void backupOriginalSettings()
    {
    }

    public bool applyClamshellAction(bool enableClamshell, bool applyToBattery)
    {
        isClamshellActive = enableClamshell;
        return true;
    }

    public void restoreOriginalSettings()
    {
        isClamshellActive = false;
        restoreCallCount++;
    }

    public void triggerImmediateSleep()
    {
        isSleepTriggered = true;
    }

    public uint? getOriginalAcLidAction() => 1u;
    public uint? getOriginalDcLidAction() => 1u;
    public uint? getCurrentAcLidAction() => isClamshellActive ? 0u : 1u;

    void IDisposable.Dispose() => dispose();

    public void dispose()
    {
    }
}

public class stateMachineTests
{
    [Fact]
    public void shouldTransitionToDockedLidOpenWhenDisplayConnected()
    {
        var fakePower = new fakePowerSchemeManager();
        using var machine = new clamshellStateMachine(fakePower);

        var displays = new List<physicalDisplayInfo>
        {
            new physicalDisplayInfo("Dell Monitor", displayTechnology.displayPortExternal, false, false, "path1")
        };

        machine.updateDisplays(displays);

        Assert.Equal(clamshellState.dockedLidOpen, machine.getCurrentState());
        Assert.True(fakePower.isClamshellActive);
    }

    [Fact]
    public void shouldTransitionToClamshellActiveWhenLidClosedWithDisplay()
    {
        var fakePower = new fakePowerSchemeManager();
        using var machine = new clamshellStateMachine(fakePower);

        var displays = new List<physicalDisplayInfo>
        {
            new physicalDisplayInfo("Dell Monitor", displayTechnology.displayPortExternal, false, false, "path1")
        };

        machine.updateDisplays(displays);
        machine.updateLidState(lidState.closed);

        Assert.Equal(clamshellState.clamshellActive, machine.getCurrentState());
        Assert.True(fakePower.isClamshellActive);
    }

    [Fact]
    public void shouldStartGracePeriodWhenDisplayDisconnectedWithLidClosed()
    {
        var fakePower = new fakePowerSchemeManager();
        using var machine = new clamshellStateMachine(fakePower);

        var displays = new List<physicalDisplayInfo>
        {
            new physicalDisplayInfo("Dell Monitor", displayTechnology.displayPortExternal, false, false, "path1")
        };

        machine.updateDisplays(displays);
        machine.updateLidState(lidState.closed);

        machine.updateDisplays(new List<physicalDisplayInfo>());

        Assert.Equal(clamshellState.disconnectPending, machine.getCurrentState());
    }

    [Fact]
    public void shouldCancelGracePeriodWhenDisplayReconnected()
    {
        var fakePower = new fakePowerSchemeManager();
        using var machine = new clamshellStateMachine(fakePower);

        var externalDisplay = new physicalDisplayInfo("Dell Monitor", displayTechnology.displayPortExternal, false, false, "path1");

        machine.updateDisplays(new List<physicalDisplayInfo> { externalDisplay });
        machine.updateLidState(lidState.closed);

        machine.updateDisplays(new List<physicalDisplayInfo>());
        Assert.Equal(clamshellState.disconnectPending, machine.getCurrentState());

        machine.updateDisplays(new List<physicalDisplayInfo> { externalDisplay });
        Assert.Equal(clamshellState.clamshellActive, machine.getCurrentState());
    }

    [Fact]
    public void shouldTransitionToNormalModeWhenLidOpenedAfterDisconnect()
    {
        var fakePower = new fakePowerSchemeManager();
        using var machine = new clamshellStateMachine(fakePower);

        var externalDisplay = new physicalDisplayInfo("Dell Monitor", displayTechnology.displayPortExternal, false, false, "path1");

        machine.updateDisplays(new List<physicalDisplayInfo> { externalDisplay });
        machine.updateLidState(lidState.closed);

        machine.updateDisplays(new List<physicalDisplayInfo>());
        Assert.Equal(clamshellState.disconnectPending, machine.getCurrentState());

        machine.updateLidState(lidState.open);
        Assert.Equal(clamshellState.normalMode, machine.getCurrentState());
        Assert.False(fakePower.isClamshellActive);
    }
}
