using System;
using System.Collections.Generic;

namespace LidDock.Diagnostics;

public record logEntry(
    DateTime timestamp,
    string level,
    string message);

public class diagnosticsLogger
{
    private static readonly diagnosticsLogger instanceValue = new diagnosticsLogger();
    public static diagnosticsLogger instance => instanceValue;

    private readonly object syncLock = new object();
    private readonly LinkedList<logEntry> entries = new LinkedList<logEntry>();
    private const int maxEntries = 500;

    public event Action<logEntry>? onNewLogEntry;

    public void logInfo(string message)
    {
        appendEntry("INFO", message);
    }

    public void logWarning(string message)
    {
        appendEntry("WARN", message);
    }

    public void logError(string message)
    {
        appendEntry("ERROR", message);
    }

    private void appendEntry(string level, string message)
    {
        var entry = new logEntry(DateTime.Now, level, message);
        lock (syncLock)
        {
            entries.AddLast(entry);
            if (entries.Count > maxEntries)
            {
                entries.RemoveFirst();
            }
        }
        onNewLogEntry?.Invoke(entry);
    }

    public IReadOnlyList<logEntry> getEntries()
    {
        lock (syncLock)
        {
            return new List<logEntry>(entries);
        }
    }
}
