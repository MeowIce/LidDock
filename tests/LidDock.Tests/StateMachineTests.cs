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
    public bool lastAppliedToBattery { get; private set; }

    public void backupOriginalSettings()
    {
    }

    public bool applyClamshellAction(bool enableClamshell, bool applyToBattery)
    {
        isClamshellActive = enableClamshell;
        lastAppliedToBattery = applyToBattery;
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

    [Fact]
    public async Task shouldDisengageWhenAcPowerLostInAcOnlyProfile()
    {
        var fakePower = new fakePowerSchemeManager();
        using var machine = new clamshellStateMachine(fakePower);

        var settings = new appSettings
        {
            activeProfile = operationalProfileType.acOnly,
            disconnectDelaySeconds = 1,
            sleepOnDisconnectWithLidClosed = true
        };
        machine.updateSettings(settings);

        var externalDisplay = new physicalDisplayInfo("Dell Monitor", displayTechnology.displayPortExternal, false, false, "path1");
        machine.updateDisplays(new List<physicalDisplayInfo> { externalDisplay });
        machine.updatePowerInfo(new systemPowerInfo(powerSourceType.acPower, 100, false));
        machine.updateLidState(lidState.closed);

        Assert.Equal(clamshellState.clamshellActive, machine.getCurrentState());

        string? notificationTitle = null;
        string? notificationMessage = null;
        machine.onNotificationRequested += (title, msg) =>
        {
            notificationTitle = title;
            notificationMessage = msg;
        };

        machine.updatePowerInfo(new systemPowerInfo(powerSourceType.battery, 95, false));
        Assert.Equal(clamshellState.disconnectPending, machine.getCurrentState());
        Assert.NotNull(notificationTitle);
        Assert.Contains("AC Power", notificationTitle);
        Assert.NotNull(notificationMessage);

        await Task.Delay(1300);

        Assert.Equal(clamshellState.enteringSleep, machine.getCurrentState());
        Assert.False(fakePower.isClamshellActive);
        Assert.True(fakePower.isSleepTriggered);
    }

    [Fact]
    public void shouldApplyClamshellToBatteryForCustomProfileAboveThreshold()
    {
        var fakePower = new fakePowerSchemeManager();
        using var machine = new clamshellStateMachine(fakePower);

        var settings = new appSettings
        {
            activeProfile = operationalProfileType.custom,
            customRequireAcPower = false,
            minimumBatteryThreshold = 15
        };
        machine.updateSettings(settings);

        var externalDisplay = new physicalDisplayInfo("Dell Monitor", displayTechnology.displayPortExternal, false, false, "path1");
        machine.updateDisplays(new List<physicalDisplayInfo> { externalDisplay });
        machine.updatePowerInfo(new systemPowerInfo(powerSourceType.battery, 50, false));
        machine.updateLidState(lidState.closed);

        Assert.Equal(clamshellState.clamshellActive, machine.getCurrentState());
        Assert.True(fakePower.lastAppliedToBattery);
    }

    [Fact]
    public void shouldNotApplyClamshellToBatteryForCustomProfileRequiringAc()
    {
        var fakePower = new fakePowerSchemeManager();
        using var machine = new clamshellStateMachine(fakePower);

        var settings = new appSettings
        {
            activeProfile = operationalProfileType.custom,
            customRequireAcPower = true
        };
        machine.updateSettings(settings);

        var externalDisplay = new physicalDisplayInfo("Dell Monitor", displayTechnology.displayPortExternal, false, false, "path1");
        machine.updateDisplays(new List<physicalDisplayInfo> { externalDisplay });
        machine.updatePowerInfo(new systemPowerInfo(powerSourceType.acPower, 100, true));
        machine.updateLidState(lidState.closed);

        Assert.Equal(clamshellState.clamshellActive, machine.getCurrentState());
        Assert.False(fakePower.lastAppliedToBattery);
    }

    [Fact]
    public async Task shouldDisengageWhenAcPowerLostInCustomProfileRequiringAc()
    {
        var fakePower = new fakePowerSchemeManager();
        using var machine = new clamshellStateMachine(fakePower);

        var settings = new appSettings
        {
            activeProfile = operationalProfileType.custom,
            customRequireAcPower = true,
            disconnectDelaySeconds = 1,
            sleepOnDisconnectWithLidClosed = true
        };
        machine.updateSettings(settings);

        var externalDisplay = new physicalDisplayInfo("Dell Monitor", displayTechnology.displayPortExternal, false, false, "path1");
        machine.updateDisplays(new List<physicalDisplayInfo> { externalDisplay });
        machine.updatePowerInfo(new systemPowerInfo(powerSourceType.acPower, 100, false));
        machine.updateLidState(lidState.closed);

        Assert.Equal(clamshellState.clamshellActive, machine.getCurrentState());

        string? notificationTitle = null;
        string? notificationMessage = null;
        machine.onNotificationRequested += (title, msg) =>
        {
            notificationTitle = title;
            notificationMessage = msg;
        };

        machine.updatePowerInfo(new systemPowerInfo(powerSourceType.battery, 95, false));
        Assert.Equal(clamshellState.disconnectPending, machine.getCurrentState());
        Assert.NotNull(notificationTitle);
        Assert.Contains("AC Power", notificationTitle);
        Assert.DoesNotContain("AC Only", notificationMessage);

        await Task.Delay(1300);

        Assert.Equal(clamshellState.enteringSleep, machine.getCurrentState());
        Assert.False(fakePower.isClamshellActive);
        Assert.True(fakePower.isSleepTriggered);
    }

    [Fact]
    public async Task shouldStartGracePeriodWhenBatteryDropsBelowThresholdInCustomProfileWithoutAcRequirement()
    {
        var fakePower = new fakePowerSchemeManager();
        using var machine = new clamshellStateMachine(fakePower);

        var settings = new appSettings
        {
            activeProfile = operationalProfileType.custom,
            customRequireAcPower = false,
            minimumBatteryThreshold = 20,
            disconnectDelaySeconds = 1,
            sleepOnDisconnectWithLidClosed = true
        };
        machine.updateSettings(settings);

        var externalDisplay = new physicalDisplayInfo("Dell Monitor", displayTechnology.displayPortExternal, false, false, "path1");
        machine.updateDisplays(new List<physicalDisplayInfo> { externalDisplay });
        machine.updatePowerInfo(new systemPowerInfo(powerSourceType.acPower, 100, true));
        machine.updateLidState(lidState.closed);

        Assert.Equal(clamshellState.clamshellActive, machine.getCurrentState());

        string? notificationTitle = null;
        machine.onNotificationRequested += (title, _) => notificationTitle = title;

        machine.updatePowerInfo(new systemPowerInfo(powerSourceType.battery, 10, false));
        Assert.Equal(clamshellState.disconnectPending, machine.getCurrentState());
        Assert.Null(notificationTitle);

        await Task.Delay(1300);

        Assert.Equal(clamshellState.enteringSleep, machine.getCurrentState());
        Assert.False(fakePower.isClamshellActive);
        Assert.True(fakePower.isSleepTriggered);
    }

    [Fact]
    public void shouldEngageClamshellWithoutDisplayWhenDisplayNotRequired()
    {
        var fakePower = new fakePowerSchemeManager();
        using var machine = new clamshellStateMachine(fakePower);

        machine.updateSettings(new appSettings
        {
            activeProfile = operationalProfileType.smartDocked,
            requireExternalDisplay = false
        });

        machine.updateDisplays(new List<physicalDisplayInfo>());
        machine.updateLidState(lidState.closed);

        Assert.Equal(clamshellState.clamshellActive, machine.getCurrentState());
        Assert.True(fakePower.isClamshellActive);
    }

    [Fact]
    public void shouldStayActiveWhenDisplayDisconnectsIfDisplayNotRequired()
    {
        var fakePower = new fakePowerSchemeManager();
        using var machine = new clamshellStateMachine(fakePower);

        machine.updateSettings(new appSettings
        {
            activeProfile = operationalProfileType.smartDocked,
            requireExternalDisplay = false
        });

        var externalDisplay = new physicalDisplayInfo("Dell Monitor", displayTechnology.displayPortExternal, false, false, "path1");
        machine.updateDisplays(new List<physicalDisplayInfo> { externalDisplay });
        machine.updateLidState(lidState.closed);

        Assert.Equal(clamshellState.clamshellActive, machine.getCurrentState());

        machine.updateDisplays(new List<physicalDisplayInfo>());

        Assert.Equal(clamshellState.clamshellActive, machine.getCurrentState());
        Assert.True(fakePower.isClamshellActive);
    }

    [Fact]
    public async Task shouldReportGracePeriodCountdownWhileDisconnectPending()
    {
        var fakePower = new fakePowerSchemeManager();
        using var machine = new clamshellStateMachine(fakePower);

        var settings = new appSettings
        {
            activeProfile = operationalProfileType.acOnly,
            disconnectDelaySeconds = 2,
            sleepOnDisconnectWithLidClosed = true
        };
        machine.updateSettings(settings);

        var externalDisplay = new physicalDisplayInfo("Dell Monitor", displayTechnology.displayPortExternal, false, false, "path1");
        machine.updateDisplays(new List<physicalDisplayInfo> { externalDisplay });
        machine.updatePowerInfo(new systemPowerInfo(powerSourceType.acPower, 100, false));
        machine.updateLidState(lidState.closed);

        Assert.Equal(0, machine.getGraceRemainingSeconds());

        machine.updatePowerInfo(new systemPowerInfo(powerSourceType.battery, 95, false));
        Assert.Equal(clamshellState.disconnectPending, machine.getCurrentState());
        Assert.InRange(machine.getGraceRemainingSeconds(), 1, 2);

        await Task.Delay(2300);

        Assert.Equal(0, machine.getGraceRemainingSeconds());
    }
}
