using System;
using LidDock.Core.Services;
using Xunit;

namespace LidDock.Tests;

public class AutoAmphetawinPolicyTests
{
    private static readonly DateTime now = new DateTime(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void startsWhenTargetAppLaunches()
    {
        var decision = autoAmphetawinPolicy.evaluate(true, true, false, false, null, null, now, false, 30);

        Assert.Equal(autoAmphetawinAction.start, decision.action);
        Assert.Equal(now.AddMinutes(30), decision.untilUtc);
    }

    [Fact]
    public void startsWithMaxValueWhenInfinite()
    {
        var decision = autoAmphetawinPolicy.evaluate(true, true, false, false, null, null, now, true, 30);

        Assert.Equal(autoAmphetawinAction.start, decision.action);
        Assert.Equal(DateTime.MaxValue, decision.untilUtc);
    }

    [Fact]
    public void doesNotHijackActiveManualLock()
    {
        var manualUntil = now.AddMinutes(10);
        var decision = autoAmphetawinPolicy.evaluate(true, true, false, false, manualUntil, null, now, false, 30);

        Assert.Equal(autoAmphetawinAction.none, decision.action);
    }

    [Fact]
    public void stopsWhenTargetAppExits()
    {
        var until = now.AddMinutes(30);
        var decision = autoAmphetawinPolicy.evaluate(true, false, true, true, until, until, now, false, 30);

        Assert.Equal(autoAmphetawinAction.stop, decision.action);
    }

    [Fact]
    public void doesNotStopManualReplacement()
    {
        var decision = autoAmphetawinPolicy.evaluate(true, false, true, true, now.AddMinutes(60), now.AddMinutes(30), now, false, 30);

        Assert.Equal(autoAmphetawinAction.none, decision.action);
    }

    [Fact]
    public void doesNotRearmAfterTimedExpiryWhileAppStillRuns()
    {
        var ownedUntil = now.AddMinutes(-1);
        var decision = autoAmphetawinPolicy.evaluate(true, true, true, true, ownedUntil, ownedUntil, now, false, 30);

        Assert.Equal(autoAmphetawinAction.none, decision.action);
    }

    [Fact]
    public void stopsWhenRuleDisabled()
    {
        var until = now.AddMinutes(30);
        var decision = autoAmphetawinPolicy.evaluate(false, true, true, true, until, until, now, false, 30);

        Assert.Equal(autoAmphetawinAction.stop, decision.action);
    }

    [Fact]
    public void staysQuietWhileAppKeepsRunning()
    {
        var until = now.AddMinutes(30);
        var decision = autoAmphetawinPolicy.evaluate(true, true, true, true, until, until, now, false, 30);

        Assert.Equal(autoAmphetawinAction.none, decision.action);
    }
}
