using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenClaw.KinectSatellite.Core;

namespace OpenClaw.KinectSatellite.Infrastructure;

/// <summary>Runs a MicroWakeWord-compatible ONNX classifier over 40-bin log-mel audio features.</summary>
public sealed class MicroWakeWordService : IWakeWordService, IDisposable
{
    private const int FeatureCount = 40;
    private readonly IUserSettingsStore _settings;
    private readonly ILogger<MicroWakeWordService> _logger;
    private readonly IWakeWordModelInstaller _installer;
    private readonly Queue<float[]> _window = new();
    private InferenceSession? _session;
    private int _positiveFrames;
    private DateTimeOffset _cooldownUntil;

    public MicroWakeWordService(IUserSettingsStore settings, IWakeWordModelInstaller installer, ILogger<MicroWakeWordService> logger)
    {
        _settings = settings;
        _installer = installer;
        _logger = logger;
    }

    public async ValueTask<bool> DetectAsync(AudioFrame frame, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _session ??= await CreateSessionAsync(cancellationToken);
        _window.Enqueue(LogSpectrum(frame.Pcm16.Span));
        while (_window.Count > 30) _window.Dequeue();
        var options = _settings.Current.WakeWord;
        if (_window.Count < 30 || DateTimeOffset.UtcNow < _cooldownUntil) return false;

        var features = _window.SelectMany(x => x).ToArray();
        var inputName = _session.InputMetadata.Keys.First();
        var input = NamedOnnxValue.CreateFromTensor(inputName, new DenseTensor<float>(features, [1, 30, FeatureCount]));
        using var results = _session.Run([input]);
        var score = results.SelectMany(r => r.AsEnumerable<float>()).Max();
        _positiveFrames = score >= options.Threshold ? _positiveFrames + 1 : 0;
        _logger.LogTrace("Wake word score {Score:0.000}", score);
        if (_positiveFrames < options.TriggerFrames) return false;
        _cooldownUntil = DateTimeOffset.UtcNow.AddSeconds(options.CooldownSeconds);
        _positiveFrames = 0;
        return true;
    }

    public void Reset()
    {
        _window.Clear();
        _positiveFrames = 0;
    }

    private async Task<InferenceSession> CreateSessionAsync(CancellationToken cancellationToken)
    {
        var installed = await _installer.EnsureInstalledAsync(_settings.Current.WakeWord.WakeWordId, cancellationToken);
        var path = installed.ModelPath;
        if (!string.Equals(Path.GetExtension(path), ".onnx", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"The official model is '{Path.GetExtension(path)}', which this ONNX build cannot execute. The verified original was retained at '{path}'; it was not renamed or converted.");
        _logger.LogInformation("Loading MicroWakeWord model {Model}", path);
        return new InferenceSession(path);
    }

    internal static float[] LogSpectrum(ReadOnlySpan<byte> pcm)
    {
        var sampleCount = Math.Min(pcm.Length / 2, 480);
        var output = new float[FeatureCount];
        if (sampleCount == 0) return output;
        for (var band = 0; band < FeatureCount; band++)
        {
            var k = 1 + band * (sampleCount / 2 - 1) / FeatureCount;
            double real = 0, imaginary = 0;
            for (var n = 0; n < sampleCount; n++)
            {
                var sample = BitConverter.ToInt16(pcm.Slice(n * 2, 2)) / 32768f;
                var hann = .5 - .5 * Math.Cos(2 * Math.PI * n / Math.Max(1, sampleCount - 1));
                var angle = 2 * Math.PI * k * n / sampleCount;
                real += sample * hann * Math.Cos(angle);
                imaginary -= sample * hann * Math.Sin(angle);
            }
            output[band] = (float)Math.Log10(1e-6 + real * real + imaginary * imaginary);
        }
        return output;
    }

    public void Dispose() => _session?.Dispose();
}
