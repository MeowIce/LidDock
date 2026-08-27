using System;
using System.Windows;
using LidDock.App.Helpers;
using LidDock.App.ViewModels;

namespace LidDock.App.Views;

public partial class DiagnosticsWindow : Window
{
    private readonly diagnosticsViewModel viewModel;

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
        viewModel.unsubscribe();
        base.OnClosed(e);
        memoryOptimizer.trimWorkingSet();
    }
}
