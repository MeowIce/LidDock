using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using LidDock.Core.Contracts;
using LidDock.Windows.Native;

namespace LidDock.Windows.Power;

public class powerSchemeManager : iPowerSchemeManager
{
    private const string registryBackupKey = @"Software\LidDock\Backup";
    private uint? originalAcLidAction;
    private uint? originalDcLidAction;
    private uint? lastAppliedAcAction;
    private uint? lastAppliedDcAction;
    private Guid lastAppliedScheme;
    private Guid currentActiveScheme;
    private bool isBackupTaken;
    private readonly object syncLock = new object();
    public bool restoreOnDispose { get; set; } = false;

    private void refreshActiveScheme()
    {
        var result = nativeMethods.powerGetActiveScheme(IntPtr.Zero, out var schemePtr);
        if (result == nativeConstants.errorSuccess && schemePtr != IntPtr.Zero)
        {
            currentActiveScheme = Marshal.PtrToStructure<Guid>(schemePtr);
            nativeMethods.localFree(schemePtr);
        }
    }

    public void backupOriginalSettings()
    {
        lock (syncLock)
        {
            if (isBackupTaken)
            {
                return;
            }

            loadFromRegistryBackup();
            refreshActiveScheme();

            var subgroup = nativeConstants.guidSystemButtonSubgroup;
            var setting = nativeConstants.guidLidCloseAction;

            if (!originalAcLidAction.HasValue)
            {
                if (nativeMethods.powerReadAcValueIndex(IntPtr.Zero, ref currentActiveScheme, ref subgroup, ref setting, out var acVal) == nativeConstants.errorSuccess)
                {
                    originalAcLidAction = acVal;
                }
            }

            if (!originalDcLidAction.HasValue)
            {
                if (nativeMethods.powerReadDcValueIndex(IntPtr.Zero, ref currentActiveScheme, ref subgroup, ref setting, out var dcVal) == nativeConstants.errorSuccess)
                {
                    originalDcLidAction = dcVal;
                }
            }

            saveToRegistryBackup();
            isBackupTaken = true;
        }
    }

    public bool applyClamshellAction(bool enableClamshell, bool applyToBattery)
    {
        lock (syncLock)
        {
            refreshActiveScheme();
            if (!isBackupTaken)
            {
                backupOriginalSettings();
            }

            var targetAcAction = enableClamshell ? 0u : (originalAcLidAction ?? 1u);
            var targetDcAction = applyToBattery
                ? (enableClamshell ? 0u : (originalDcLidAction ?? 1u))
                : (originalDcLidAction ?? 1u);

            if (isBackupTaken &&
                currentActiveScheme == lastAppliedScheme &&
                lastAppliedAcAction == targetAcAction &&
                lastAppliedDcAction == targetDcAction)
            {
                return true;
            }

            var subgroup = nativeConstants.guidSystemButtonSubgroup;
            var setting = nativeConstants.guidLidCloseAction;

            var acResult = nativeMethods.powerWriteAcValueIndex(
                IntPtr.Zero,
                ref currentActiveScheme,
                ref subgroup,
                ref setting,
                targetAcAction);

            var dcResult = nativeMethods.powerWriteDcValueIndex(
                IntPtr.Zero,
                ref currentActiveScheme,
                ref subgroup,
                ref setting,
                targetDcAction);

            var applyResult = nativeMethods.powerSetActiveScheme(IntPtr.Zero, ref currentActiveScheme);
            var success = acResult == nativeConstants.errorSuccess &&
                          dcResult == nativeConstants.errorSuccess &&
                          applyResult == nativeConstants.errorSuccess;

            if (success)
            {
                lastAppliedAcAction = targetAcAction;
                lastAppliedDcAction = targetDcAction;
                lastAppliedScheme = currentActiveScheme;
            }

            return success;
        }
    }

    public void restoreOriginalSettings()
    {
        lock (syncLock)
        {
            if (!isBackupTaken && originalAcLidAction == null && originalDcLidAction == null)
            {
                loadFromRegistryBackup();
            }

            refreshActiveScheme();
            var subgroup = nativeConstants.guidSystemButtonSubgroup;
            var setting = nativeConstants.guidLidCloseAction;

            if (originalAcLidAction.HasValue)
            {
                nativeMethods.powerWriteAcValueIndex(
                    IntPtr.Zero,
                    ref currentActiveScheme,
                    ref subgroup,
                    ref setting,
                    originalAcLidAction.Value);
            }

            if (originalDcLidAction.HasValue)
            {
                nativeMethods.powerWriteDcValueIndex(
                    IntPtr.Zero,
                    ref currentActiveScheme,
                    ref subgroup,
                    ref setting,
                    originalDcLidAction.Value);
            }

            nativeMethods.powerSetActiveScheme(IntPtr.Zero, ref currentActiveScheme);
            clearRegistryBackup();
            isBackupTaken = false;
            lastAppliedAcAction = null;
            lastAppliedDcAction = null;
            lastAppliedScheme = Guid.Empty;
        }
    }

    public void triggerImmediateSleep()
    {
        nativeMethods.setSuspendState(false, false, false);
    }

    public uint? getOriginalAcLidAction() => originalAcLidAction;
    public uint? getOriginalDcLidAction() => originalDcLidAction;

    public uint? getCurrentAcLidAction()
    {
        refreshActiveScheme();
        var subgroup = nativeConstants.guidSystemButtonSubgroup;
        var setting = nativeConstants.guidLidCloseAction;
        if (nativeMethods.powerReadAcValueIndex(IntPtr.Zero, ref currentActiveScheme, ref subgroup, ref setting, out var acVal) == nativeConstants.errorSuccess)
        {
            return acVal;
        }
        return null;
    }

    private void saveToRegistryBackup()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(registryBackupKey);
            if (key != null)
            {
                if (originalAcLidAction.HasValue)
                {
                    key.SetValue("OriginalAcLidAction", (int)originalAcLidAction.Value, RegistryValueKind.DWord);
                }
                if (originalDcLidAction.HasValue)
                {
                    key.SetValue("OriginalDcLidAction", (int)originalDcLidAction.Value, RegistryValueKind.DWord);
                }
            }
        }
        catch
        {
        }
    }

    private void loadFromRegistryBackup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(registryBackupKey);
            if (key != null)
            {
                var acObj = key.GetValue("OriginalAcLidAction");
                if (acObj is int acInt)
                {
                    originalAcLidAction = (uint)acInt;
                }

                var dcObj = key.GetValue("OriginalDcLidAction");
                if (dcObj is int dcInt)
                {
                    originalDcLidAction = (uint)dcInt;
                }
            }
        }
        catch
        {
        }
    }

    private void clearRegistryBackup()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(registryBackupKey, false);
        }
        catch
        {
        }
    }

    void IDisposable.Dispose() => dispose();

    public void dispose()
    {
        if (restoreOnDispose)
        {
            restoreOriginalSettings();
        }
    }
}
