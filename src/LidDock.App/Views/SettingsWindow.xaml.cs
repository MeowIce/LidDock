using System;
using System.Diagnostics;
using System.Windows;
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
    }

    private void onSourceInitialized(object? sender, EventArgs e)
    {
        windowBackdropHelper.applyBackdrop(this, true, true);
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
