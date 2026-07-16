using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenClaw.KinectSatellite.Core;

namespace OpenClaw.KinectSatellite.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSatelliteInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.TryAddSingleton<IUserSettingsStore, UserSettingsStore>();
        services.AddSingleton<IKinectService, KinectService>();
        services.AddSingleton<IWakeWordService, MicroWakeWordService>();
        services.AddSingleton<IViewAssistClient, ViewAssistClient>();
        services.AddSingleton<IAudioPlayer, AudioPlayer>();
        services.AddSingleton<IHomeAssistantConnectionTester, HomeAssistantConnectionTester>();
        services.AddSingleton<SatelliteWorker>();
        services.AddSingleton<ISatelliteController, SatelliteController>();
        services.AddHttpClient(nameof(AudioPlayer));
        services.AddHttpClient(nameof(HomeAssistantConnectionTester));
        return services;
    }
}
