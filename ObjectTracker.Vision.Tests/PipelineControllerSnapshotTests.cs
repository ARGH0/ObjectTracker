using ObjectTracker.Core.Domain;
using ObjectTracker.Core.Ports;
using Xunit;

namespace ObjectTracker.Vision.Tests;

public sealed class PipelineControllerSnapshotTests
{
    [Fact]
    public async Task StartAsync_PublishesPerCameraSourceSnapshotWithSourceAndAnnotatedFrames()
    {
        var sourceFrame = new FramePacket("camera-1", 1234, 2, 2, [1, 2, 3]);
        var output = new RecordingOutputPort();
        await using var controller = new PipelineController(
            new SingleFrameSourceFactory(new SingleFrameSource(sourceFrame)),
            new StubDetectorManager([]),
            new StubTracker([]),
            [output],
            new StubClock(sourceFrame.TimestampUtcMs));

        await controller.StartAsync("camera-1", CancellationToken.None);
        var snapshot = await output.WaitForSnapshotAsync();
        await controller.StopAsync(CancellationToken.None);

        Assert.Equal("camera-1", snapshot.CameraSourceId);
        Assert.Same(sourceFrame, snapshot.SourceFrame);
        Assert.Equal("camera-1", snapshot.AnnotatedFrame.SourceId);
        Assert.Equal(sourceFrame.TimestampUtcMs, snapshot.AnnotatedFrame.TimestampUtcMs);
    }

    [Fact]
    public async Task StartAsync_PublishesSnapshotWithTrainObservationsTrainStatesAndTiming()
    {
        var sourceFrame = new FramePacket("camera-1", 2000, 2, 2, [1, 2, 3]);
        var detection = new Detection("detection-1", 10, 20, 8, 18, 12, 14, 0.75f, "Red", "camera-1", sourceFrame.TimestampUtcMs);
        var trainState = new TrainState(
            "local-train-1",
            "Red",
            10,
            20,
            3,
            90,
            0.8f,
            TrainMotionState.Moving,
            CollisionWarningState.None,
            "camera-1",
            sourceFrame.TimestampUtcMs);
        var output = new RecordingOutputPort();
        await using var controller = new PipelineController(
            new SingleFrameSourceFactory(new SingleFrameSource(sourceFrame)),
            new StubDetectorManager([detection]),
            new StubTracker([trainState]),
            [output],
            new StubClock(sourceFrame.TimestampUtcMs));

        await controller.StartAsync("camera-1", CancellationToken.None);
        var snapshot = await output.WaitForSnapshotAsync();
        await controller.StopAsync(CancellationToken.None);

        var observation = Assert.Single(snapshot.TrainObservations);
        Assert.Equal("Red", observation.TrainColor);
        Assert.Equal("camera-1", observation.SourceId);
        Assert.Empty(snapshot.MovingObjectObservations);
        Assert.Same(trainState, Assert.Single(snapshot.TrainStates));
        Assert.Equal(sourceFrame.TimestampUtcMs, snapshot.Timing.TimestampUtcMs);
        Assert.True(snapshot.Timing.FramesPerSecond >= 0);
        Assert.True(snapshot.Timing.ProcessingMs >= 0);
    }

    [Fact]
    public async Task StartAsync_WhenDebugViewEnabled_PublishesNamedDebugFrames()
    {
        var sourceFrame = new FramePacket("camera-1", 1234, 2, 2, [1, 2, 3]);
        var output = new RecordingOutputPort();
        await using var controller = new PipelineController(
            new SingleFrameSourceFactory(new SingleFrameSource(sourceFrame)),
            new StubDetectorManager([]),
            new StubTracker([]),
            [output],
            new StubClock(sourceFrame.TimestampUtcMs));

        controller.SetDebugViewEnabled("camera-1", enabled: true);

        await controller.StartAsync("camera-1", CancellationToken.None);
        var snapshot = await output.WaitForSnapshotAsync();
        await controller.StopAsync(CancellationToken.None);

        var debugFrame = Assert.Single(snapshot.DebugFrames);
        Assert.Equal("source", debugFrame.Name);
        Assert.Same(sourceFrame, debugFrame.Frame);
    }

    [Fact]
    public async Task StartAsync_WhenDebugViewDisabled_PublishesNoDebugFrames()
    {
        var sourceFrame = new FramePacket("camera-1", 1234, 2, 2, [1, 2, 3]);
        var output = new RecordingOutputPort();
        await using var controller = new PipelineController(
            new SingleFrameSourceFactory(new SingleFrameSource(sourceFrame)),
            new StubDetectorManager([]),
            new StubTracker([]),
            [output],
            new StubClock(sourceFrame.TimestampUtcMs));

        await controller.StartAsync("camera-1", CancellationToken.None);
        var snapshot = await output.WaitForSnapshotAsync();
        await controller.StopAsync(CancellationToken.None);

        Assert.Empty(snapshot.DebugFrames);
    }

    private sealed class SingleFrameSourceFactory(IFrameSource source) : IFrameSourceFactory
    {
        public IReadOnlyList<FrameSourceInfo> GetAvailableSources() => [new(source.Id, source.DisplayName)];

        public IFrameSource Create(string sourceId) => source;
    }

    private sealed class SingleFrameSource(FramePacket frame) : IFrameSource
    {
        private bool consumed;

        public string Id => frame.SourceId;

        public string DisplayName => frame.SourceId;

        public string Diagnostics => "test source";

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<FramePacket?> ReadFrameAsync(CancellationToken cancellationToken)
        {
            if (consumed)
            {
                return Task.FromResult<FramePacket?>(null);
            }

            consumed = true;
            return Task.FromResult<FramePacket?>(frame);
        }

        public string? ConsumeDiagnosticEvent() => null;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class StubDetectorManager(IReadOnlyList<Detection> detections) : IDetectorManager
    {
        public DetectorMode ActiveMode => DetectorMode.Color;

        public IReadOnlyList<DetectorMode> SupportedModes => [DetectorMode.Color];

        public IReadOnlyList<string> AvailableColorFilters => [];

        public IReadOnlyList<string> EnabledColorFilters => [];

        public IReadOnlyList<ColorCalibrationProfile> ColorCalibrations => [];

        public void SwitchMode(DetectorMode mode) { }

        public void SetEnabledColorFilters(IEnumerable<string> colors) { }

        public void SetColorCalibrations(IEnumerable<ColorCalibrationProfile> calibrations) { }

        public Task<IReadOnlyList<Detection>> DetectAsync(FramePacket frame, CancellationToken cancellationToken) => Task.FromResult(detections);
    }

    private sealed class StubTracker(IReadOnlyList<TrainState> trainStates) : ITracker
    {
        public IReadOnlyList<TrainState> Update(IReadOnlyList<Detection> detections, long frameTimestampUtcMs) => trainStates;

        public void Reset() { }
    }

    private sealed class StubClock(long timestamp) : IClock
    {
        public long UtcNowMs() => timestamp;
    }
}
