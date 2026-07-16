using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenClaw.KinectSatellite.Core;

namespace OpenClaw.KinectSatellite.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSatelliteInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<KinectOptions>().Bind(config.GetSection(KinectOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<WakeWordOptions>().Bind(config.GetSection(WakeWordOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<HomeAssistantOptions>().Bind(config.GetSection(HomeAssistantOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<IKinectService, KinectService>();
        services.AddSingleton<IWakeWordService, MicroWakeWordService>();
        services.AddSingleton<IViewAssistClient, ViewAssistClient>();
        services.AddSingleton<IAudioPlayer, AudioPlayer>();
        services.AddHttpClient(nameof(AudioPlayer));
        return services;
    }
}

