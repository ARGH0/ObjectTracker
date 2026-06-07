using ObjectTracker.Core.Domain;
using ObjectTracker.Core.Domain.Enums;
using ObjectTracker.Core.Ports;
using System.Runtime.ExceptionServices;
using Xunit;
using Cv = OpenCvSharp;

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
        var trainObservation = new TrainObservation("camera-1", sourceFrame.TimestampUtcMs, "Red", 10, 20, 8, 18, 12, 14, 0.75f);
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
            new StubVisualObservationPipeline(new VisualObservationResult([], [trainObservation], [])),
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
    public async Task StartAsync_SeparatesMovingObjectObservationsFromTrainObservations()
    {
        var sourceFrame = new FramePacket("camera-1", 2100, 2, 2, [1, 2, 3]);
        var movingEvidence = new MovingObjectObservation("camera-1", sourceFrame.TimestampUtcMs, 5, 6, 1, 2, 3, 4, 0.5f);
        var trainColorEvidence = new TrainObservation("camera-1", sourceFrame.TimestampUtcMs, "Red", 10, 20, 8, 18, 12, 14, 0.75f);
        var output = new RecordingOutputPort();
        await using var controller = new PipelineController(
            new SingleFrameSourceFactory(new SingleFrameSource(sourceFrame)),
            new StubVisualObservationPipeline(new VisualObservationResult([movingEvidence], [trainColorEvidence], [])),
            new StubTracker([]),
            [output],
            new StubClock(sourceFrame.TimestampUtcMs));

        await controller.StartAsync("camera-1", CancellationToken.None);
        var snapshot = await output.WaitForSnapshotAsync();
        await controller.StopAsync(CancellationToken.None);

        var movingObservation = Assert.Single(snapshot.MovingObjectObservations);
        Assert.Equal("camera-1", movingObservation.SourceId);
        Assert.Equal(5, movingObservation.X);
        var trainObservation = Assert.Single(snapshot.TrainObservations);
        Assert.Equal("Red", trainObservation.TrainColor);
    }

    [Fact]
    public async Task StartAsync_CanPublishSnapshotFromVisualObservationPipeline()
    {
        var sourceFrame = new FramePacket("camera-1", 2200, 2, 2, [1, 2, 3]);
        var movingObservation = new MovingObjectObservation("camera-1", sourceFrame.TimestampUtcMs, 5, 6, 1, 2, 3, 4, 0.5f);
        var trainObservation = new TrainObservation("camera-1", sourceFrame.TimestampUtcMs, "Red", 10, 20, 8, 18, 12, 14, 0.75f);
        var debugFrame = new DebugFrame("moving-object-observation", sourceFrame);
        var output = new RecordingOutputPort();
        await using var controller = new PipelineController(
            new SingleFrameSourceFactory(new SingleFrameSource(sourceFrame)),
            new StubVisualObservationPipeline(new VisualObservationResult([movingObservation], [trainObservation], [debugFrame])),
            new StubTracker([]),
            [output],
            new StubClock(sourceFrame.TimestampUtcMs));

        await controller.StartAsync("camera-1", CancellationToken.None);
        var snapshot = await output.WaitForSnapshotAsync();
        await controller.StopAsync(CancellationToken.None);

        Assert.Same(movingObservation, Assert.Single(snapshot.MovingObjectObservations));
        Assert.Same(trainObservation, Assert.Single(snapshot.TrainObservations));
        Assert.Same(debugFrame, Assert.Single(snapshot.DebugFrames));
    }

    [Fact]
    public async Task StartAsync_DoesNotPublishFullFrameColorDetectionsAsTrainObservations()
    {
        var sourceFrame = new FramePacket("camera-1", 2300, 2, 2, [1, 2, 3]);
        var staticColorBlob = new Detection("red-blob", 10, 20, 8, 18, 12, 14, 0.75f, "Red", "camera-1", sourceFrame.TimestampUtcMs);
        var output = new RecordingOutputPort();
        await using var controller = new PipelineController(
            new SingleFrameSourceFactory(new SingleFrameSource(sourceFrame)),
            new StubDetectorManager([staticColorBlob]),
            new StubTracker([]),
            [output],
            new StubClock(sourceFrame.TimestampUtcMs));

        await controller.StartAsync("camera-1", CancellationToken.None);
        var snapshot = await output.WaitForSnapshotAsync();
        await controller.StopAsync(CancellationToken.None);

        Assert.Empty(snapshot.TrainObservations);
    }

    [Fact]
    public async Task StartAsync_WhenDebugViewEnabled_PublishesNamedDebugFrames()
    {
        var sourceFrame = new FramePacket("camera-1", 1234, 2, 2, [1, 2, 3]);
        var trainObservation = new TrainObservation("camera-1", sourceFrame.TimestampUtcMs, "Red", 10, 20, 8, 18, 12, 14, 0.75f);
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
            new StubVisualObservationPipeline(new VisualObservationResult([], [trainObservation], [])),
            new StubTracker([trainState]),
            [output],
            new StubClock(sourceFrame.TimestampUtcMs));

        controller.SetDebugViewEnabled("camera-1", enabled: true);

        await controller.StartAsync("camera-1", CancellationToken.None);
        var snapshot = await output.WaitForSnapshotAsync();
        await controller.StopAsync(CancellationToken.None);

        Assert.Contains(snapshot.DebugFrames, frame => frame.Name == "source");
        Assert.Contains(snapshot.DebugFrames, frame => frame.Name == "train-observation");
        Assert.Contains(snapshot.DebugFrames, frame => frame.Name == "train-tracking");
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

    [Fact]
    public async Task StartAsync_WhenTrainObservationHasNoTrainState_DoesNotDrawObservationOnAnnotatedFrame()
    {
        var sourceFrame = JpegFrame("camera-1", 1234);
        var observation = new TrainObservation("camera-1", sourceFrame.TimestampUtcMs, "Red", 10, 10, 4, 4, 8, 8, 0.75f);
        var output = new RecordingOutputPort();
        await using var controller = new PipelineController(
            new SingleFrameSourceFactory(new SingleFrameSource(sourceFrame)),
            new StubVisualObservationPipeline(new VisualObservationResult([], [observation], [])),
            new StubTracker([]),
            [output],
            new StubClock(sourceFrame.TimestampUtcMs));

        await controller.StartAsync("camera-1", CancellationToken.None);
        var snapshot = await output.WaitForSnapshotAsync();
        await controller.StopAsync(CancellationToken.None);

        Assert.Single(snapshot.TrainObservations);
        Assert.Empty(snapshot.TrainStates);
        Assert.Equal(sourceFrame.EncodedJpeg, snapshot.AnnotatedFrame.EncodedJpeg);
    }

    [Fact]
    public async Task StartAsync_WhenTrainStateExists_DrawsTrainStateOnAnnotatedFrame()
    {
        var sourceFrame = JpegFrame("camera-1", 1234);
        var trainState = new TrainState(
            "local-train-1",
            "Red",
            10,
            10,
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
            new StubDetectorManager([]),
            new StubTracker([trainState]),
            [output],
            new StubClock(sourceFrame.TimestampUtcMs));

        await controller.StartAsync("camera-1", CancellationToken.None);
        var snapshot = await output.WaitForSnapshotAsync();
        await controller.StopAsync(CancellationToken.None);

        Assert.Same(trainState, Assert.Single(snapshot.TrainStates));
        Assert.NotEqual(sourceFrame.EncodedJpeg, snapshot.AnnotatedFrame.EncodedJpeg);
    }

    [Fact]
    public async Task StopAsync_WhenLaneIsWaitingForFreshSourceFrame_DoesNotThrowFirstChanceTaskCanceledException()
    {
        var sourceFrame = new FramePacket("camera-1", 1234, 2, 2, [1, 2, 3]);
        var output = new RecordingOutputPort();
        var taskCanceledExceptions = 0;
        await using var controller = new PipelineController(
            new SingleFrameSourceFactory(new SingleFrameSource(sourceFrame)),
            new StubDetectorManager([]),
            new StubTracker([]),
            [output],
            new StubClock(sourceFrame.TimestampUtcMs));

        await controller.StartAsync("camera-1", CancellationToken.None);
        await output.WaitForSnapshotAsync();

        void OnFirstChanceException(object? sender, FirstChanceExceptionEventArgs args)
        {
            if (args.Exception is TaskCanceledException && args.Exception.StackTrace?.Contains("PipelineController", StringComparison.Ordinal) == true)
            {
                taskCanceledExceptions++;
            }
        }

        AppDomain.CurrentDomain.FirstChanceException += OnFirstChanceException;
        try
        {
            await Task.Delay(25);
            await controller.StopAsync(CancellationToken.None);
        }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= OnFirstChanceException;
        }

        Assert.Equal(0, taskCanceledExceptions);
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
        public IReadOnlyList<string> AvailableColorFilters => [];

        public IReadOnlyList<string> EnabledColorFilters => [];

        public IReadOnlyList<ColorCalibrationProfile> ColorCalibrations => [];

        public void SetEnabledColorFilters(IEnumerable<string> colors) { }

        public void SetColorCalibrations(IEnumerable<ColorCalibrationProfile> calibrations) { }

        public Task<IReadOnlyList<Detection>> DetectAsync(FramePacket frame, CancellationToken cancellationToken) => Task.FromResult(detections);
    }

    private sealed class StubVisualObservationPipeline(VisualObservationResult result) : IVisualObservationPipeline
    {
        public Task<VisualObservationResult> ObserveAsync(
            FramePacket sourceFrame,
            VisualObservationSettings settings,
            CancellationToken cancellationToken) => Task.FromResult(result);
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

    private static FramePacket JpegFrame(string sourceId, long timestampUtcMs)
    {
        using var image = new Cv.Mat(24, 24, Cv.MatType.CV_8UC3, Cv.Scalar.Black);
        Cv.Cv2.ImEncode(".jpg", image, out var encoded, [new Cv.ImageEncodingParam(Cv.ImwriteFlags.JpegQuality, 90)]);
        return new FramePacket(sourceId, timestampUtcMs, image.Width, image.Height, encoded);
    }
}
