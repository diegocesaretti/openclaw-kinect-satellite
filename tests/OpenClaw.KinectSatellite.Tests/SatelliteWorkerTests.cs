using Microsoft.Extensions.Logging.Abstractions;
using OpenClaw.KinectSatellite.Core;
using System.Runtime.CompilerServices;

namespace OpenClaw.KinectSatellite.Tests;

public sealed class SatelliteWorkerTests
{
    [Fact]
    public async Task ControllerReportsRunningAndStoppedStates()
    {
        var worker = new SatelliteWorker(new FakeKinect(), new NeverWake(), new FakeAssist(), NullLogger<SatelliteWorker>.Instance);
        var controller = new SatelliteController(worker, NullLogger<SatelliteController>.Instance);
        await controller.StartAsync();
        Assert.Equal(SatelliteState.Running, controller.State);
        await controller.StopAsync();
        Assert.Equal(SatelliteState.Stopped, controller.State);
    }

    [Fact]
    public async Task StartsPipelineAfterWakeWord()
    {
        var kinect = new FakeKinect();
        var assist = new FakeAssist();
        var worker = new SatelliteWorker(kinect, new AlwaysWake(), assist, NullLogger<SatelliteWorker>.Instance);
        await worker.StartAsync(CancellationToken.None);
        await assist.Called.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await worker.StopAsync(CancellationToken.None);
        Assert.True(await assist.Called.Task);
    }

    private sealed class FakeKinect : IKinectService
    {
        public async IAsyncEnumerable<AudioFrame> CaptureAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return new AudioFrame(new byte[960], DateTimeOffset.UtcNow);
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    private sealed class AlwaysWake : IWakeWordService
    {
        public ValueTask<bool> DetectAsync(AudioFrame frame, CancellationToken cancellationToken) => ValueTask.FromResult(true);
        public void Reset() { }
    }
    private sealed class NeverWake : IWakeWordService
    {
        public ValueTask<bool> DetectAsync(AudioFrame frame, CancellationToken cancellationToken) => ValueTask.FromResult(false);
        public void Reset() { }
    }
    private sealed class FakeAssist : IViewAssistClient
    {
        public TaskCompletionSource<bool> Called { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task RunPipelineAsync(IAsyncEnumerable<AudioFrame> audio, CancellationToken cancellationToken) { Called.SetResult(true); return Task.CompletedTask; }
    }
}
