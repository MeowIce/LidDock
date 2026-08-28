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
    private const int maxEntries = 128;
    private readonly logEntry[] ringBuffer = new logEntry[maxEntries];
    private int writeIndex = 0;
    private int count = 0;

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
            ringBuffer[writeIndex] = entry;
            writeIndex = (writeIndex + 1) % maxEntries;
            if (count < maxEntries)
            {
                count++;
            }
        }
        onNewLogEntry?.Invoke(entry);
    }

    public IReadOnlyList<logEntry> getEntries()
    {
        lock (syncLock)
        {
            var result = new List<logEntry>(count);
            var startIndex = count < maxEntries ? 0 : writeIndex;
            for (var i = 0; i < count; i++)
            {
                var idx = (startIndex + i) % maxEntries;
                result.Add(ringBuffer[idx]);
            }
            return result;
        }
    }
}
