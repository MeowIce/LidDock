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

    [Fact]
    public void shouldDenyClamshellOnCustomRequireAcWhenRunningOnBattery()
    {
        var settings = new appSettings
        {
            enableClamshell = true,
            activeProfile = operationalProfileType.custom,
            customRequireAcPower = true,
            minimumBatteryThreshold = 15
        };

        var power = new systemPowerInfo(powerSourceType.battery, 95, false);
        var result = profileEvaluator.shouldAllowClamshell(settings, power, true);

        Assert.False(result);
    }

    [Fact]
    public void shouldAllowClamshellOnCustomRequireAcWhenConnectedToAc()
    {
        var settings = new appSettings
        {
            enableClamshell = true,
            activeProfile = operationalProfileType.custom,
            customRequireAcPower = true,
            minimumBatteryThreshold = 15
        };

        var power = new systemPowerInfo(powerSourceType.acPower, 5, false);
        var result = profileEvaluator.shouldAllowClamshell(settings, power, true);

        Assert.True(result);
    }

    [Fact]
    public void shouldAllowClamshellOnCustomWithoutAcRequirementWhenBatteryAtOrAboveThreshold()
    {
        var settings = new appSettings
        {
            enableClamshell = true,
            activeProfile = operationalProfileType.custom,
            customRequireAcPower = false,
            minimumBatteryThreshold = 50
        };

        var power = new systemPowerInfo(powerSourceType.battery, 50, false);
        var result = profileEvaluator.shouldAllowClamshell(settings, power, true);

        Assert.True(result);
    }

    [Fact]
    public void shouldDenyClamshellOnCustomWithoutAcRequirementWhenBatteryBelowThreshold()
    {
        var settings = new appSettings
        {
            enableClamshell = true,
            activeProfile = operationalProfileType.custom,
            customRequireAcPower = false,
            minimumBatteryThreshold = 20
        };

        var power = new systemPowerInfo(powerSourceType.battery, 15, false);
        var result = profileEvaluator.shouldAllowClamshell(settings, power, true);

        Assert.False(result);
    }

    [Fact]
    public void shouldAllowClamshellOnCustomWithoutAcRequirementWhenOnAcBelowThreshold()
    {
        var settings = new appSettings
        {
            enableClamshell = true,
            activeProfile = operationalProfileType.custom,
            customRequireAcPower = false,
            minimumBatteryThreshold = 20
        };

        var power = new systemPowerInfo(powerSourceType.acPower, 10, false);
        var result = profileEvaluator.shouldAllowClamshell(settings, power, true);

        Assert.True(result);
    }

    [Fact]
    public void shouldDenyClamshellOnCustomWhenNoExternalDisplayConnected()
    {
        var settings = new appSettings
        {
            enableClamshell = true,
            activeProfile = operationalProfileType.custom
        };

        var power = new systemPowerInfo(powerSourceType.acPower, 100, true);
        var result = profileEvaluator.shouldAllowClamshell(settings, power, false);

        Assert.False(result);
    }

    [Fact]
    public void requiresAcPowerReflectsProfileRules()
    {
        var smartDocked = new appSettings { activeProfile = operationalProfileType.smartDocked };
        var acOnly = new appSettings { activeProfile = operationalProfileType.acOnly };
        var alwaysClamshell = new appSettings { activeProfile = operationalProfileType.alwaysClamshell };
        var customWithAc = new appSettings { activeProfile = operationalProfileType.custom, customRequireAcPower = true };
        var customWithoutAc = new appSettings { activeProfile = operationalProfileType.custom, customRequireAcPower = false };

        Assert.False(profileEvaluator.requiresAcPower(smartDocked));
        Assert.True(profileEvaluator.requiresAcPower(acOnly));
        Assert.False(profileEvaluator.requiresAcPower(alwaysClamshell));
        Assert.True(profileEvaluator.requiresAcPower(customWithAc));
        Assert.False(profileEvaluator.requiresAcPower(customWithoutAc));
    }

    [Fact]
    public void shouldAllowClamshellWithoutExternalDisplayWhenDisplayNotRequired()
    {
        var settings = new appSettings
        {
            enableClamshell = true,
            activeProfile = operationalProfileType.alwaysClamshell,
            requireExternalDisplay = false
        };

        var power = new systemPowerInfo(powerSourceType.acPower, 100, true);
        var result = profileEvaluator.shouldAllowClamshell(settings, power, false);

        Assert.True(result);
    }

    [Fact]
    public void shouldKeepPowerRulesWhenDisplayNotRequired()
    {
        var settings = new appSettings
        {
            enableClamshell = true,
            activeProfile = operationalProfileType.smartDocked,
            minimumBatteryThreshold = 20,
            requireExternalDisplay = false
        };

        Assert.True(profileEvaluator.shouldAllowClamshell(settings, new systemPowerInfo(powerSourceType.battery, 50, false), false));
        Assert.False(profileEvaluator.shouldAllowClamshell(settings, new systemPowerInfo(powerSourceType.battery, 15, false), false));
    }
}
