namespace OpenClaw.KinectSatellite.Core;

public sealed record AudioFrame(ReadOnlyMemory<byte> Pcm16, DateTimeOffset Timestamp)
{
    public const int SampleRate = 16_000;
    public const int Channels = 1;
}

public interface IKinectService : IAsyncDisposable
{
    IAsyncEnumerable<AudioFrame> CaptureAsync(CancellationToken cancellationToken);
}

public interface IWakeWordService
{
    ValueTask<bool> DetectAsync(AudioFrame frame, CancellationToken cancellationToken);
    void Reset();
}

public interface IViewAssistClient
{
    Task RunPipelineAsync(IAsyncEnumerable<AudioFrame> audio, CancellationToken cancellationToken);
}

public interface IAudioPlayer
{
    Task PlayAsync(Uri media, CancellationToken cancellationToken);
}

