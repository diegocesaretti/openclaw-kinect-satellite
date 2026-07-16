using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenClaw.KinectSatellite.Infrastructure;
using Serilog;
using Serilog.Events;
using OpenClaw.KinectSatellite.Core;

namespace OpenClaw.KinectSatellite;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            var builder = Host.CreateApplicationBuilder(e.Args);
            var settings = new UserSettingsStore();
            builder.Services.AddSingleton<IUserSettingsStore>(settings);
            var level = Enum.TryParse<LogEventLevel>(settings.Current.Application.LoggingLevel, true, out var parsed) ? parsed : LogEventLevel.Information;
            builder.Services.AddSerilog(configuration => configuration.MinimumLevel.Is(level).Enrich.FromLogContext().WriteTo.File(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenClaw", "KinectSatellite", "logs", "openclaw-.log"),
                rollingInterval: RollingInterval.Day));
            builder.Services.AddSatelliteInfrastructure(builder.Configuration);
            builder.Services.AddSingleton<MainWindow>();
            _host = builder.Build();
            await _host.StartAsync();
            var window = _host.Services.GetRequiredService<MainWindow>();
            MainWindow = window;
            if (!e.Args.Contains("--minimized", StringComparer.OrdinalIgnoreCase)) window.Show();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.Message, "OpenClaw Kinect Satellite could not start", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null) { await _host.StopAsync(); _host.Dispose(); }
        await Log.CloseAndFlushAsync();
        base.OnExit(e);
    }
}
