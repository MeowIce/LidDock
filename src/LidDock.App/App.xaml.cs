using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using LidDock.App.Helpers;
using LidDock.App.ViewModels;
using LidDock.App.Views;
using LidDock.Core.Models;
using LidDock.Core.StateMachine;
using LidDock.Windows.Power;
using LidDock.Windows.Watchers;

namespace LidDock.App;

public partial class App : Application
{
    private static Mutex? singleInstanceMutex;
    private powerSchemeManager? powerManager;
    private clamshellStateMachine? stateMachine;
    private displayWatcher? displayWatcher;
    private lidWatcher? lidWatcher;
    private powerWatcher? powerWatcher;
    private appSettings appSettings = new appSettings();
    private settingsViewModel? settingsVm;
    private SettingsWindow? settingsWindowInstance;
    private DiagnosticsWindow? diagnosticsWindowInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            singleInstanceMutex = new Mutex(true, "LidDock_UI_Mutex", out var createdNew);
            if (!createdNew)
            {
                Shutdown();
                Environment.Exit(0);
                return;
            }

            base.OnStartup(e);
            themeManager.applySystemTheme();

            if (e.Args.Contains("--uninstall"))
            {
                settingsManager.performUninstall(e.Args.Contains("--keep-settings"));
                Shutdown();
                Environment.Exit(0);
                return;
            }

            if (e.Args.Contains("--minimized"))
            {
                ensureDaemonRunning();
                Shutdown();
                Environment.Exit(0);
                return;
            }

            ensureDaemonRunning();

            appSettings = settingsManager.loadSettings();
            settingsVm = new settingsViewModel(appSettings);

            if (e.Args.Contains("--diagnostics"))
            {
                openDiagnostics();
            }
            else
            {
                openSettings();
            }
        }
        catch
        {
        }
    }

    private void ensureDaemonRunning()
    {
        try
        {
            var liddockCount = Process.GetProcessesByName("LidDock").Length;
            var daemonCount = Process.GetProcessesByName("LidDock.Daemon").Length;

            if (liddockCount == 0 && daemonCount == 0)
            {
                var permanentPath = settingsManager.getPermanentDaemonPath();
                if (File.Exists(permanentPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = permanentPath,
                        Arguments = "--minimized",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    return;
                }

                using var appKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\LidDock");
                var savedPath = appKey?.GetValue("DaemonPath") as string;
                if (!string.IsNullOrEmpty(savedPath) && File.Exists(savedPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = savedPath,
                        Arguments = "--minimized",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    return;
                }

                var baseDir = AppContext.BaseDirectory;
                var daemonPath = Path.Combine(baseDir, "LidDock.exe");
                if (!File.Exists(daemonPath))
                {
                    daemonPath = Path.Combine(baseDir, "LidDock.Daemon.exe");
                }

                if (File.Exists(daemonPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = daemonPath,
                        Arguments = "--minimized",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                }
            }
        }
        catch
        {
        }
    }

    private void openSettings()
    {
        if (settingsVm == null)
        {
            return;
        }

        try
        {
            if (settingsWindowInstance == null || !settingsWindowInstance.IsLoaded)
            {
                settingsWindowInstance = new SettingsWindow(settingsVm);
                MainWindow = settingsWindowInstance;
                settingsWindowInstance.onOpenDiagnosticsRequested += openDiagnostics;
                settingsWindowInstance.Closed += (s, e) =>
                {
                    settingsWindowInstance = null;
                    if (diagnosticsWindowInstance == null)
                    {
                        Shutdown();
                        Environment.Exit(0);
                    }
                };
                settingsWindowInstance.Show();
                settingsWindowInstance.Activate();
            }
            else
            {
                settingsWindowInstance.Activate();
            }
        }
        catch
        {
        }
    }

    private void ensureDiagnosticsComponents()
    {
        if (powerManager == null)
        {
            powerManager = new powerSchemeManager();
            stateMachine = new clamshellStateMachine(powerManager);
            displayWatcher = new displayWatcher();
            lidWatcher = new lidWatcher();
            powerWatcher = new powerWatcher();

            displayWatcher.onDisplaysChanged += d => stateMachine.updateDisplays(d);
            powerWatcher.onPowerStatusChanged += p => stateMachine.updatePowerInfo(p);
            lidWatcher.onLidStateChanged += l => stateMachine.updateLidState(l);

            displayWatcher.notifyDisplayConfigurationChanged();
            powerWatcher.notifyPowerStatusChanged();
            stateMachine.updateLidState(lidWatcher.queryLidState());
        }
    }

    private void openDiagnostics()
    {
        ensureDiagnosticsComponents();

        if (stateMachine == null || displayWatcher == null || lidWatcher == null || powerWatcher == null || powerManager == null)
        {
            return;
        }

        if (diagnosticsWindowInstance == null || !diagnosticsWindowInstance.IsLoaded)
        {
            var vm = new diagnosticsViewModel(stateMachine, displayWatcher, lidWatcher, powerWatcher, powerManager);
            diagnosticsWindowInstance = new DiagnosticsWindow(vm);
            if (MainWindow == null)
            {
                MainWindow = diagnosticsWindowInstance;
            }

            diagnosticsWindowInstance.Closed += (s, e) =>
            {
                diagnosticsWindowInstance = null;
                if (settingsWindowInstance == null)
                {
                    Shutdown();
                    Environment.Exit(0);
                }
            };
            diagnosticsWindowInstance.Show();
            diagnosticsWindowInstance.Activate();
        }
        else
        {
            diagnosticsWindowInstance.Activate();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        settingsManager.saveSettings(appSettings);
        base.OnExit(e);
    }
}
