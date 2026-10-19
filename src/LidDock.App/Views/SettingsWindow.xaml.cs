using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
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
        viewModel.PropertyChanged += onViewModelPropertyChanged;
        if (viewModel.onlySelectedDisplaysTrigger)
        {
            displaySelectionPanel.Height = double.NaN;
        }
        SourceInitialized += onSourceInitialized;
        ContentRendered += onContentRendered;
        StateChanged += onStateChanged;
        Activated += onActivated;
    }

    private void onSourceInitialized(object? sender, EventArgs e)
    {
        windowBackdropHelper.applyBackdrop(this, true);
        themeManager.applySystemTheme();

        var helper = new WindowInteropHelper(this);
        var source = HwndSource.FromHwnd(helper.Handle);
        source?.AddHook(wndProc);
    }

    private IntPtr wndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int wmSettingChange = 0x001A;
        const int wmThemeChanged = 0x031A;
        const int wmDwmColorizationColorChanged = 0x0320;
        const int wmEnterSizeMove = 0x0231;
        const int wmExitSizeMove = 0x0232;

        if (msg == wmSettingChange || msg == wmThemeChanged || msg == wmDwmColorizationColorChanged)
        {
            Dispatcher.InvokeAsync(() => themeManager.applySystemTheme(true));
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

    private void onActivated(object? sender, EventArgs e)
    {
        (DataContext as settingsViewModel)?.refreshDisplayList();
    }

    private DoubleAnimation? displaySelectionAnimation;

    private void onViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(settingsViewModel.onlySelectedDisplaysTrigger))
        {
            animateDisplaySelection((DataContext as settingsViewModel)?.onlySelectedDisplaysTrigger == true);
        }
    }

    private void animateDisplaySelection(bool expand)
    {
        var startHeight = displaySelectionPanel.ActualHeight;
        displaySelectionPanel.Height = expand ? double.NaN : 0;

        var targetHeight = 0.0;
        if (expand)
        {
            displaySelectionPanel.Measure(new Size(displaySelectionPanel.ActualWidth, double.PositiveInfinity));
            targetHeight = displaySelectionPanel.DesiredSize.Height + displaySelectionPanel.Margin.Top + displaySelectionPanel.Margin.Bottom;
        }

        var animation = new DoubleAnimation(startHeight, targetHeight, TimeSpan.FromMilliseconds(expand ? 260 : 200))
        {
            EasingFunction = new CubicEase { EasingMode = expand ? EasingMode.EaseOut : EasingMode.EaseIn }
        };
        animation.Completed += onDisplaySelectionAnimationCompleted;
        displaySelectionAnimation = animation;
        displaySelectionPanel.BeginAnimation(FrameworkElement.HeightProperty, animation);
    }

    private void onDisplaySelectionAnimationCompleted(object? sender, EventArgs e)
    {
        if (!ReferenceEquals(sender, displaySelectionAnimation))
        {
            return;
        }

        displaySelectionAnimation = null;
        displaySelectionPanel.BeginAnimation(FrameworkElement.HeightProperty, null);
        displaySelectionPanel.Height = (DataContext as settingsViewModel)?.onlySelectedDisplaysTrigger == true ? double.NaN : 0;
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
