using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using CloudflareTray.Services;
using CloudflareTray.ViewModels;
using CloudflareTray.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CloudflareTray;

public partial class App : Application
{
    // Null in the designer, which creates the app via the parameterless constructor
    private readonly IServiceProvider? _services;

    public App()
    {
    }

    public App(IServiceProvider services)
    {
        _services = services;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && _services != null)
        {
            var viewModel = _services.GetRequiredService<MainWindowViewModel>();

            // Closing the window only hides it; the app is quit via the tray menu
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.MainWindow = new MainWindow { DataContext = viewModel };

            // SIGTERM/Ctrl+C stops the host, so the UI should shut down as well
            _services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping
                .Register(() => Dispatcher.UIThread.Post(() => desktop.Shutdown()));

            SetupTrayIcon(desktop, viewModel);
            viewModel.IsTrayAvailable = TrayHost.IsAvailable();
            if (!viewModel.IsTrayAvailable)
                _services.GetRequiredService<ILogger<App>>().LogWarning("No tray available, closing minimizes the window");
            viewModel.Reload();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupTrayIcon(IClassicDesktopStyleApplicationLifetime desktop, MainWindowViewModel viewModel)
    {
        var open = new NativeMenuItem("Open");
        open.Click += (_, _) => ShowMainWindow(desktop);
        var quit = new NativeMenuItem("Quit");
        quit.Click += (_, _) => desktop.Shutdown();

        var trayIcon = new TrayIcon
        {
            Menu = new NativeMenu { Items = { open, new NativeMenuItemSeparator(), quit } },
        };
        trayIcon.Clicked += (_, _) => ShowMainWindow(desktop);

        void Update()
        {
            var whiteGlyph = TrayIconAssets.IsTrayBarDark(ActualThemeVariant == ThemeVariant.Dark);
            var uri = TrayIconAssets.Uri(viewModel.TrayStatus, whiteGlyph, OperatingSystem.IsWindows());
            trayIcon.Icon = new WindowIcon(AssetLoader.Open(new Uri(uri)));
            trayIcon.ToolTipText = viewModel.TrayToolTip;
        }

        Update();
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainWindowViewModel.TrayStatus) or nameof(MainWindowViewModel.TrayToolTip))
                Update();
        };
        // When the system switches between light and dark, the panel usually changes too
        ActualThemeVariantChanged += (_, _) => Update();

        TrayIcon.SetIcons(this, [trayIcon]);
    }

    private static void ShowMainWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (desktop.MainWindow is not { } window) return;
        window.Show();
        window.Activate();
    }
}
