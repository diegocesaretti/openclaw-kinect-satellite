using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using OpenClaw.KinectSatellite.Core;

namespace OpenClaw.KinectSatellite.Infrastructure;

/// <summary>Loads Kinect SDK v1 at runtime so the rest of the application remains testable without Kinect hardware.</summary>
public sealed class KinectService(IUserSettingsStore settings, ILogger<KinectService> logger) : IKinectService
{
    private object? _sensor;
    private Stream? _stream;

    public async IAsyncEnumerable<AudioFrame> CaptureAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        EnsureStarted();
        var size = AudioFrame.SampleRate * sizeof(short) * settings.Current.Kinect.FrameMilliseconds / 1000;
        var buffer = new byte[size];
        while (!cancellationToken.IsCancellationRequested)
        {
            var count = await _stream!.ReadAsync(buffer, cancellationToken);
            if (count == 0) yield break;
            yield return new AudioFrame(buffer.AsMemory(0, count).ToArray(), DateTimeOffset.UtcNow);
        }
    }

    private void EnsureStarted()
    {
        if (_stream is not null) return;
        var assembly = LoadKinectAssembly();
        var sensorType = assembly.GetType("Microsoft.Kinect.KinectSensor", true)!;
        var sensors = (System.Collections.IEnumerable)sensorType.GetProperty("KinectSensors", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
        _sensor = sensors.Cast<object>().FirstOrDefault(s => s.GetType().GetProperty("Status")?.GetValue(s)?.ToString() == "Connected")
            ?? throw new InvalidOperationException("No connected Kinect SDK v1 sensor was found.");
        sensorType.GetMethod("Start")!.Invoke(_sensor, null);
        var source = sensorType.GetProperty("AudioSource")!.GetValue(_sensor)!;
        SetEnum(source, "BeamAngleMode", "Adaptive");
        var options = settings.Current.Kinect;
        SetEnum(source, "EchoCancellationMode", options.EchoCancellation ? "CancellationOnly" : "None");
        SetProperty(source, "AutomaticGainControlEnabled", true);
        SetProperty(source, "NoiseSuppression", options.NoiseSuppression);
        _stream = (Stream)source.GetType().GetMethod("Start")!.Invoke(source, null)!;
        logger.LogInformation("Kinect microphone array started with adaptive beamforming (AEC={Aec}, NS={Ns})", options.EchoCancellation, options.NoiseSuppression);
    }

    private static Assembly LoadKinectAssembly()
    {
        try { return Assembly.Load("Microsoft.Kinect"); }
        catch (Exception ex) { throw new InvalidOperationException("Microsoft Kinect SDK v1.8 is required. Install it and run this process as x86.", ex); }
    }

    private static void SetProperty(object target, string name, object value) => target.GetType().GetProperty(name)?.SetValue(target, value);
    private static void SetEnum(object target, string name, string value)
    {
        var property = target.GetType().GetProperty(name);
        if (property is not null) property.SetValue(target, Enum.Parse(property.PropertyType, value));
    }

    public ValueTask DisposeAsync()
    {
        _stream?.Dispose();
        _sensor?.GetType().GetMethod("Stop")?.Invoke(_sensor, null);
        return ValueTask.CompletedTask;
    }
}
