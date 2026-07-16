using OpenClaw.KinectSatellite.Core;
using OpenClaw.KinectSatellite.Infrastructure;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();
try
{
    var builder = Host.CreateApplicationBuilder(args);
    builder.Services.AddSerilog((services, configuration) => configuration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File("logs/openclaw-.log", rollingInterval: RollingInterval.Day));
    builder.Services.AddSatelliteInfrastructure(builder.Configuration);
    builder.Services.AddHostedService<SatelliteWorker>();
    await builder.Build().RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "OpenClaw Kinect Satellite terminated unexpectedly");
}
finally { await Log.CloseAndFlushAsync(); }

