using System;
using System.Threading;
using LidDock.Windows.Native;

namespace LidDock.Daemon;

public class keepAwakeManager : IDisposable
{
    private readonly object syncLock = new object();
    private CancellationTokenSource? cts;
    private DateTime activeUntilUtc = DateTime.MinValue;

    public bool isActive
    {
        get
        {
            lock (syncLock)
            {
                return activeUntilUtc > DateTime.UtcNow;
            }
        }
    }

    public DateTime getActiveUntilUtc()
    {
        lock (syncLock)
        {
            return activeUntilUtc;
        }
    }

    public void refresh(DateTime? untilUtc)
    {
        lock (syncLock)
        {
            var target = untilUtc.HasValue && untilUtc.Value > DateTime.UtcNow ? untilUtc.Value : (DateTime?)null;

            if (target == null)
            {
                stopInternal();
                activeUntilUtc = DateTime.MinValue;
                return;
            }

            if (cts != null && target.Value == activeUntilUtc)
            {
                return;
            }

            stopInternal();
            activeUntilUtc = target.Value;

            var newCts = new CancellationTokenSource();
            cts = newCts;
            var deadlineUtc = target.Value;
            var infinite = deadlineUtc == DateTime.MaxValue;

            var thread = new Thread(() =>
            {
                try
                {
                    nativeMethods.setThreadExecutionState(nativeConstants.esContinuous | nativeConstants.esSystemRequired);
                    if (infinite)
                    {
                        newCts.Token.WaitHandle.WaitOne(Timeout.InfiniteTimeSpan);
                    }
                    else
                    {
                        var remaining = deadlineUtc - DateTime.UtcNow;
                        if (remaining > TimeSpan.Zero)
                        {
                            newCts.Token.WaitHandle.WaitOne(remaining);
                        }
                    }
                }
                catch
                {
                }
                finally
                {
                    nativeMethods.setThreadExecutionState(nativeConstants.esContinuous);
                    lock (syncLock)
                    {
                        if (cts == newCts)
                        {
                            cts = null;
                            activeUntilUtc = DateTime.MinValue;
                        }
                    }

                    newCts.Dispose();
                }
            })
            {
                IsBackground = true,
                Name = "LidDock Amphetawin"
            };
            thread.Start();
        }
    }

    private void stopInternal()
    {
        var oldCts = cts;
        cts = null;

        if (oldCts != null)
        {
            try
            {
                oldCts.Cancel();
            }
            catch
            {
            }
        }
    }

    void IDisposable.Dispose() => dispose();

    public void dispose()
    {
        lock (syncLock)
        {
            stopInternal();
            activeUntilUtc = DateTime.MinValue;
        }
    }
}
