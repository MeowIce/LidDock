using System;
using System.IO;
using System.Text;

namespace LidDock.Diagnostics;

public static class diagnosticsExporter
{
    public static string exportToFile(systemDiagnosticsSnapshot snapshot, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);
        var fileName = $"LidDock_Diagnostics_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        var fullPath = Path.Combine(targetDirectory, fileName);

        var sb = new StringBuilder();
        sb.AppendLine(snapshot.toFormattedText());
        sb.AppendLine();
        sb.AppendLine("Recent Diagnostics Event Logs");
        sb.AppendLine("----------------------------------------");

        var logs = diagnosticsLogger.instance.getEntries();
        foreach (var log in logs)
        {
            sb.AppendLine($"[{log.timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{log.level}] {log.message}");
        }

        File.WriteAllText(fullPath, sb.ToString(), Encoding.UTF8);
        return fullPath;
    }
}
