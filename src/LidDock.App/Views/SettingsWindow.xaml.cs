using System;
using System.Windows;
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

    private void onCloseClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
