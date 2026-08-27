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
    private Guid currentActiveScheme;
    private bool isBackupTaken;
    private readonly object syncLock = new object();

    public void backupOriginalSettings()
    {
        lock (syncLock)
        {
            if (isBackupTaken)
            {
                return;
            }

            loadFromRegistryBackup();

            var result = nativeMethods.powerGetActiveScheme(IntPtr.Zero, out var schemePtr);
            if (result != nativeConstants.errorSuccess || schemePtr == IntPtr.Zero)
            {
                return;
            }

            currentActiveScheme = Marshal.PtrToStructure<Guid>(schemePtr);
            nativeMethods.localFree(schemePtr);

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
            if (!isBackupTaken)
            {
                backupOriginalSettings();
            }

            var subgroup = nativeConstants.guidSystemButtonSubgroup;
            var setting = nativeConstants.guidLidCloseAction;
            var targetAction = enableClamshell ? 0u : (originalAcLidAction ?? 1u);

            var acResult = nativeMethods.powerWriteAcValueIndex(
                IntPtr.Zero,
                ref currentActiveScheme,
                ref subgroup,
                ref setting,
                targetAction);

            if (applyToBattery)
            {
                var batteryAction = enableClamshell ? 0u : (originalDcLidAction ?? 1u);
                nativeMethods.powerWriteDcValueIndex(
                    IntPtr.Zero,
                    ref currentActiveScheme,
                    ref subgroup,
                    ref setting,
                    batteryAction);
            }
            else
            {
                var batteryAction = originalDcLidAction ?? 1u;
                nativeMethods.powerWriteDcValueIndex(
                    IntPtr.Zero,
                    ref currentActiveScheme,
                    ref subgroup,
                    ref setting,
                    batteryAction);
            }

            var applyResult = nativeMethods.powerSetActiveScheme(IntPtr.Zero, ref currentActiveScheme);
            return acResult == nativeConstants.errorSuccess && applyResult == nativeConstants.errorSuccess;
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
        restoreOriginalSettings();
    }
}
