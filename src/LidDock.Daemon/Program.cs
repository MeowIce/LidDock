using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using LidDock.Core.Models;
using LidDock.Core.Services;
using LidDock.Core.StateMachine;
using LidDock.Diagnostics;
using LidDock.Windows.Native;
using LidDock.Windows.Power;
using LidDock.Windows.Watchers;

namespace LidDock.Daemon;

internal static class Program
{
    private static Mutex? singleInstanceMutex;
    private static powerSchemeManager? powerManager;
    private static clamshellStateMachine? stateMachine;
    private static displayWatcher? displayWatcher;
    private static lidWatcher? lidWatcher;
    private static powerWatcher? powerWatcher;
    private static nativeMessageWindow? nativeWindow;
    private static daemonTrayManager? trayManager;
    private static keepAwakeManager? keepAwake;
    private static autoAmphetawinManager? autoAmphetawin;
    private static appSettings appSettings = new appSettings();
    private static FileSystemWatcher? settingsWatcher;
    private static CancellationTokenSource? workingSetTrimCts;
    private static Timer? maintenanceTimer;
    private static readonly object syncRoot = new object();

    [STAThread]
    static void Main(string[] args)
    {
        if (args.Contains("--uninstall"))
        {
            performUninstall(args.Contains("--silent"), args.Contains("--keep-settings"));
            return;
        }

        singleInstanceMutex = new Mutex(true, "LidDock_Daemon_Mutex", out var createdNew);
        if (!createdNew)
        {
            if (!args.Contains("--minimized"))
            {
                nativeTrayMenu.launchUi(string.Empty);
            }
            return;
        }

        appSettings = settingsManager.loadSettings();

        try
        {
            settingsManager.ensurePermanentInstallation();
            var permanentPath = settingsManager.getPermanentDaemonPath();
            using var liddockKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\LidDock");
            liddockKey?.SetValue("DaemonPath", permanentPath);
            settingsManager.updateAutoStartRegistry(appSettings.startWithWindows);
        }
        catch
        {
        }

        powerManager = new powerSchemeManager();
        powerManager.backupOriginalSettings();
        powerManager.restoreOnDispose = true;

        stateMachine = new clamshellStateMachine(powerManager);
        displayWatcher = new displayWatcher();
        lidWatcher = new lidWatcher();
        powerWatcher = new powerWatcher();
        nativeWindow = new nativeMessageWindow();
        trayManager = new daemonTrayManager();
        keepAwake = new keepAwakeManager();
        autoAmphetawin = new autoAmphetawinManager(applyAutoAmphetawin);

        nativeWindow.initialize();
        trayManager.initialize(nativeWindow.handle);

        nativeWindow.onGenericMessage += (msg, wParam, lParam) =>
        {
            trayManager?.handleWindowMessage(msg, wParam, lParam, appSettings, stateMachine, exitDaemon);
        };

        trayManager.onInteractionCompleted += () => scheduleWorkingSetTrim(1500);

        hookEvents();
        startSettingsWatcher();

        refreshKeepAwake();
        refreshSystemState();
        autoAmphetawin.update(appSettings);

        var isMinimized = args.Contains("--minimized");
        if (!isMinimized)
        {
            Task.Run(() =>
            {
                try
                {
                    nativeTrayMenu.launchUi(string.Empty);
                }
                catch
                {
                }
            });
        }

        scheduleWorkingSetTrim(2000);
        maintenanceTimer = new Timer(_ => trimWorkingSet(), null, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));

        if (appSettings.autoCheckForUpdates)
        {
            scheduleBackgroundUpdateCheck();
        }

        int ret;
        while ((ret = nativeMethods.getMessage(out var msg, IntPtr.Zero, 0, 0)) != 0)
        {
            if (ret == -1)
            {
                break;
            }

            nativeMethods.translateMessage(ref msg);
            nativeMethods.dispatchMessage(ref msg);
        }

