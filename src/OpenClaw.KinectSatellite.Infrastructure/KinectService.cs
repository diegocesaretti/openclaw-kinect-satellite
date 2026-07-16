using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
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
        var sensorType = assembly.GetType("Microsoft.Kinect.KinectSensor", true)
            ?? throw new InvalidOperationException("Microsoft.Kinect.dll loaded, but KinectSensor type was not found.");
        var sensorsProperty = sensorType.GetProperty("KinectSensors", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("Microsoft.Kinect.KinectSensor.KinectSensors was not found.");
        var sensors = (System.Collections.IEnumerable)(sensorsProperty.GetValue(null)
            ?? throw new InvalidOperationException("Kinect SDK returned a null sensor collection."));
        var statuses = sensors.Cast<object>().Select(s => s.GetType().GetProperty("Status")?.GetValue(s)?.ToString() ?? "Unknown").ToArray();
        _sensor = sensors.Cast<object>().FirstOrDefault(s => s.GetType().GetProperty("Status")?.GetValue(s)?.ToString() == "Connected")
            ?? throw new InvalidOperationException($"No connected Kinect SDK v1 sensor was found. Reported statuses: {(statuses.Length == 0 ? "<none>" : string.Join(", ", statuses))}.");

        try
        {
            Invoke(_sensor, sensorType.GetMethod("Start") ?? throw new MissingMethodException(sensorType.FullName, "Start"), "Kinect sensor start");
            var source = sensorType.GetProperty("AudioSource")?.GetValue(_sensor)
                ?? throw new InvalidOperationException("Kinect sensor returned no AudioSource.");
            SetEnum(source, "BeamAngleMode", "Adaptive");
            var options = settings.Current.Kinect;
            ConfigureAudio(source, options.EchoCancellation, options.NoiseSuppression);

            try
            {
                _stream = (Stream)(Invoke(source, source.GetType().GetMethod("Start") ?? throw new MissingMethodException(source.GetType().FullName, "Start"), "Kinect microphone-array start")
                    ?? throw new InvalidOperationException("Kinect AudioSource.Start returned no audio stream."));
            }
            catch (InvalidOperationException first) when (options.EchoCancellation || options.NoiseSuppression)
            {
                logger.LogWarning(first, "Kinect enhanced audio processing failed; retrying with AEC and noise suppression disabled");
                ConfigureAudio(source, false, false);
                _stream = (Stream)(Invoke(source, source.GetType().GetMethod("Start") ?? throw new MissingMethodException(source.GetType().FullName, "Start"), "Kinect microphone-array fallback start")
                    ?? throw new InvalidOperationException("Kinect AudioSource.Start returned no audio stream."));
            }

            logger.LogInformation("Kinect microphone array started with adaptive beamforming (requested AEC={Aec}, NS={Ns})", options.EchoCancellation, options.NoiseSuppression);
        }
        catch
        {
            try { Invoke(_sensor, sensorType.GetMethod("Stop")!, "Kinect sensor cleanup"); } catch { }
            _sensor = null;
            _stream = null;
            throw;
        }
    }

    private static void ConfigureAudio(object source, bool echoCancellation, bool noiseSuppression)
    {
        SetEnum(source, "EchoCancellationMode", echoCancellation ? "CancellationOnly" : "None");
        SetProperty(source, "AutomaticGainControlEnabled", true);
        SetProperty(source, "NoiseSuppression", noiseSuppression);
    }

    private static Assembly LoadKinectAssembly()
    {
        var failures = new List<string>();
        try { return Assembly.Load("Microsoft.Kinect"); }
        catch (Exception ex) { failures.Add($"Assembly.Load: {ex.GetType().Name}: {ex.Message}"); }

        foreach (var candidate in GetKinectAssemblyCandidates())
        {
            if (!File.Exists(candidate)) continue;
            try
            {
                return AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(candidate));
            }
            catch (Exception ex)
            {
                failures.Add($"{candidate}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        var searched = string.Join(Environment.NewLine, GetKinectAssemblyCandidates().Select(x => $"  - {x}"));
        var details = string.Join(Environment.NewLine, failures.Select(x => $"  - {x}"));
        throw new InvalidOperationException(
            $"Microsoft.Kinect.dll from Kinect for Windows SDK v1.8 could not be loaded.{Environment.NewLine}" +
            $"Process architecture: {RuntimeInformation.ProcessArchitecture}.{Environment.NewLine}" +
            $"KINECTSDK10_DIR: {Environment.GetEnvironmentVariable("KINECTSDK10_DIR") ?? "<not set>"}.{Environment.NewLine}" +
            $"Searched:{Environment.NewLine}{searched}{Environment.NewLine}" +
            $"Load failures:{Environment.NewLine}{details}{Environment.NewLine}" +
            "Repair/install Kinect for Windows SDK or Runtime v1.8, then restart Windows. You may also copy the SDK's Microsoft.Kinect.dll beside the executable.");
    }

    public static IReadOnlyList<string> GetKinectAssemblyCandidates()
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine(AppContext.BaseDirectory, "Microsoft.Kinect.dll")
        };

        AddSdkRoot(candidates, Environment.GetEnvironmentVariable("KINECTSDK10_DIR"));
        AddSdkRoot(candidates, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft SDKs", "Kinect", "v1.8"));
        AddSdkRoot(candidates, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft SDKs", "Kinect", "v1.8"));

        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        foreach (var gac in new[] { "GAC_MSIL", "GAC_32" })
        {
            var root = Path.Combine(windows, "Microsoft.NET", "assembly", gac, "Microsoft.Kinect");
            if (!Directory.Exists(root)) continue;
            try
            {
                foreach (var path in Directory.EnumerateFiles(root, "Microsoft.Kinect.dll", SearchOption.AllDirectories))
                    candidates.Add(path);
            }
            catch (Exception)
            {
                // A protected GAC entry should not prevent checking the remaining supported locations.
            }
        }

        return candidates.ToArray();
    }

    private static void AddSdkRoot(ISet<string> candidates, string? root)
    {
        if (string.IsNullOrWhiteSpace(root)) return;
        candidates.Add(Path.Combine(root, "Assemblies", "Microsoft.Kinect.dll"));
        candidates.Add(Path.Combine(root, "Microsoft.Kinect.dll"));
    }

    private static object? Invoke(object target, MethodInfo method, string operation)
    {
        try { return method.Invoke(target, null); }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw new InvalidOperationException($"{operation} failed: {Describe(ex.InnerException)}", ex.InnerException);
        }
    }

    private static void SetProperty(object target, string name, object value)
    {
        var property = target.GetType().GetProperty(name);
        if (property is null) return;
        try { property.SetValue(target, value); }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw new InvalidOperationException($"Kinect audio property {name} failed: {Describe(ex.InnerException)}", ex.InnerException);
        }
    }

    private static void SetEnum(object target, string name, string value)
    {
        var property = target.GetType().GetProperty(name);
        if (property is null) return;
        try { property.SetValue(target, Enum.Parse(property.PropertyType, value)); }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw new InvalidOperationException($"Kinect audio property {name}={value} failed: {Describe(ex.InnerException)}", ex.InnerException);
        }
    }

    private static string Describe(Exception exception)
    {
        var messages = new List<string>();
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            var message = current.Message;
            if (current is ExternalException external)
                message += $" (HRESULT 0x{external.ErrorCode:X8})";
            if (!messages.Contains(message, StringComparer.Ordinal))
                messages.Add(message);
        }
        return string.Join(" -> ", messages);
    }

    public ValueTask DisposeAsync()
    {
        _stream?.Dispose();
        _sensor?.GetType().GetMethod("Stop")?.Invoke(_sensor, null);
        return ValueTask.CompletedTask;
    }
}
