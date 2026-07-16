using Microsoft.Extensions.Logging.Abstractions;
using OpenClaw.KinectSatellite.Core;
using OpenClaw.KinectSatellite.Infrastructure;

namespace OpenClaw.KinectSatellite.Tests;

public sealed class OpenWakeWordCompatibilityTests
{
    [Fact]
    [Trait("Category", "Compatibility")]
    public async Task OfficialAlexaAndJarvisOnnxBundlesLoadAndRun()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        var installer = new WakeWordModelInstaller(new WakeWordCatalog(), new SingleClientFactory(http), NullLogger<WakeWordModelInstaller>.Instance);
        var settings = new MutableSettingsStore("alexa");
        using var service = new OpenWakeWordService(settings, installer, NullLogger<OpenWakeWordService>.Instance);
        var silence = new AudioFrame(new byte[1_280 * sizeof(short)], DateTimeOffset.UtcNow);

        await RunFramesAsync(service, silence);
        settings.Select("hey_jarvis");
        await RunFramesAsync(service, silence);
    }

    private static async Task RunFramesAsync(OpenWakeWordService service, AudioFrame frame)
    {
        for (var i = 0; i < 30; i++)
            await service.DetectAsync(frame, CancellationToken.None);
    }

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class MutableSettingsStore(string wakeWord) : IUserSettingsStore
    {
        public SatelliteSettings Current { get; private set; } = Create(wakeWord);
        public void Select(string id) => Current = Create(id);
        public Task SaveAsync(SatelliteSettings settings, CancellationToken cancellationToken = default)
        {
            Current = settings;
            return Task.CompletedTask;
        }

        private static SatelliteSettings Create(string id) =>
            new(new(), new WakeWordOptions { WakeWordId = id, Threshold = .5f, TriggerFrames = 2 }, new(), new());
    }
}
