namespace LidDock.Core.Models;

public enum clamshellState
{
    normalMode,
    dockedLidOpen,
    clamshellActive,
    disconnectPending,
    enteringSleep,
    suspended,
    errorFallback
}
