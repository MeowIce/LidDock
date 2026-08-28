using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using LidDock.Core.Models;
using LidDock.Core.StateMachine;
using LidDock.Windows.Native;

namespace LidDock.Daemon;

public static class nativeTrayMenu
{
    private const uint mfString = 0x00000000;
    private const uint mfGrayed = 0x00000001;
    private const uint mfDisabled = 0x00000002;
    private const uint mfChecked = 0x00000008;
    private const uint mfPopup = 0x00000010;
    private const uint mfSeparator = 0x00000800;

    private const uint tpmRightButton = 0x0002;
    private const uint tpmReturnCmd = 0x0100;

    private const int cmdToggleClamshell = 1001;
    private const int cmdProfileSmartDocked = 1002;
    private const int cmdProfileAcOnly = 1003;
    private const int cmdProfileAlwaysClamshell = 1004;
    private const int cmdOpenSettings = 1006;
    private const int cmdOpenDiagnostics = 1007;
    private const int cmdExit = 1008;

    [StructLayout(LayoutKind.Sequential)]
    private struct point
    {
        public int x;
        public int y;
    }

    [DllImport("user32.dll", EntryPoint = "CreatePopupMenu")]
    private static extern IntPtr createPopupMenu();

    [DllImport("user32.dll", EntryPoint = "AppendMenuW", CharSet = CharSet.Unicode)]
    private static extern bool appendMenu(
        IntPtr hMenu,
        uint uFlags,
        IntPtr uIdNewItem,
        string lpNewItem);

    [DllImport("user32.dll", EntryPoint = "DestroyMenu")]
    private static extern bool destroyMenu(IntPtr hMenu);

    [DllImport("user32.dll", EntryPoint = "GetCursorPos")]
    private static extern bool getCursorPos(out point lpPoint);

    [DllImport("user32.dll", EntryPoint = "SetForegroundWindow")]
    private static extern bool setForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", EntryPoint = "TrackPopupMenuEx")]
    private static extern int trackPopupMenuEx(
        IntPtr hMenu,
        uint uFlags,
        int x,
        int y,
        IntPtr hWnd,
        IntPtr lpTpmParams);

    public static void showContextMenu(
        IntPtr hWnd,
        appSettings settings,
        clamshellState state,
        string monitorName,
        lidState lid,
        systemPowerInfo power,
        clamshellStateMachine stateMachine,
        Action onExitRequested)
    {
        var hMenu = createPopupMenu();
        if (hMenu == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var stateStr = displayFormatters.formatClamshellState(state);
            var lidStr = displayFormatters.formatLidState(lid);
            var powerStr = displayFormatters.formatPowerSource(power);

            appendMenu(hMenu, mfString | mfDisabled, IntPtr.Zero, $"Status: {stateStr}");
            appendMenu(hMenu, mfString | mfDisabled, IntPtr.Zero, $"Display: {monitorName}");
            appendMenu(hMenu, mfString | mfDisabled, IntPtr.Zero, $"Lid: {lidStr}");
            appendMenu(hMenu, mfString | mfDisabled, IntPtr.Zero, $"Power: {powerStr}");
            appendMenu(hMenu, mfSeparator, IntPtr.Zero, string.Empty);

            var enableFlags = mfString | (settings.enableClamshell ? mfChecked : 0);
            appendMenu(hMenu, enableFlags, (IntPtr)cmdToggleClamshell, "Enable Smart Clamshell Mode");

            var hProfileSub = createPopupMenu();
            var smartFlags = mfString | (settings.activeProfile == operationalProfileType.smartDocked ? mfChecked : 0);
            var acFlags = mfString | (settings.activeProfile == operationalProfileType.acOnly ? mfChecked : 0);
            var alwaysFlags = mfString | (settings.activeProfile == operationalProfileType.alwaysClamshell ? mfChecked : 0);

            appendMenu(hProfileSub, smartFlags, (IntPtr)cmdProfileSmartDocked, "Smart Docked (Recommended)");
            appendMenu(hProfileSub, acFlags, (IntPtr)cmdProfileAcOnly, "AC Power Only");
            appendMenu(hProfileSub, alwaysFlags, (IntPtr)cmdProfileAlwaysClamshell, "Always Clamshell");

            appendMenu(hMenu, mfPopup, hProfileSub, "Profiles");
            appendMenu(hMenu, mfSeparator, IntPtr.Zero, string.Empty);

            appendMenu(hMenu, mfString, (IntPtr)cmdOpenSettings, "Settings...");
            appendMenu(hMenu, mfString, (IntPtr)cmdOpenDiagnostics, "Diagnostics...");
            appendMenu(hMenu, mfSeparator, IntPtr.Zero, string.Empty);
            appendMenu(hMenu, mfString, (IntPtr)cmdExit, "Exit LidDock");

            getCursorPos(out var pt);
            setForegroundWindow(hWnd);

            var cmd = trackPopupMenuEx(hMenu, tpmRightButton | tpmReturnCmd, pt.x, pt.y, hWnd, IntPtr.Zero);
            handleCommand(cmd, settings, stateMachine, onExitRequested);
        }
        finally
        {
            destroyMenu(hMenu);
        }
    }

