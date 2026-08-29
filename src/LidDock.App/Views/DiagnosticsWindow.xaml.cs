using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using LidDock.App.Helpers;
using LidDock.App.ViewModels;
using LidDock.Core.Models;
using LidDock.Windows.Native;

namespace LidDock.App.Views;

public partial class DiagnosticsWindow : Window
{
    private readonly diagnosticsViewModel viewModel;
    private IntPtr lidNotificationHandle = IntPtr.Zero;
    private IntPtr powerNotificationHandle = IntPtr.Zero;
    private IntPtr batteryNotificationHandle = IntPtr.Zero;
    private uint settingsChangedMessage;
    private DispatcherTimer? dynamicMetricsTimer;

    public DiagnosticsWindow(diagnosticsViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
        SourceInitialized += onSourceInitialized;
        ContentRendered += onContentRendered;
    }

    private void onContentRendered(object? sender, EventArgs e)
    {
        ContentRendered -= onContentRendered;
        Task.Delay(800).ContinueWith(_ =>
        {
            Dispatcher.InvokeAsync(memoryOptimizer.trimWorkingSet);
        });
    }

    private void onSourceInitialized(object? sender, EventArgs e)
    {
        windowBackdropHelper.applyBackdrop(this, true, true);
        accentColorHelper.applySystemAccentColor();

        var helper = new WindowInteropHelper(this);
        var source = HwndSource.FromHwnd(helper.Handle);
        if (source != null)
        {
            source.AddHook(wndProc);

            var lidGuid = nativeConstants.guidLidSwitchStateChange;
            lidNotificationHandle = nativeMethods.registerPowerSettingNotification(
                helper.Handle,
                ref lidGuid,
                nativeConstants.deviceNotifyWindowHandle);

            var powerGuid = nativeConstants.guidAcdcPowerSource;
            powerNotificationHandle = nativeMethods.registerPowerSettingNotification(
                helper.Handle,
                ref powerGuid,
                nativeConstants.deviceNotifyWindowHandle);

            var batteryGuid = nativeConstants.guidBatteryPercentageRemaining;
            batteryNotificationHandle = nativeMethods.registerPowerSettingNotification(
                helper.Handle,
                ref batteryGuid,
                nativeConstants.deviceNotifyWindowHandle);

            settingsChangedMessage = nativeMethods.registerWindowMessage("LidDock_SettingsChanged_Event");
        }

        dynamicMetricsTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        dynamicMetricsTimer.Tick += (s, e) => viewModel.pollDynamicMetrics();
        dynamicMetricsTimer.Start();
    }

    private IntPtr wndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int wmSettingChange = 0x001A;
        const int wmDwmColorizationColorChanged = 0x0320;

        if (msg == wmSettingChange || msg == wmDwmColorizationColorChanged)
        {
            Dispatcher.InvokeAsync(accentColorHelper.applySystemAccentColor);
            return IntPtr.Zero;
        }

        if (msg == nativeConstants.wmDisplayChange)
        {
            viewModel.onDisplayTopologyChanged();
            return IntPtr.Zero;
        }

        if (settingsChangedMessage != 0 && (uint)msg == settingsChangedMessage)
        {
            viewModel.onSettingsChanged();
            return IntPtr.Zero;
        }

        if (msg == nativeConstants.wmPowerBroadcast)
        {
            var wParamVal = wParam.ToInt64();
            if (wParamVal == nativeConstants.pbtApmPowerStatusChange)
            {
                viewModel.onPowerStatusChanged();
                return IntPtr.Zero;
            }

            if (wParamVal == nativeConstants.pbtPowerSettingChange && lParam != IntPtr.Zero)
            {
                var setting = Marshal.PtrToStructure<powerBroadcastSetting>(lParam);
                if (setting.powerSetting == nativeConstants.guidLidSwitchStateChange)
                {
                    var isLidOpen = (setting.dataLength == 1)
                        ? Marshal.ReadByte(lParam, 20) != 0
                        : Marshal.ReadInt32(lParam, 20) != 0;
                    viewModel.updateLidState(isLidOpen ? lidState.open : lidState.closed);
                }
                else if (setting.powerSetting == nativeConstants.guidAcdcPowerSource ||
                         setting.powerSetting == nativeConstants.guidBatteryPercentageRemaining)
                {
                    viewModel.onPowerStatusChanged();
                }
                return IntPtr.Zero;
            }
        }
        return IntPtr.Zero;
    }

    private void onRefreshClicked(object sender, RoutedEventArgs e)
    {
        viewModel.refreshAll();
    }

    private void onExportClicked(object sender, RoutedEventArgs e)
    {
        var exportedFile = viewModel.exportReport();
        MessageBox.Show($"Diagnostics report saved to:\n{exportedFile}", "Report Exported", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void onCloseClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        dynamicMetricsTimer?.Stop();

        if (lidNotificationHandle != IntPtr.Zero)
        {
            nativeMethods.unregisterPowerSettingNotification(lidNotificationHandle);
            lidNotificationHandle = IntPtr.Zero;
        }

        if (powerNotificationHandle != IntPtr.Zero)
        {
            nativeMethods.unregisterPowerSettingNotification(powerNotificationHandle);
            powerNotificationHandle = IntPtr.Zero;
        }

        if (batteryNotificationHandle != IntPtr.Zero)
        {
            nativeMethods.unregisterPowerSettingNotification(batteryNotificationHandle);
            batteryNotificationHandle = IntPtr.Zero;
        }

        viewModel.unsubscribe();
        base.OnClosed(e);
        memoryOptimizer.trimWorkingSet();
    }
}
