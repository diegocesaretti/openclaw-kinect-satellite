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
    [Required] public string ModelPath { get; init; } = "models/okay-nabu.onnx";
    [Range(0, 1)] public float Threshold { get; init; } = .7f;
    [Range(1, 20)] public int TriggerFrames { get; init; } = 3;
    [Range(0, 30)] public int CooldownSeconds { get; init; } = 2;
}

public sealed class HomeAssistantOptions
{
    public const string Section = "HomeAssistant";
    [Required, Url] public string Url { get; init; } = "http://homeassistant.local:8123";
    [Required] public string AccessToken { get; init; } = "";
    public string? PipelineId { get; init; }
    public string? DeviceId { get; init; }
}

