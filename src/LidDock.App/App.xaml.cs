using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using LidDock.App.Tray;
using LidDock.App.ViewModels;
using LidDock.App.Views;
using LidDock.Core.Models;
using LidDock.Core.StateMachine;
using LidDock.Diagnostics;
using LidDock.Windows.Power;
using LidDock.Windows.Watchers;

namespace LidDock.App;

public partial class App : Application
{
    private Mutex? singleInstanceMutex;
    private powerSchemeManager? powerManager;
    private clamshellStateMachine? stateMachine;
    private displayWatcher? displayWatcher;
    private lidWatcher? lidWatcher;
    private powerWatcher? powerWatcher;
    private nativeMessageWindow? nativeWindow;
    private trayIconManager? trayManager;
    private appSettings appSettings = new appSettings();
    private settingsViewModel? settingsVm;
    private diagnosticsViewModel? diagnosticsVm;
    private SettingsWindow? settingsWindowInstance;
    private DiagnosticsWindow? diagnosticsWindowInstance;

    private MenuItem? statusMenuItem;
    private MenuItem? displayMenuItem;
    private MenuItem? lidMenuItem;
    private MenuItem? powerMenuItem;
    private MenuItem? enableClamshellMenuItem;
    private MenuItem? profileSmartDockedItem;
    private MenuItem? profileAcOnlyItem;
    private MenuItem? profileAlwaysClamshellItem;
    private MenuItem? profileCustomItem;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        singleInstanceMutex = new Mutex(true, "LidDock_SingleInstance_Mutex", out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show("Another instance of LidDock is already running.", "LidDock", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        initializeComponents();
    }

    private void initializeComponents()
    {
        diagnosticsLogger.instance.logInfo("Initializing LidDock Core Services");

        powerManager = new powerSchemeManager();
        powerManager.backupOriginalSettings();

        stateMachine = new clamshellStateMachine(powerManager);
        displayWatcher = new displayWatcher();
        lidWatcher = new lidWatcher();
        powerWatcher = new powerWatcher();
        nativeWindow = new nativeMessageWindow();
        trayManager = new trayIconManager();

        settingsVm = new settingsViewModel(appSettings, stateMachine);
        diagnosticsVm = new diagnosticsViewModel(stateMachine, displayWatcher, lidWatcher, powerWatcher, powerManager);

        nativeWindow.initialize();

        var contextMenu = buildTrayContextMenu();
        var helper = new WindowInteropHelper(new Window());
        var messageHwnd = helper.EnsureHandle();
        trayManager.initialize(messageHwnd, contextMenu);

        var hwndSource = HwndSource.FromHwnd(messageHwnd);
        hwndSource?.AddHook(trayMessageHook);

        hookEvents();

        displayWatcher.notifyDisplayConfigurationChanged();
        powerWatcher.notifyPowerStatusChanged();

        diagnosticsLogger.instance.logInfo("LidDock Ready and Monitoring");
    }

    private IntPtr trayMessageHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == trayIconManager.wmTrayCallback)
        {
            trayManager?.handleWindowMessage(msg, wParam, lParam);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void hookEvents()
    {
        if (nativeWindow == null || displayWatcher == null || lidWatcher == null || powerWatcher == null || stateMachine == null || trayManager == null)
        {
            return;
        }

        nativeWindow.onDisplayChangedMessage += () =>
        {
            diagnosticsLogger.instance.logInfo("Event: WM_DISPLAYCHANGE received");
            displayWatcher.notifyDisplayConfigurationChanged();
        };

        nativeWindow.onLidStateChangedMessage += isOpen =>
        {
            diagnosticsLogger.instance.logInfo($"Event: Lid state changed to {(isOpen ? "Open" : "Closed")}");
            lidWatcher.notifyLidStateChanged(isOpen);
            stateMachine.updateLidState(isOpen ? lidState.open : lidState.closed);
        };

        nativeWindow.onPowerSourceChangedMessage += () =>
        {
            diagnosticsLogger.instance.logInfo("Event: Power source changed");
            powerWatcher.notifyPowerStatusChanged();
        };

        nativeWindow.onTaskbarCreatedMessage += () =>
        {
            diagnosticsLogger.instance.logInfo("Event: Taskbar created, recreating tray icon");
            trayManager.recreateIcon();
        };

        displayWatcher.onDisplaysChanged += displays =>
        {
            diagnosticsLogger.instance.logInfo($"Active display paths updated. Total count: {displays.Count}");
            stateMachine.updateDisplays(displays);
            updateTrayDisplay();
        };

        powerWatcher.onPowerStatusChanged += power =>
        {
            diagnosticsLogger.instance.logInfo($"Power status updated: {power.powerSource}, Battery: {power.batteryPercent}%");
            stateMachine.updatePowerInfo(power);
            updateTrayDisplay();
        };

        stateMachine.onStateChanged += state =>
        {
            diagnosticsLogger.instance.logInfo($"State Transition: Current State = {state}");
            updateTrayDisplay();
        };
    }

    private void updateTrayDisplay()
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

        trayManager.updateStatus(state, monitorName, lid, power);

        Dispatcher.InvokeAsync(() =>
        {
            if (statusMenuItem != null)
            {
                statusMenuItem.Header = $"Status: {displayFormatters.formatClamshellState(state)}";
            }
            if (displayMenuItem != null)
            {
                displayMenuItem.Header = $"Display: {monitorName}";
            }
            if (lidMenuItem != null)
            {
                lidMenuItem.Header = $"Lid: {displayFormatters.formatLidState(lid)}";
            }
            if (powerMenuItem != null)
            {
                powerMenuItem.Header = $"Power: {displayFormatters.formatPowerSource(power)}";
            }
            if (enableClamshellMenuItem != null)
            {
                enableClamshellMenuItem.IsChecked = appSettings.enableClamshell;
            }
            updateProfileCheckmarks();
        });
    }

