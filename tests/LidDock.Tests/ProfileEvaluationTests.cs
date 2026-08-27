using Xunit;
using LidDock.Core.Models;
using LidDock.Core.Profiles;

namespace LidDock.Tests;

public class profileEvaluationTests
{
    [Fact]
    public void shouldAllowClamshellOnSmartDockedWithAdequateBattery()
    {
        var settings = new appSettings
        {
            enableClamshell = true,
            activeProfile = operationalProfileType.smartDocked,
            minimumBatteryThreshold = 20
        };

        var power = new systemPowerInfo(powerSourceType.battery, 50, false);
        var result = profileEvaluator.shouldAllowClamshell(settings, power, true);

        Assert.True(result);
    }

    [Fact]
    public void shouldDenyClamshellOnSmartDockedWithLowBattery()
    {
        var settings = new appSettings
        {
            enableClamshell = true,
            activeProfile = operationalProfileType.smartDocked,
            minimumBatteryThreshold = 20
        };

        var power = new systemPowerInfo(powerSourceType.battery, 15, false);
        var result = profileEvaluator.shouldAllowClamshell(settings, power, true);

        Assert.False(result);
    }

    [Fact]
    public void shouldDenyClamshellOnAcOnlyWhenRunningOnBattery()
    {
        var settings = new appSettings
        {
            enableClamshell = true,
            activeProfile = operationalProfileType.acOnly
        };

        var power = new systemPowerInfo(powerSourceType.battery, 95, false);
        var result = profileEvaluator.shouldAllowClamshell(settings, power, true);

        Assert.False(result);
    }

    [Fact]
    public void shouldAllowClamshellOnAcOnlyWhenConnectedToAc()
    {
        var settings = new appSettings
        {
            enableClamshell = true,
            activeProfile = operationalProfileType.acOnly
        };

        var power = new systemPowerInfo(powerSourceType.acPower, 95, true);
        var result = profileEvaluator.shouldAllowClamshell(settings, power, true);

        Assert.True(result);
    }

    [Fact]
    public void shouldAllowClamshellOnAlwaysClamshellEvenOnBattery()
    {
        var settings = new appSettings
        {
            enableClamshell = true,
            activeProfile = operationalProfileType.alwaysClamshell
        };

        var power = new systemPowerInfo(powerSourceType.battery, 10, false);
        var result = profileEvaluator.shouldAllowClamshell(settings, power, true);

        Assert.True(result);
    }

    [Fact]
    public void shouldDenyClamshellWhenNoExternalDisplayConnected()
    {
        var settings = new appSettings
        {
            enableClamshell = true,
            activeProfile = operationalProfileType.alwaysClamshell
        };

        var power = new systemPowerInfo(powerSourceType.acPower, 100, true);
        var result = profileEvaluator.shouldAllowClamshell(settings, power, false);

        Assert.False(result);
    }
}
