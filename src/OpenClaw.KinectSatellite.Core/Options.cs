using System.ComponentModel.DataAnnotations;

namespace OpenClaw.KinectSatellite.Core;

public sealed class KinectOptions
{
    public const string Section = "Kinect";
    public bool EchoCancellation { get; init; } = true;
    public bool NoiseSuppression { get; init; } = true;
    [Range(10, 1000)] public int FrameMilliseconds { get; init; } = 30;
}

public sealed class WakeWordOptions
{
    public const string Section = "WakeWord";
    [Required] public string WakeWordId { get; init; } = "alexa";
    [Range(0, 1)] public float Threshold { get; init; } = .7f;
    [Range(1, 20)] public int TriggerFrames { get; init; } = 3;
    [Range(0, 30)] public int CooldownSeconds { get; init; } = 2;
}

public sealed record WakeWordModelDefinition(
    string Id,
    string DisplayName,
    Uri ManifestUri,
    Uri LicenseUri,
    float DefaultThreshold,
    int DefaultTriggerFrames,
    string Attribution);

public sealed record InstalledWakeWordModel(string ModelPath, string Sha256, string SourceUrl, string LicensePath);

public interface IWakeWordCatalog
{
    IReadOnlyList<WakeWordModelDefinition> Models { get; }
    WakeWordModelDefinition Get(string id);
}

public interface IWakeWordModelInstaller
{
    Task<InstalledWakeWordModel> EnsureInstalledAsync(string id, CancellationToken cancellationToken = default);
}

public sealed class HomeAssistantOptions
{
    public const string Section = "HomeAssistant";
    [Required, Url] public string Url { get; init; } = "http://homeassistant.local:8123";
    [Required] public string AccessToken { get; init; } = "";
    public string? PipelineId { get; init; }
    public string? DeviceId { get; init; }
}

public sealed class ApplicationOptions
{
    public string LoggingLevel { get; init; } = "Information";
    public bool LaunchAtStartup { get; init; }
}

public sealed record SatelliteSettings(
    KinectOptions Kinect,
    WakeWordOptions WakeWord,
    HomeAssistantOptions HomeAssistant,
    ApplicationOptions Application);

public interface IUserSettingsStore
{
    SatelliteSettings Current { get; }
    Task SaveAsync(SatelliteSettings settings, CancellationToken cancellationToken = default);
}

public interface IHomeAssistantConnectionTester
{
    Task<(bool Success, string Message)> TestAsync(string url, string accessToken, CancellationToken cancellationToken = default);
}

public enum SatelliteState { Stopped, Starting, Running, Stopping, Faulted }

public interface ISatelliteController
{
    SatelliteState State { get; }
    string? LastError { get; }
    event EventHandler? StateChanged;
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
