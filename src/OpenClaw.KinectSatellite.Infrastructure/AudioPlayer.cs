using NAudio.Wave;
using OpenClaw.KinectSatellite.Core;

namespace OpenClaw.KinectSatellite.Infrastructure;

public sealed class AudioPlayer(IHttpClientFactory clients) : IAudioPlayer
{
    public async Task PlayAsync(Uri media, CancellationToken cancellationToken)
    {
        var bytes = await clients.CreateClient(nameof(AudioPlayer)).GetByteArrayAsync(media, cancellationToken);
        var extension = Path.GetExtension(media.AbsolutePath);
        var path = Path.Combine(Path.GetTempPath(), $"openclaw-{Guid.NewGuid():N}{(string.IsNullOrEmpty(extension) ? ".mp3" : extension)}");
        await File.WriteAllBytesAsync(path, bytes, cancellationToken);
        try
        {
            using var reader = new AudioFileReader(path);
            using var output = new WaveOutEvent();
            output.Init(reader);
            output.Play();
            while (output.PlaybackState == PlaybackState.Playing)
                await Task.Delay(50, cancellationToken);
        }
        finally { File.Delete(path); }
    }
}