        cleanup();
    }

    private static void hookEvents()
    {
        if (nativeWindow == null || displayWatcher == null || lidWatcher == null || powerWatcher == null || stateMachine == null || trayManager == null)
        {
            return;
        }

        nativeWindow.onSettingsChangedMessage += () =>
        {
            if (settingsWatcher == null)
            {
                onSettingsFileChanged();
            }
        };

        nativeWindow.onDisplayChangedMessage += () =>
        {
            diagnosticsLogger.instance.logInfo("Daemon Event: WM_DISPLAYCHANGE");
            displayWatcher.notifyDisplayConfigurationChanged();
        };

        nativeWindow.onLidStateChangedMessage += isOpen =>
        {
            diagnosticsLogger.instance.logInfo($"Daemon Event: Lid {(isOpen ? "Open" : "Closed")}");
            lidWatcher.notifyLidStateChanged(isOpen);
            stateMachine.updateLidState(isOpen ? lidState.open : lidState.closed);
        };

        nativeWindow.onPowerSourceChangedMessage += () =>
        {
            diagnosticsLogger.instance.logInfo("Daemon Event: Power source changed");
            powerWatcher.notifyPowerStatusChanged();
        };

        nativeWindow.onSystemResumedMessage += () =>
        {
            diagnosticsLogger.instance.logInfo("Daemon Event: System resumed");
            refreshSystemState();
        };

        nativeWindow.onTaskbarCreatedMessage += () =>
        {
            trayManager.recreateIcon();
        };

        displayWatcher.onDisplaysChanged += displays =>
        {
            var previousState = stateMachine.getCurrentState();
            stateMachine.updateDisplays(displays);
            if (stateMachine.getCurrentState() == previousState)
            {
                updateTrayDisplay();
            }
        };

        powerWatcher.onPowerStatusChanged += power =>
        {
            var previousState = stateMachine.getCurrentState();
            stateMachine.updatePowerInfo(power);
            if (stateMachine.getCurrentState() == previousState)
            {
                updateTrayDisplay();
            }
        };

        stateMachine.onStateChanged += state =>
        {
            updateTrayDisplay();
        };

        stateMachine.onNotificationRequested += (title, message) =>
        {
            diagnosticsLogger.instance.logWarning($"{title}: {message}");
            trayManager?.showToastNotification(title, message, true);
        };
    }

    private static void refreshKeepAwake()
    {
        if (keepAwake == null)
        {
            return;
        }

        var wasActive = keepAwake.isActive;
        keepAwake.refresh(appSettings.amphetawinUntilUtc);
        var isActive = keepAwake.isActive;

        if (!wasActive && isActive)
        {
            var activeUntilUtc = keepAwake.getActiveUntilUtc();
            if (activeUntilUtc == DateTime.MaxValue)
            {
                if (appSettings.autoAmphetawinOwned)
                {
                    var appName = string.IsNullOrEmpty(appSettings.autoAmphetawinExecutablePath)
                        ? "the selected app"
                        : Path.GetFileName(appSettings.autoAmphetawinExecutablePath);
                    diagnosticsLogger.instance.logInfo($"Daemon Event: Amphetawin active indefinitely for {appName}");
                    trayManager?.showToastNotification(
                        "Amphetawin",
                        $"Automatic sleep stays disabled indefinitely while {appName} is running.",
                        false);
                }
                else
                {
                    diagnosticsLogger.instance.logInfo("Daemon Event: Amphetawin active indefinitely");
                    trayManager?.showToastNotification(
                        "Amphetawin",
                        "Automatic sleep is disabled indefinitely. Turn off Amphetawin to allow sleep.",
                        false);
                }
            }
            else
            {
                var untilText = activeUntilUtc.ToLocalTime().ToString("HH:mm");
                diagnosticsLogger.instance.logInfo($"Daemon Event: Amphetawin active until {untilText}");
                trayManager?.showToastNotification(
                    "Amphetawin",
                    $"Automatic sleep is disabled. The system will stay awake until {untilText}.",
                    false);
            }
        }
    }

    private static void refreshSystemState()
    {
        if (nativeWindow == null || displayWatcher == null || lidWatcher == null || powerWatcher == null || stateMachine == null)
        {
            return;
        }

        nativeWindow.refreshLidNotification();
        displayWatcher.notifyDisplayConfigurationChanged();
        powerWatcher.notifyPowerStatusChanged();
        stateMachine.updateLidState(lidWatcher.queryLidState());
        updateTrayDisplay();
    }

    private static void updateTrayDisplay()
    {
        if (trayManager == null || stateMachine == null || displayWatcher == null || lidWatcher == null || powerWatcher == null)
        {
            return;
        }

        var state = stateMachine.getCurrentState();
        var displays = displayWatcher.queryConnectedDisplays();
        var extDisplay = displays.FirstOrDefault(d => !d.isInternal && !d.isVirtual);
        var monitorName = extDisplay != null ? $"{extDisplay.friendlyName} ({extDisplay.formattedTechnology})" : "None Detected";
        var lid = lidWatcher.queryLidState();
        var power = powerWatcher.queryPowerStatus();

        lock (syncRoot)
        {
            trayManager.updateStatus(state, monitorName, lid, power, keepAwake?.isActive == true);
        }

        scheduleWorkingSetTrim(3000);
    }

    private static void startSettingsWatcher()
    {
        try
        {
            var settingsDir = Path.GetDirectoryName(settingsManager.getSettingsFilePath());
            if (!string.IsNullOrEmpty(settingsDir))
            {
                if (!Directory.Exists(settingsDir))
                {
                    Directory.CreateDirectory(settingsDir);
                }

                settingsWatcher = new FileSystemWatcher(settingsDir, "settings.json")
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };

                settingsWatcher.Changed += (s, e) => onSettingsFileChanged();
                settingsWatcher.Created += (s, e) => onSettingsFileChanged();
                settingsWatcher.Renamed += (s, e) => onSettingsFileChanged();
            }
        }
        catch
        {
        }
    }

    private static void onSettingsFileChanged()
    {
        try
        {
            diagnosticsLogger.instance.logInfo("Daemon Event: Settings applied");
            var newSettings = settingsManager.loadSettings();
            lock (syncRoot)
            {
                appSettings = newSettings;
            }

            stateMachine?.updateSettings(newSettings);
            autoAmphetawin?.update(newSettings);
            refreshKeepAwake();
            updateTrayDisplay();
            scheduleWorkingSetTrim(1000);
        }
        catch
        {
        }
    }

    private static void applyAutoAmphetawin(DateTime? untilUtc)
    {
        var current = settingsManager.loadSettings();
        current.amphetawinUntilUtc = untilUtc;
        current.autoAmphetawinOwned = untilUtc.HasValue;
        lock (syncRoot)
        {
            appSettings = current;
        }

        stateMachine?.updateSettings(current);
        refreshKeepAwake();
        updateTrayDisplay();
        settingsManager.saveSettings(current);
        autoAmphetawin?.update(current);
    }

    private static void scheduleWorkingSetTrim(int delayMs)
    {
        lock (syncRoot)
        {
            workingSetTrimCts?.Cancel();
            workingSetTrimCts?.Dispose();
            var cts = new CancellationTokenSource();
            workingSetTrimCts = cts;

            Task.Delay(delayMs, cts.Token).ContinueWith(t =>
            {
                if (!t.IsCanceled)
                {
                    trimWorkingSet();
                }
            }, TaskScheduler.Default);
        }
    }

    private static void trimWorkingSet()
    {
        try
        {
            System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(2, GCCollectionMode.Aggressive, true, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Aggressive, true, true);

            var handle = nativeMethods.getCurrentProcess();
            if (handle != IntPtr.Zero)
            {
                nativeMethods.emptyWorkingSet(handle);
                nativeMethods.setProcessWorkingSetSize(handle, (IntPtr)(-1), (IntPtr)(-1));
            }
        }
        catch
        {
        }
    }

    private static void exitDaemon()
    {
        nativeMethods.postQuitMessage(0);
    }

    private static void scheduleBackgroundUpdateCheck()
    {
        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(10000);

                if (!appSettings.autoCheckForUpdates)
                {
                    return;
                }

                var now = DateTime.UtcNow;
                if (appSettings.lastUpdateCheckUtc.HasValue && (now - appSettings.lastUpdateCheckUtc.Value).TotalHours < 24)
                {
                    return;
                }

                using var checker = new updateChecker();
                var currentVer = typeof(Program).Assembly.GetName().Version ?? new Version(1, 0, 0);
                var result = await checker.checkForUpdatesAsync(currentVer);

                appSettings.lastUpdateCheckUtc = now;
                settingsManager.saveSettings(appSettings);

                if (result.isUpdateAvailable && result.latestVersion != null)
                {
                    trayManager?.showToastNotification(
                        "LidDock Update Available",
                        $"Version v{result.latestVersion} is available. Double-click tray icon to view.",
                        false);
                }
            }
            catch
            {
            }
            finally
            {
                scheduleWorkingSetTrim(1000);
            }
        });
    }

    private static void cleanup()
    {
        maintenanceTimer?.Dispose();
        settingsWatcher?.Dispose();
        workingSetTrimCts?.Cancel();
        workingSetTrimCts?.Dispose();
        autoAmphetawin?.dispose();
        keepAwake?.dispose();
        trayManager?.dispose();
        nativeWindow?.dispose();
        stateMachine?.dispose();
        powerManager?.dispose();

        if (singleInstanceMutex != null)
        {
            singleInstanceMutex.ReleaseMutex();
            singleInstanceMutex.Dispose();
        }
    }

    private static void performUninstall(bool silent, bool keepSettings)
    {
        try
        {
            var currentId = Environment.ProcessId;
            foreach (var p in Process.GetProcessesByName("LidDock"))
            {
                if (p.Id != currentId)
                {
                    try
                    {
                        p.Kill();
                        p.WaitForExit(1500);
                    }
                    catch
                    {
                    }
                }
            }

            foreach (var p in Process.GetProcessesByName("LidDock.UI"))
            {
                try
                {
                    p.Kill();
                    p.WaitForExit(1500);
                }
                catch
                {
                }
            }
        }
        catch
        {
        }

        try
        {
            using var pm = new powerSchemeManager();
            pm.restoreOriginalSettings();
        }
        catch
        {
        }

        try
        {
            settingsManager.performUninstall(keepSettings);
        }
        catch
        {
        }

        if (!silent)
        {
            nativeMethods.messageBox(
                IntPtr.Zero,
                "LidDock has been completely removed and Windows power settings restored.",
                "LidDock Uninstaller",
                0x00000040);
        }
    }
}
