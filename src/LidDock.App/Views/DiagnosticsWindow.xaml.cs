using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using LidDock.App.Helpers;
using LidDock.App.ViewModels;
using LidDock.Core.Models;
using LidDock.Windows.Native;

namespace LidDock.App.Views;

public partial class DiagnosticsWindow : Window
{
    private readonly diagnosticsViewModel viewModel;
    private IntPtr lidNotificationHandle = IntPtr.Zero;

    public DiagnosticsWindow(diagnosticsViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
        SourceInitialized += onSourceInitialized;
    }

    private void onSourceInitialized(object? sender, EventArgs e)
    {
        windowBackdropHelper.applyBackdrop(this, true, true);

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
        }
    }

    private IntPtr wndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == nativeConstants.wmPowerBroadcast && wParam.ToInt64() == nativeConstants.pbtPowerSettingChange && lParam != IntPtr.Zero)
        {
            var setting = Marshal.PtrToStructure<powerBroadcastSetting>(lParam);
            if (setting.powerSetting == nativeConstants.guidLidSwitchStateChange)
            {
                var isLidOpen = (setting.dataLength == 1)
                    ? Marshal.ReadByte(lParam, 20) != 0
                    : Marshal.ReadInt32(lParam, 20) != 0;
                viewModel.updateLidState(isLidOpen ? lidState.open : lidState.closed);
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
        if (lidNotificationHandle != IntPtr.Zero)
        {
            nativeMethods.unregisterPowerSettingNotification(lidNotificationHandle);
            lidNotificationHandle = IntPtr.Zero;
        }

        viewModel.unsubscribe();
        base.OnClosed(e);
        memoryOptimizer.trimWorkingSet();
    }
}
