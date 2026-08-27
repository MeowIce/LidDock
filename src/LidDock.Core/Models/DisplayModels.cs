namespace LidDock.Core.Models;

public enum displayTechnology : uint
{
    other = 4294967295,
    hdmi = 0,
    dvi = 1,
    displayPortExternal = 10,
    displayPortEmbedded = 11,
    udiExternal = 12,
    udiEmbedded = 13,
    sdTvDongle = 14,
    miracast = 15,
    indirectWired = 16,
    indirectVirtual = 17,
    internalDisplay = 0x80000000
}

public record physicalDisplayInfo(
    string friendlyName,
    displayTechnology technology,
    bool isInternal,
    bool isVirtual,
    string devicePath)
{
    public string formattedTechnology => displayFormatters.formatDisplayTechnology(technology);
}
