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
    private const int cmdProfileCustom = 1005;
    private const int cmdOpenSettings = 1006;
    private const int cmdOpenDiagnostics = 1007;
    private const int cmdExit = 1008;
    private const int cmdAbout = 1009;
    private const int cmdAmphetawin15Minutes = 1010;
    private const int cmdAmphetawin30Minutes = 1011;
    private const int cmdAmphetawin1Hour = 1012;
    private const int cmdAmphetawinInfinite = 1013;

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
            appendMenu(hMenu, enableFlags, (IntPtr)cmdToggleClamshell, "Use LidDock");

            var hProfileSub = createPopupMenu();
            var smartFlags = mfString | (settings.activeProfile == operationalProfileType.smartDocked ? mfChecked : 0);
            var acFlags = mfString | (settings.activeProfile == operationalProfileType.acOnly ? mfChecked : 0);
            var alwaysFlags = mfString | (settings.activeProfile == operationalProfileType.alwaysClamshell ? mfChecked : 0);
            var customFlags = mfString | (settings.activeProfile == operationalProfileType.custom ? mfChecked : 0);

            appendMenu(hProfileSub, smartFlags, (IntPtr)cmdProfileSmartDocked, "Smart Docked (Recommended)");
            appendMenu(hProfileSub, acFlags, (IntPtr)cmdProfileAcOnly, "AC Power Only");
            appendMenu(hProfileSub, alwaysFlags, (IntPtr)cmdProfileAlwaysClamshell, "Always Clamshell");
            appendMenu(hProfileSub, customFlags, (IntPtr)cmdProfileCustom, "Custom");

            appendMenu(hMenu, mfPopup, hProfileSub, "Profiles");

            var hAmphetawinSub = createPopupMenu();
            var hNewSessionSub = createPopupMenu();
            appendMenu(hNewSessionSub, mfString, (IntPtr)cmdAmphetawin15Minutes, "15 minutes");
            appendMenu(hNewSessionSub, mfString, (IntPtr)cmdAmphetawin30Minutes, "30 minutes");
            appendMenu(hNewSessionSub, mfString, (IntPtr)cmdAmphetawin1Hour, "1 hour");
            appendMenu(hNewSessionSub, mfString, (IntPtr)cmdAmphetawinInfinite, "Infinite");
            appendMenu(hAmphetawinSub, mfPopup, hNewSessionSub, "New Session");
            appendMenu(hMenu, mfPopup, hAmphetawinSub, "Amphetawin");

            appendMenu(hMenu, mfSeparator, IntPtr.Zero, string.Empty);

            appendMenu(hMenu, mfString, (IntPtr)cmdOpenSettings, "Settings...");
            appendMenu(hMenu, mfString, (IntPtr)cmdOpenDiagnostics, "Diagnostics...");
            appendMenu(hMenu, mfString, (IntPtr)cmdAbout, $"About LidDock v{getVersionString()}");
            appendMenu(hMenu, mfSeparator, IntPtr.Zero, string.Empty);
            appendMenu(hMenu, mfString, (IntPtr)cmdExit, "Exit LidDock");

            getCursorPos(out var pt);
            setForegroundWindow(hWnd);

            var cmd = trackPopupMenuEx(hMenu, tpmRightButton | tpmReturnCmd, pt.x, pt.y, hWnd, IntPtr.Zero);
            handleCommand(cmd, stateMachine, onExitRequested);
        }
        finally
        {
            destroyMenu(hMenu);
        }
    }

    private static void handleCommand(
        int cmd,
        clamshellStateMachine stateMachine,
        Action onExitRequested)
    {
        switch (cmd)
        {
            case cmdToggleClamshell:
                changeSettings(stateMachine, s => s.enableClamshell = !s.enableClamshell);
                break;

            case cmdProfileSmartDocked:
                changeSettings(stateMachine, s => s.activeProfile = operationalProfileType.smartDocked);
                break;

            case cmdProfileAcOnly:
                changeSettings(stateMachine, s => s.activeProfile = operationalProfileType.acOnly);
                break;

            case cmdProfileAlwaysClamshell:
                changeSettings(stateMachine, s => s.activeProfile = operationalProfileType.alwaysClamshell);
                break;

            case cmdProfileCustom:
                changeSettings(stateMachine, s => s.activeProfile = operationalProfileType.custom);
                break;

            case cmdAmphetawin15Minutes:
                startAmphetawinSession(stateMachine, 15);
                break;

            case cmdAmphetawin30Minutes:
                startAmphetawinSession(stateMachine, 30);
                break;

            case cmdAmphetawin1Hour:
                startAmphetawinSession(stateMachine, 60);
                break;

            case cmdAmphetawinInfinite:
                startAmphetawinSession(stateMachine, null);
                break;

            case cmdOpenSettings:
                launchUi(string.Empty);
                break;

            case cmdOpenDiagnostics:
                launchUi("--diagnostics");
                break;

            case cmdAbout:
                showAbout();
                break;

            case cmdExit:
                onExitRequested();
                break;
        }
    }

    private static void changeSettings(clamshellStateMachine stateMachine, Action<appSettings> mutate)
    {
        var settings = settingsManager.loadSettings();
        mutate(settings);
        stateMachine.updateSettings(settings);
        settingsManager.saveSettings(settings);
    }

    private static void startAmphetawinSession(clamshellStateMachine stateMachine, int? minutes)
    {
        changeSettings(stateMachine, s =>
        {
            s.amphetawinInfinite = !minutes.HasValue;
            s.amphetawinUntilUtc = minutes.HasValue
                ? DateTime.UtcNow.AddMinutes(minutes.Value)
                : DateTime.MaxValue;
            s.autoAmphetawinOwned = false;
        });
    }

    private static string getVersionString()
    {
        var version = typeof(nativeTrayMenu).Assembly.GetName().Version;
        return version == null ? "?" : $"{version.Major}.{version.Minor}.{version.Build}";
    }

    private static void showAbout()
    {
        nativeMethods.messageBox(
            IntPtr.Zero,
            $"LidDock v{getVersionString()}\n\nSmart Clamshell Mode for Windows 11.\nhttps://github.com/MeowIce/LidDock",
            "About LidDock",
            0x00000040);
    }

    public static void launchUi(string argument)
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            var localUiPath = Path.Combine(baseDir, "LidDock.UI.exe");
            if (File.Exists(localUiPath))
            {
                startProcess(localUiPath, argument);
                return;
            }

            localUiPath = Path.Combine(baseDir, "LidDock.App.exe");
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
            var windowTitle = argument == "--diagnostics" ? "LidDock Diagnostics" : "LidDock Settings";
            var existingWindow = nativeMethods.findWindow(null, windowTitle);
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