    private ContextMenu buildTrayContextMenu()
    {
        var menu = new ContextMenu();

        statusMenuItem = new MenuItem
        {
            Header = "Status: Standard (Undocked)",
            IsEnabled = false,
            FontWeight = FontWeights.Bold
        };
        menu.Items.Add(statusMenuItem);

        displayMenuItem = new MenuItem
        {
            Header = "Display: None Detected",
            IsEnabled = false
        };
        menu.Items.Add(displayMenuItem);

        lidMenuItem = new MenuItem
        {
            Header = "Lid: Open",
            IsEnabled = false
        };
        menu.Items.Add(lidMenuItem);

        powerMenuItem = new MenuItem
        {
            Header = "Power: AC Connected",
            IsEnabled = false
        };
        menu.Items.Add(powerMenuItem);

        menu.Items.Add(new Separator());

        enableClamshellMenuItem = new MenuItem
        {
            Header = "Enable Smart Clamshell Mode",
            IsCheckable = true,
            IsChecked = appSettings.enableClamshell
        };
        enableClamshellMenuItem.Click += (s, e) =>
        {
            appSettings.enableClamshell = enableClamshellMenuItem.IsChecked;
            stateMachine?.updateSettings(appSettings);
        };
        menu.Items.Add(enableClamshellMenuItem);

        var profileMenu = new MenuItem { Header = "Profiles" };

        profileSmartDockedItem = new MenuItem { Header = "Smart Docked (Recommended)", IsCheckable = true };
        profileSmartDockedItem.Click += (s, e) => selectProfile(operationalProfileType.smartDocked);
        profileMenu.Items.Add(profileSmartDockedItem);

        profileAcOnlyItem = new MenuItem { Header = "AC Power Only", IsCheckable = true };
        profileAcOnlyItem.Click += (s, e) => selectProfile(operationalProfileType.acOnly);
        profileMenu.Items.Add(profileAcOnlyItem);

        profileAlwaysClamshellItem = new MenuItem { Header = "Always Clamshell", IsCheckable = true };
        profileAlwaysClamshellItem.Click += (s, e) => selectProfile(operationalProfileType.alwaysClamshell);
        profileMenu.Items.Add(profileAlwaysClamshellItem);

        profileCustomItem = new MenuItem { Header = "Custom Profile", IsCheckable = true };
        profileCustomItem.Click += (s, e) => selectProfile(operationalProfileType.custom);
        profileMenu.Items.Add(profileCustomItem);

        menu.Items.Add(profileMenu);
        menu.Items.Add(new Separator());

        var settingsItem = new MenuItem { Header = "Settings..." };
        settingsItem.Click += (s, e) => openSettings();
        menu.Items.Add(settingsItem);

        var diagItem = new MenuItem { Header = "Diagnostics..." };
        diagItem.Click += (s, e) => openDiagnostics();
        menu.Items.Add(diagItem);

        menu.Items.Add(new Separator());

        var exitItem = new MenuItem { Header = "Exit LidDock" };
        exitItem.Click += (s, e) => exitApplication();
        menu.Items.Add(exitItem);

        updateProfileCheckmarks();

        return menu;
    }

    private void selectProfile(operationalProfileType profile)
    {
        appSettings.activeProfile = profile;
        updateProfileCheckmarks();
        stateMachine?.updateSettings(appSettings);
    }

    private void updateProfileCheckmarks()
    {
        if (profileSmartDockedItem != null)
        {
            profileSmartDockedItem.IsChecked = appSettings.activeProfile == operationalProfileType.smartDocked;
        }
        if (profileAcOnlyItem != null)
        {
            profileAcOnlyItem.IsChecked = appSettings.activeProfile == operationalProfileType.acOnly;
        }
        if (profileAlwaysClamshellItem != null)
        {
            profileAlwaysClamshellItem.IsChecked = appSettings.activeProfile == operationalProfileType.alwaysClamshell;
        }
        if (profileCustomItem != null)
        {
            profileCustomItem.IsChecked = appSettings.activeProfile == operationalProfileType.custom;
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
            settingsWindowInstance.Show();
        }
        else
        {
            settingsWindowInstance.Activate();
        }
    }

    private void openDiagnostics()
    {
        if (diagnosticsVm == null)
        {
            return;
        }

        if (diagnosticsWindowInstance == null || !diagnosticsWindowInstance.IsLoaded)
        {
            diagnosticsWindowInstance = new DiagnosticsWindow(diagnosticsVm);
            diagnosticsWindowInstance.Show();
        }
        else
        {
            diagnosticsWindowInstance.Activate();
        }
    }

    private void exitApplication()
    {
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        diagnosticsLogger.instance.logInfo("LidDock Exiting. Restoring original power settings.");

        trayManager?.dispose();
        nativeWindow?.dispose();
        stateMachine?.dispose();
        powerManager?.dispose();

        if (singleInstanceMutex != null)
        {
            singleInstanceMutex.ReleaseMutex();
            singleInstanceMutex.Dispose();
        }

        base.OnExit(e);
    }
}
