using System;
using System.Collections.Generic;
using System.Text;
using LidDock.Core.Models;

namespace LidDock.Diagnostics;

public class systemDiagnosticsSnapshot
{
    public DateTime snapshotTime { get; set; } = DateTime.Now;
    public string osVersion { get; set; } = Environment.OSVersion.ToString();
    public bool is64BitOperatingSystem { get; set; } = Environment.Is64BitOperatingSystem;
    public clamshellState currentClamshellState { get; set; }
    public lidState currentLidState { get; set; }
    public systemPowerInfo currentPowerInfo { get; set; } = new systemPowerInfo(powerSourceType.unknown, 0, false);
    public uint? originalAcLidAction { get; set; }
    public uint? originalDcLidAction { get; set; }
    public uint? currentAcLidAction { get; set; }
    public IReadOnlyList<physicalDisplayInfo> connectedDisplays { get; set; } = Array.Empty<physicalDisplayInfo>();

    public string toFormattedText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("LidDock System Diagnostics Report");
        sb.AppendLine("----------------------------------------");
        sb.AppendLine($"Timestamp: {snapshotTime:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"OS Version: {osVersion} ({(is64BitOperatingSystem ? "64-bit" : "32-bit")})");
        sb.AppendLine($"Machine Name: {Environment.MachineName}");
        sb.AppendLine();
        sb.AppendLine("State & Power Status");
        sb.AppendLine("----------------------------------------");
        sb.AppendLine($"Clamshell State: {displayFormatters.formatClamshellState(currentClamshellState)}");
        sb.AppendLine($"Lid State: {displayFormatters.formatLidState(currentLidState)}");
        sb.AppendLine($"Power Source: {displayFormatters.formatPowerSource(currentPowerInfo)}");
        sb.AppendLine($"Battery Percent: {currentPowerInfo.batteryPercent}%");
        sb.AppendLine($"Is Charging: {currentPowerInfo.isCharging}");
        sb.AppendLine();
        sb.AppendLine("Windows Power Plan Settings");
        sb.AppendLine("----------------------------------------");
        sb.AppendLine($"Original AC Lid Action: {formatLidAction(originalAcLidAction)}");
        sb.AppendLine($"Original DC Lid Action: {formatLidAction(originalDcLidAction)}");
        sb.AppendLine($"Current AC Lid Action: {formatLidAction(currentAcLidAction)}");
        sb.AppendLine();
        sb.AppendLine($"Connected Displays Count: {connectedDisplays.Count}");
        sb.AppendLine("----------------------------------------");

        for (var i = 0; i < connectedDisplays.Count; i++)
        {
            var d = connectedDisplays[i];
            sb.AppendLine($"[{i + 1}] {d.friendlyName}");
            sb.AppendLine($"    Technology: {d.formattedTechnology}");
            sb.AppendLine($"    Is Internal: {d.isInternal}");
            sb.AppendLine($"    Is Virtual: {d.isVirtual}");
            sb.AppendLine($"    Device Path: {d.devicePath}");
        }

        return sb.ToString();
    }

    private static string formatLidAction(uint? action)
    {
        return action switch
        {
            0 => "0 (Do nothing)",
            1 => "1 (Sleep)",
            2 => "2 (Hibernate)",
            3 => "3 (Shut down)",
            null => "Unknown",
            _ => action.ToString()!
        };
    }
}
