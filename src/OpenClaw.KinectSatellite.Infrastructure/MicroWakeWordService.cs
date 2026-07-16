using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenClaw.KinectSatellite.Core;

namespace OpenClaw.KinectSatellite.Infrastructure;

/// <summary>Runs the official openWakeWord ONNX streaming feature and classifier pipeline.</summary>
public sealed class OpenWakeWordService : IWakeWordService, IDisposable
{
    private const int ChunkSamples = 1_280; // 80 ms at 16 kHz
    private const int OverlapSamples = 480;
    private const int MelBins = 32;
    private const int MelWindowFrames = 76;
    private const int EmbeddingSize = 96;

    private readonly IUserSettingsStore _settings;
    private readonly IWakeWordModelInstaller _installer;
    private readonly ILogger<OpenWakeWordService> _logger;
    private readonly List<short> _pending = [];
    private readonly List<short> _overlap = [];
    private readonly List<float[]> _melFrames = [];
    private readonly List<float[]> _embeddings = [];

    private InferenceSession? _melSession;
    private InferenceSession? _embeddingSession;
    private InferenceSession? _classifierSession;
    private string? _loadedWakeWord;
    private int _classifierFrames = 16;
    private int _positiveFrames;
    private DateTimeOffset _cooldownUntil;

    public OpenWakeWordService(IUserSettingsStore settings, IWakeWordModelInstaller installer, ILogger<OpenWakeWordService> logger)
    {
        _settings = settings;
        _installer = installer;
        _logger = logger;
        Reset();
    }

    public async ValueTask<bool> DetectAsync(AudioFrame frame, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var selected = _settings.Current.WakeWord.WakeWordId;
        if (_classifierSession is null || !string.Equals(_loadedWakeWord, selected, StringComparison.Ordinal))
            await LoadAsync(selected, cancellationToken);

        var pcm = frame.Pcm16.Span;
        for (var i = 0; i + 1 < pcm.Length; i += 2)
            _pending.Add(BitConverter.ToInt16(pcm.Slice(i, 2)));

        var detected = false;
        while (_pending.Count >= ChunkSamples)
        {
            var chunk = _pending.GetRange(0, ChunkSamples);
            _pending.RemoveRange(0, ChunkSamples);
            var inputSamples = new short[_overlap.Count + chunk.Count];
            _overlap.CopyTo(inputSamples, 0);
            chunk.CopyTo(inputSamples, _overlap.Count);
            _overlap.Clear();
            _overlap.AddRange(chunk.Skip(Math.Max(0, chunk.Count - OverlapSamples)));

            AppendMelFrames(inputSamples);
            AppendEmbedding();
            if (EvaluateClassifier())
                detected = true;
        }

        return detected;
    }

    public void Reset()
    {
        _pending.Clear();
        _overlap.Clear();
        _melFrames.Clear();
        _embeddings.Clear();
        for (var i = 0; i < MelWindowFrames; i++)
            _melFrames.Add(Enumerable.Repeat(1f, MelBins).ToArray());
        _positiveFrames = 0;
        _cooldownUntil = DateTimeOffset.MinValue;
    }

    private async Task LoadAsync(string wakeWordId, CancellationToken cancellationToken)
    {
        var installed = await _installer.EnsureInstalledAsync(wakeWordId, cancellationToken);
        DisposeSessions();
        try
        {
            _melSession = new InferenceSession(installed.MelSpectrogramPath);
            _embeddingSession = new InferenceSession(installed.EmbeddingPath);
            _classifierSession = new InferenceSession(installed.ClassifierPath);
            var dimensions = _classifierSession.InputMetadata.Values.First().Dimensions;
            _classifierFrames = dimensions.Length > 1 && dimensions[1] > 0 ? dimensions[1] : 16;
            _loadedWakeWord = wakeWordId;
            Reset();
            _logger.LogInformation("Loaded openWakeWord ONNX model {WakeWord} with {Frames} embedding frames", wakeWordId, _classifierFrames);
        }
        catch
        {
            DisposeSessions();
            throw;
        }
    }

    private void AppendMelFrames(short[] samples)
    {
        var session = _melSession ?? throw new InvalidOperationException("The mel-spectrogram model is not loaded.");
        var values = samples.Select(x => (float)x).ToArray();
        var inputName = session.InputMetadata.Keys.First();
        var input = NamedOnnxValue.CreateFromTensor(inputName, new DenseTensor<float>(values, [1, values.Length]));
        using var results = session.Run([input]);
        var output = results.First().AsEnumerable<float>().ToArray();
        if (output.Length == 0 || output.Length % MelBins != 0)
            throw new InvalidDataException($"openWakeWord mel-spectrogram output has unexpected length {output.Length}.");

        for (var offset = 0; offset < output.Length; offset += MelBins)
        {
            var row = new float[MelBins];
            for (var bin = 0; bin < MelBins; bin++)
                row[bin] = output[offset + bin] / 10f + 2f;
            _melFrames.Add(row);
        }

        if (_melFrames.Count > 970)
            _melFrames.RemoveRange(0, _melFrames.Count - 970);
    }

    private void AppendEmbedding()
    {
        if (_melFrames.Count < MelWindowFrames) return;
        var session = _embeddingSession ?? throw new InvalidOperationException("The embedding model is not loaded.");
        var values = _melFrames.Skip(_melFrames.Count - MelWindowFrames).SelectMany(x => x).ToArray();
        var inputName = session.InputMetadata.Keys.First();
        var input = NamedOnnxValue.CreateFromTensor(inputName, new DenseTensor<float>(values, [1, MelWindowFrames, MelBins, 1]));
        using var results = session.Run([input]);
        var output = results.First().AsEnumerable<float>().ToArray();
        if (output.Length < EmbeddingSize)
            throw new InvalidDataException($"openWakeWord embedding output has unexpected length {output.Length}.");
        _embeddings.Add(output.TakeLast(EmbeddingSize).ToArray());
        if (_embeddings.Count > 120)
            _embeddings.RemoveRange(0, _embeddings.Count - 120);
    }

    private bool EvaluateClassifier()
    {
        if (_embeddings.Count < _classifierFrames || DateTimeOffset.UtcNow < _cooldownUntil) return false;
        var session = _classifierSession ?? throw new InvalidOperationException("The wake-word classifier is not loaded.");
        var values = _embeddings.Skip(_embeddings.Count - _classifierFrames).SelectMany(x => x).ToArray();
        var inputName = session.InputMetadata.Keys.First();
        var input = NamedOnnxValue.CreateFromTensor(inputName, new DenseTensor<float>(values, [1, _classifierFrames, EmbeddingSize]));
        using var results = session.Run([input]);
        var score = results.SelectMany(x => x.AsEnumerable<float>()).Max();
        var options = _settings.Current.WakeWord;
        _positiveFrames = score >= options.Threshold ? _positiveFrames + 1 : 0;
        _logger.LogTrace("openWakeWord {WakeWord} score {Score:0.000}", _loadedWakeWord, score);
        if (_positiveFrames < options.TriggerFrames) return false;
        _positiveFrames = 0;
        _cooldownUntil = DateTimeOffset.UtcNow.AddSeconds(options.CooldownSeconds);
        return true;
    }

    private void DisposeSessions()
    {
        _melSession?.Dispose();
        _embeddingSession?.Dispose();
        _classifierSession?.Dispose();
        _melSession = null;
        _embeddingSession = null;
        _classifierSession = null;
    }

    public void Dispose() => DisposeSessions();
}
