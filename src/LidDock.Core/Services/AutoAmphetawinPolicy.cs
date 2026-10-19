using System;

namespace LidDock.Core.Services;

public enum autoAmphetawinAction
{
    none,
    start,
    stop
}

public readonly struct autoAmphetawinDecision
{
    public autoAmphetawinDecision(autoAmphetawinAction action, DateTime? untilUtc = null)
    {
        this.action = action;
        this.untilUtc = untilUtc;
    }

    public autoAmphetawinAction action { get; }
    public DateTime? untilUtc { get; }
}

public static class autoAmphetawinPolicy
{
    public static autoAmphetawinDecision evaluate(
        bool ruleEnabled,
        bool targetRunning,
        bool wasTargetRunning,
        bool autoOwned,
        DateTime? currentUntilUtc,
        DateTime? ownedUntilUtc,
        DateTime now,
        bool infinite,
        int durationMinutes)
    {
        if (autoOwned)
        {
            if (currentUntilUtc != ownedUntilUtc)
            {
                return new autoAmphetawinDecision(autoAmphetawinAction.none);
            }

            if (!ruleEnabled || !targetRunning)
            {
                return new autoAmphetawinDecision(autoAmphetawinAction.stop);
            }

            if (!currentUntilUtc.HasValue || currentUntilUtc.Value <= now)
            {
                return new autoAmphetawinDecision(autoAmphetawinAction.none);
            }
        }

        if (!ruleEnabled || !targetRunning)
        {
            return new autoAmphetawinDecision(autoAmphetawinAction.none);
        }

        if (!wasTargetRunning)
        {
            var active = currentUntilUtc.HasValue && currentUntilUtc.Value > now;
            if (!active)
            {
                var until = infinite ? DateTime.MaxValue : now.AddMinutes(Math.Max(1, durationMinutes));
                return new autoAmphetawinDecision(autoAmphetawinAction.start, until);
            }
        }

        return new autoAmphetawinDecision(autoAmphetawinAction.none);
    }
}