    private static void handleCommand(
        int cmd,
        appSettings settings,
        clamshellStateMachine stateMachine,
        Action onExitRequested)
    {
        switch (cmd)
        {
            case cmdToggleClamshell:
                settings.enableClamshell = !settings.enableClamshell;
                stateMachine.updateSettings(settings);
                settingsManager.saveSettings(settings);
                break;

            case cmdProfileSmartDocked:
                settings.activeProfile = operationalProfileType.smartDocked;
                stateMachine.updateSettings(settings);
                settingsManager.saveSettings(settings);
                break;

            case cmdProfileAcOnly:
                settings.activeProfile = operationalProfileType.acOnly;
                stateMachine.updateSettings(settings);
                settingsManager.saveSettings(settings);
                break;

            case cmdProfileAlwaysClamshell:
                settings.activeProfile = operationalProfileType.alwaysClamshell;
                stateMachine.updateSettings(settings);
                settingsManager.saveSettings(settings);
                break;

            case cmdOpenSettings:
                launchUi(string.Empty);
                break;

            case cmdOpenDiagnostics:
                launchUi("--diagnostics");
                break;

            case cmdExit:
                onExitRequested();
                break;
        }
    }

    public static void launchUi(string argument)
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            var localUiPath = Path.Combine(baseDir, "LidDock.App.exe");
            if (File.Exists(localUiPath))
            {
                startProcess(localUiPath, argument);
                return;
            }

            var targetDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LidDock");

            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            var targetExePath = Path.Combine(targetDir, "LidDock.UI.exe");
            ensureUiExtracted(targetExePath);

            if (File.Exists(targetExePath))
            {
                startProcess(targetExePath, argument);
            }
        }
        catch
        {
        }
    }

    private static void ensureUiExtracted(string targetExePath)
    {
        try
        {
            var assembly = typeof(nativeTrayMenu).Assembly;
            var resourceNames = assembly.GetManifestResourceNames();
            var resourceName = resourceNames.FirstOrDefault(n => n.EndsWith("LidDock.UI.exe", StringComparison.OrdinalIgnoreCase));
            if (resourceName == null)
            {
                return;
            }

            using var resourceStream = assembly.GetManifestResourceStream(resourceName);
            if (resourceStream == null)
            {
                return;
            }

            if (File.Exists(targetExePath))
            {
                var daemonPath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(daemonPath) && File.Exists(daemonPath))
                {
                    var daemonInfo = new FileInfo(daemonPath);
                    var uiInfo = new FileInfo(targetExePath);
                    if (uiInfo.LastWriteTimeUtc >= daemonInfo.LastWriteTimeUtc && uiInfo.Length == resourceStream.Length)
                    {
                        return;
                    }
                }
            }

            var tempPath = targetExePath + ".tmp";
            using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                resourceStream.CopyTo(fileStream);
            }

            File.Move(tempPath, targetExePath, true);
        }
        catch
        {
        }
    }

    private static void startProcess(string exePath, string argument)
    {
        try
        {
            var existingWindow = nativeMethods.findWindow(null, "LidDock Settings");
            if (existingWindow != IntPtr.Zero)
            {
                nativeMethods.showWindow(existingWindow, 9);
                nativeMethods.setForegroundWindow(existingWindow);
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = argument,
                UseShellExecute = false,
                CreateNoWindow = false
            };
            Process.Start(startInfo);
        }
        catch
        {
        }
    }
}
