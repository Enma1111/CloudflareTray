using Avalonia;
using System;
using System.IO;
using CloudflareTray.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace CloudflareTray;

sealed class Program
{
    public const string WmClass = "CloudflareTray";

    public static string LogDirectory => Path.Combine(AppPaths.DataDirectory, "logs");

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static int Main(string[] args)
    {
        Log.Logger = CreateLogger();

        try
        {
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                Args = args,
                // Otherwise the current working directory would be watched
                ContentRootPath = AppContext.BaseDirectory,
            });

            builder.Logging.ClearProviders();
            builder.Services.AddSerilog(dispose: false);
            builder.Services.AddCloudflareTray(ProfileLoader.DefaultDirectory);

            using var host = builder.Build();
            host.Start();
            Log.Information("CloudflareTray started, data in {DataDirectory}", AppPaths.DataDirectory);

            var exitCode = BuildAvaloniaApp(() => new App(host.Services))
                .StartWithClassicDesktopLifetime(args);

            // When the host is stopped and disposed, CloudflareTunnelService stops all tunnels
            host.StopAsync().GetAwaiter().GetResult();
            Log.Information("CloudflareTray exited");
            return exitCode;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "CloudflareTray terminated unexpectedly");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    /// <summary>
    /// Console and one log file per day in <see cref="LogDirectory"/>. Hard-coded instead of using
    /// Serilog.Settings.Configuration, which relies on reflection and is not reliable under Native AOT.
    /// </summary>
    private static Logger CreateLogger()
    {
        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(Path.Combine(LogDirectory, "cloudflaretray-.log"),
                rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
            .CreateLogger();
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp() => BuildAvaloniaApp(() => new App());

    private static AppBuilder BuildAvaloniaApp(Func<App> createApp)
        => AppBuilder.Configure(createApp)
            .UsePlatformDetect()
            // Must match StartupWMClass in packaging/linux/cloudflaretray.desktop,
            // otherwise GNOME doesn't associate the window with the app (wrong icon in Alt+Tab and the dock)
            .With(new X11PlatformOptions { WmClass = WmClass })
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
