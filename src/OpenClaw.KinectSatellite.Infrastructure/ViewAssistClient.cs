using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenClaw.KinectSatellite.Core;

namespace OpenClaw.KinectSatellite.Infrastructure;

/// <summary>Home Assistant WebSocket Assist-pipeline client used by the View Assist satellite.</summary>
public sealed class ViewAssistClient(IOptions<HomeAssistantOptions> options, IAudioPlayer player, ILogger<ViewAssistClient> logger) : IViewAssistClient
{
    public async Task RunPipelineAsync(IAsyncEnumerable<AudioFrame> audio, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(WebSocketUri(settings.Url), cancellationToken);
        _ = await ReceiveJsonAsync(socket, cancellationToken); // auth_required
        await SendJsonAsync(socket, new { type = "auth", access_token = settings.AccessToken }, cancellationToken);
        var auth = await ReceiveJsonAsync(socket, cancellationToken);
        if (auth.GetProperty("type").GetString() != "auth_ok") throw new InvalidOperationException("Home Assistant authentication failed.");

        await SendJsonAsync(socket, new
        {
            id = 1,
            type = "assist_pipeline/run",
            start_stage = "stt",
            end_stage = "tts",
            input = new { sample_rate = AudioFrame.SampleRate },
            pipeline = settings.PipelineId,
            device_id = settings.DeviceId
        }, cancellationToken);

        var response = await ReceiveJsonAsync(socket, cancellationToken);
        var handler = response.GetProperty("result").GetProperty("audio_binary_handler_id").GetByte();
        using var sendCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var sending = StreamAudioAsync(socket, handler, audio, sendCts.Token);
        try
        {
            while (true)
            {
                var message = await ReceiveJsonAsync(socket, cancellationToken);
                if (!message.TryGetProperty("event", out var pipelineEvent)) continue;
                var eventType = pipelineEvent.GetProperty("type").GetString();
                logger.LogDebug("Assist pipeline event {EventType}", eventType);
                if (eventType == "tts-end" && TryGetTtsUrl(pipelineEvent, settings.Url, out var media))
                    await player.PlayAsync(media, cancellationToken);
                if (eventType is "run-end" or "error") break;
            }
        }
        finally
        {
            sendCts.Cancel();
            try { await sending; } catch (OperationCanceledException) { }
        }
    }

    private static async Task StreamAudioAsync(ClientWebSocket socket, byte handler, IAsyncEnumerable<AudioFrame> audio, CancellationToken token)
    {
        await foreach (var frame in audio.WithCancellation(token))
        {
            var packet = new byte[frame.Pcm16.Length + 1];
            packet[0] = handler;
            frame.Pcm16.CopyTo(packet.AsMemory(1));
            await socket.SendAsync(packet, WebSocketMessageType.Binary, true, token);
        }
    }

    private static bool TryGetTtsUrl(JsonElement pipelineEvent, string baseUrl, out Uri uri)
    {
        uri = default!;
        if (!pipelineEvent.TryGetProperty("data", out var data) || !data.TryGetProperty("tts_output", out var output) ||
            !output.TryGetProperty("url", out var url)) return false;
        uri = new Uri(new Uri(baseUrl.TrimEnd('/') + "/"), url.GetString()!.TrimStart('/'));
        return true;
    }

    private static Uri WebSocketUri(string url)
    {
        var builder = new UriBuilder(url) { Scheme = url.StartsWith("https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws", Path = "/api/websocket" };
        return builder.Uri;
    }

    private static async Task SendJsonAsync(ClientWebSocket socket, object value, CancellationToken token) =>
        await socket.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)), WebSocketMessageType.Text, true, token);

    private static async Task<JsonElement> ReceiveJsonAsync(ClientWebSocket socket, CancellationToken token)
    {
        using var data = new MemoryStream();
        var buffer = new byte[8192];
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, token);
            if (result.MessageType == WebSocketMessageType.Close) throw new WebSocketException("Home Assistant closed the connection.");
            data.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);
        using var document = JsonDocument.Parse(data.ToArray());
        return document.RootElement.Clone();
    }
}
