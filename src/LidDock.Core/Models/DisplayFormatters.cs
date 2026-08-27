namespace LidDock.Core.Models;

public static class displayFormatters
{
    public static string formatClamshellState(clamshellState state)
    {
        return state switch
        {
            clamshellState.normalMode => "Standard (Undocked)",
            clamshellState.dockedLidOpen => "Docked (Lid Open)",
            clamshellState.clamshellActive => "Clamshell Active",
            clamshellState.disconnectPending => "Disconnect Grace Period",
            clamshellState.enteringSleep => "Entering Sleep Mode",
            clamshellState.suspended => "Suspended",
            clamshellState.errorFallback => "Safe Mode (Error Fallback)",
            _ => "Unknown"
        };
    }

    public static string formatLidState(lidState state)
    {
        return state switch
        {
            lidState.open => "Open",
            lidState.closed => "Closed",
            _ => "Unknown Sensor"
        };
    }

    public static string formatPowerSource(systemPowerInfo power)
    {
        var chargingText = power.isCharging ? " (Charging)" : string.Empty;
        return power.powerSource switch
        {
            powerSourceType.acPower => $"AC Connected{chargingText}",
            powerSourceType.battery => $"Battery ({power.batteryPercent}%){chargingText}",
            _ => $"Unknown ({power.batteryPercent}%)"
        };
    }

    public static string formatDisplayTechnology(displayTechnology tech)
    {
        return tech switch
        {
            displayTechnology.hdmi => "HDMI",
            displayTechnology.displayPortExternal => "DisplayPort",
            displayTechnology.displayPortEmbedded => "eDP (Internal)",
            displayTechnology.internalDisplay => "Internal Display",
            displayTechnology.udiExternal => "UDI External",
            displayTechnology.udiEmbedded => "UDI Embedded",
            displayTechnology.indirectWired => "USB-C / Thunderbolt Dock",
            displayTechnology.indirectVirtual => "Virtual Display Driver",
            displayTechnology.miracast => "Wireless (Miracast)",
            displayTechnology.dvi => "DVI",
            _ => "External Monitor"
        };
    }
}
