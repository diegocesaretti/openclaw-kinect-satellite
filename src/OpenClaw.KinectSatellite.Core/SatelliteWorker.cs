using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OpenClaw.KinectSatellite.Core;

public sealed class SatelliteWorker(
    IKinectService kinect,
    IWakeWordService wakeWord,
    IViewAssistClient assist,
    ILogger<SatelliteWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("OpenClaw satellite listening for its wake word");
        await foreach (var frame in kinect.CaptureAsync(stoppingToken))
        {
            if (!await wakeWord.DetectAsync(frame, stoppingToken)) continue;

            logger.LogInformation("Wake word detected; starting Home Assistant Assist pipeline");
            try
            {
                await assist.RunPipelineAsync(kinect.CaptureAsync(stoppingToken), stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Assist pipeline failed");
            }
            finally
            {
                wakeWord.Reset();
            }
        }
    }
}
