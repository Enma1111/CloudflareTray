using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using CloudflareTray.Services;
using CloudflareTray.ViewModels;

namespace CloudflareTray.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is MainWindowViewModel vm)
            vm.ShowNewTunnelDialog = dialog => new NewTunnelWindow { DataContext = dialog }.ShowDialog<bool>(this);
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // Closing only hides the window to the tray; the tunnels keep running. Quitting is done via the tray menu.
        // Without a tray (GNOME without AppIndicator) the window would be unreachable, so only minimize it.
        if (e.CloseReason == WindowCloseReason.WindowClosing)
        {
            e.Cancel = true;
            var trayAvailable = TrayHost.IsAvailable();
            if (DataContext is MainWindowViewModel vm) vm.IsTrayAvailable = trayAvailable;

            if (trayAvailable)
                Hide();
            else
                WindowState = WindowState.Minimized;
        }
        base.OnClosing(e);
    }

    private void OnQuitClick(object? sender, RoutedEventArgs e) =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
}
