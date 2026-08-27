using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using LidDock.App.ViewModels;
using LidDock.App.Views;
using LidDock.Core.Models;
using LidDock.Core.StateMachine;
using LidDock.Windows.Power;
using LidDock.Windows.Watchers;

namespace LidDock.App;

public partial class App : Application
{
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
        base.OnStartup(e);

        ensureDaemonRunning();

        appSettings = settingsManager.loadSettings();
        powerManager = new powerSchemeManager();
        stateMachine = new clamshellStateMachine(powerManager);
        displayWatcher = new displayWatcher();
        lidWatcher = new lidWatcher();
        powerWatcher = new powerWatcher();

        settingsVm = new settingsViewModel(appSettings, stateMachine);

        if (e.Args.Contains("--diagnostics"))
        {
            openDiagnostics();
        }
        else
        {
            openSettings();
        }
    }

    private void ensureDaemonRunning()
    {
        try
        {
            if (Process.GetProcessesByName("LidDock").Length == 0 && Process.GetProcessesByName("LidDock.Daemon").Length == 0)
            {
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
                        UseShellExecute = true
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

    private void openDiagnostics()
    {
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
