using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Navigation;
using LidDock.App.Helpers;
using LidDock.App.ViewModels;

namespace LidDock.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(settingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        SourceInitialized += onSourceInitialized;
        ContentRendered += onContentRendered;
        StateChanged += onStateChanged;
    }

    private void onSourceInitialized(object? sender, EventArgs e)
    {
        windowBackdropHelper.applyBackdrop(this, true, true);
        accentColorHelper.applySystemAccentColor();

        var helper = new WindowInteropHelper(this);
        var source = HwndSource.FromHwnd(helper.Handle);
        source?.AddHook(wndProc);
    }

    private IntPtr wndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int wmSettingChange = 0x001A;
        const int wmDwmColorizationColorChanged = 0x0320;
        const int wmEnterSizeMove = 0x0231;
        const int wmExitSizeMove = 0x0232;

        if (msg == wmSettingChange || msg == wmDwmColorizationColorChanged)
        {
            Dispatcher.InvokeAsync(accentColorHelper.applySystemAccentColor);
        }
        else if (msg == wmEnterSizeMove)
        {
            windowBackdropHelper.handleSizeMove(hwnd, true);
        }
        else if (msg == wmExitSizeMove)
        {
            windowBackdropHelper.handleSizeMove(hwnd, false);
        }
        return IntPtr.Zero;
    }

    private void onContentRendered(object? sender, EventArgs e)
    {
        ContentRendered -= onContentRendered;
        Task.Delay(800).ContinueWith(_ =>
        {
            Dispatcher.InvokeAsync(memoryOptimizer.trimWorkingSet);
        });
    }

    private void onStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            memoryOptimizer.trimWorkingSet();
        }
    }

    public event Action? onOpenDiagnosticsRequested;

    private void onDiagnosticsClicked(object sender, RoutedEventArgs e)
    {
        onOpenDiagnosticsRequested?.Invoke();
    }

    private void onCloseClicked(object sender, RoutedEventArgs e)
    {
        (DataContext as settingsViewModel)?.save();
        Close();
    }

    private async void onCheckForUpdatesClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is settingsViewModel vm)
        {
            await vm.checkForUpdatesAsync();
        }
    }

    private void onHyperlinkRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }
        catch
        {
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        memoryOptimizer.trimWorkingSet();
    }
}
