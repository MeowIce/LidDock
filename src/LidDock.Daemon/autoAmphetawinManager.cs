using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using LidDock.Core.Models;
using LidDock.Core.Services;

namespace LidDock.Daemon;

public class autoAmphetawinManager : IDisposable
{
    private const int pollIntervalMs = 5000;
    private readonly object syncLock = new object();
    private readonly Action<DateTime?> apply;
    private Timer? timer;
    private appSettings settings = new appSettings();
    private bool wasTargetRunning;
    private bool autoOwned;
    private DateTime? ownedUntilUtc;
    private bool initialized;
    private bool lastRuleEnabled;
    private string? lastProcessName;
    private bool polling;
    private bool disposed;

    public autoAmphetawinManager(Action<DateTime?> apply)
    {
        this.apply = apply;
    }

    public void update(appSettings newSettings)
    {
        var stop = false;

        lock (syncLock)
        {
            if (disposed)
            {
                return;
            }

            var wasInitialized = initialized;
            var previousProcessName = lastProcessName;

            if (!initialized)
            {
                initialized = true;
                autoOwned = newSettings.autoAmphetawinOwned && newSettings.amphetawinUntilUtc.HasValue;
                ownedUntilUtc = autoOwned ? newSettings.amphetawinUntilUtc : null;
            }
            else if (!newSettings.autoAmphetawinOwned && autoOwned)
            {
                autoOwned = false;
                ownedUntilUtc = null;
                wasTargetRunning = true;
            }

            settings = newSettings;

            var ruleEnabled = newSettings.autoAmphetawinEnabled && !string.IsNullOrEmpty(newSettings.autoAmphetawinProcessName);
            var processName = newSettings.autoAmphetawinProcessName;
            var processChanged = wasInitialized && !string.Equals(previousProcessName, processName, StringComparison.OrdinalIgnoreCase);
            var fileOwned = newSettings.autoAmphetawinOwned && newSettings.amphetawinUntilUtc.HasValue;

            if (autoOwned && (processChanged || !ruleEnabled))
            {
                if (ownedUntilUtc.HasValue && newSettings.amphetawinUntilUtc == ownedUntilUtc)
                {
                    stop = true;
                }

                autoOwned = false;
                ownedUntilUtc = null;
            }

            if (!ruleEnabled && fileOwned && newSettings.amphetawinUntilUtc!.Value > DateTime.UtcNow)
            {
                stop = true;
            }

            if (!ruleEnabled)
            {
                wasTargetRunning = false;
                timer?.Change(Timeout.Infinite, Timeout.Infinite);
            }
            else
            {
                if (!lastRuleEnabled || processChanged)
                {
                    wasTargetRunning = false;
                }

                timer ??= new Timer(_ => poll(), null, Timeout.Infinite, Timeout.Infinite);
                timer.Change(0, pollIntervalMs);
            }

            lastRuleEnabled = ruleEnabled;
            lastProcessName = processName;
        }

        if (stop)
        {
            apply(null);
        }
    }

    private void poll()
    {
        lock (syncLock)
        {
            if (polling || disposed)
            {
                return;
            }

            polling = true;
        }

        try
        {
            appSettings snapshot;
            lock (syncLock)
            {
                snapshot = settings;
            }

            var running = isProcessRunning(snapshot.autoAmphetawinProcessName);
            autoAmphetawinDecision decision;
            var now = DateTime.UtcNow;

            lock (syncLock)
            {
                if (!ReferenceEquals(snapshot, settings))
                {
                    return;
                }

                decision = autoAmphetawinPolicy.evaluate(
                    snapshot.autoAmphetawinEnabled && !string.IsNullOrEmpty(snapshot.autoAmphetawinProcessName),
                    running,
                    wasTargetRunning,
                    autoOwned,
                    snapshot.amphetawinUntilUtc,
                    ownedUntilUtc,
                    now,
                    snapshot.autoAmphetawinInfinite,
                    snapshot.autoAmphetawinDurationMinutes);

                wasTargetRunning = running;

                if (decision.action == autoAmphetawinAction.start)
                {
                    autoOwned = true;
                    ownedUntilUtc = decision.untilUtc;
                }
                else if (decision.action == autoAmphetawinAction.stop)
                {
                    autoOwned = false;
                    ownedUntilUtc = null;
                }
                else if (autoOwned && (snapshot.amphetawinUntilUtc != ownedUntilUtc || !snapshot.amphetawinUntilUtc.HasValue || snapshot.amphetawinUntilUtc.Value <= now))
                {
                    autoOwned = false;
                    ownedUntilUtc = null;
                }
            }

            if (decision.action == autoAmphetawinAction.start)
            {
                apply(decision.untilUtc);
            }
            else if (decision.action == autoAmphetawinAction.stop)
            {
                apply(null);
            }
        }
        catch
        {
        }
        finally
        {
            lock (syncLock)
            {
                polling = false;
            }
        }
    }

    private static bool isProcessRunning(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        var name = Path.GetFileNameWithoutExtension(processName);
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        try
        {
            var matches = Process.GetProcessesByName(name);
            try
            {
                return matches.Length > 0;
            }
            finally
            {
                foreach (var match in matches)
                {
                    match.Dispose();
                }
            }
        }
        catch
        {
            return false;
        }
    }

    void IDisposable.Dispose() => dispose();

    public void dispose()
    {
        lock (syncLock)
        {
            disposed = true;
            timer?.Dispose();
            timer = null;
        }
    }
}
