using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OpenClaw.KinectSatellite.Core;

public sealed class SatelliteWorker(
    IKinectService kinect,
    IWakeWordService wakeWord,
    IViewAssistClient assist,
    ILogger<SatelliteWorker> logger) : BackgroundService
{
    public event EventHandler<Exception>? Faulted;
    public Exception? LastFailure { get; private set; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            LastFailure = null;
            logger.LogInformation("OpenClaw satellite listening for its wake word");
            await foreach (var frame in kinect.CaptureAsync(stoppingToken))
            {
                if (!await wakeWord.DetectAsync(frame, stoppingToken)) continue;
                logger.LogInformation("Wake word detected; starting Home Assistant Assist pipeline");
                try { await assist.RunPipelineAsync(kinect.CaptureAsync(stoppingToken), stoppingToken); }
                catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogError(ex, "Assist pipeline failed"); }
                finally { wakeWord.Reset(); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception ex) { LastFailure = ex; logger.LogError(ex, "Satellite stopped unexpectedly"); Faulted?.Invoke(this, ex); }
    }
}

public sealed class SatelliteController : ISatelliteController
{
    private readonly SatelliteWorker _worker;
    private readonly ILogger<SatelliteController> _logger;
    public SatelliteState State { get; private set; } = SatelliteState.Stopped;
    public string? LastError { get; private set; }
    public event EventHandler? StateChanged;

    public SatelliteController(SatelliteWorker worker, ILogger<SatelliteController> logger)
    {
        _worker = worker;
        _logger = logger;
        _worker.Faulted += (_, exception) =>
        {
            LastError = exception.Message;
            SetState(SatelliteState.Faulted);
        };
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (State is SatelliteState.Running or SatelliteState.Starting) return;
        SetState(SatelliteState.Starting);
        try
        {
            await _worker.StartAsync(cancellationToken);
            if (_worker.LastFailure is not null) throw new InvalidOperationException(_worker.LastFailure.Message, _worker.LastFailure);
            LastError = null;
            SetState(SatelliteState.Running);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            _logger.LogError(ex, "Could not start satellite");
            SetState(SatelliteState.Faulted);
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (State is SatelliteState.Stopped or SatelliteState.Stopping) return;
        SetState(SatelliteState.Stopping);
        try { await _worker.StopAsync(cancellationToken); SetState(SatelliteState.Stopped); }
        catch (Exception ex) { LastError = ex.Message; SetState(SatelliteState.Faulted); throw; }
    }

    private void SetState(SatelliteState state) { State = state; StateChanged?.Invoke(this, EventArgs.Empty); }
}
