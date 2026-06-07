using ObjectTracker.Core.Domain;
using ObjectTracker.Core.Ports;
using Xunit;

namespace ObjectTracker.Vision.Tests;

public sealed class PipelineControllerLaneReconciliationTests
{
    [Fact]
    public async Task StartAsync_StartsLanesForAllIncludedCameraSources()
    {
        var factory = new MultiFrameSourceFactory(
            new ControlledFrameSource("camera-1", [Frame("camera-1", 1000)]),
            new ControlledFrameSource("camera-2", [Frame("camera-2", 1000)]));
        var output = new RecordingOutputPort();
        await using var controller = CreateController(factory, output);

        controller.SetVisionPipelineInclusion("camera-1", included: true);
        controller.SetVisionPipelineInclusion("camera-2", included: true);

        await controller.StartAsync(CancellationToken.None);
        var snapshots = await output.WaitForSnapshotsAsync(2);
        await controller.StopAsync(CancellationToken.None);

        Assert.Contains(snapshots, snapshot => snapshot.CameraSourceId == "camera-1");
        Assert.Contains(snapshots, snapshot => snapshot.CameraSourceId == "camera-2");
        Assert.Equal(1, factory.Source("camera-1").StartCount);
        Assert.Equal(1, factory.Source("camera-2").StartCount);
    }

    [Fact]
    public async Task StopAsync_StopsAllActiveLanes()
    {
        var factory = new MultiFrameSourceFactory(
            new ControlledFrameSource("camera-1", [Frame("camera-1", 1000)]),
            new ControlledFrameSource("camera-2", [Frame("camera-2", 1000)]));
        var output = new RecordingOutputPort();
        await using var controller = CreateController(factory, output);

        controller.SetVisionPipelineInclusion("camera-1", included: true);
        controller.SetVisionPipelineInclusion("camera-2", included: true);
        await controller.StartAsync(CancellationToken.None);
        await output.WaitForSnapshotsAsync(2);

        await controller.StopAsync(CancellationToken.None);

        Assert.Equal(1, factory.Source("camera-1").StopCount);
        Assert.Equal(1, factory.Source("camera-2").StopCount);
    }

    [Fact]
    public async Task SetVisionPipelineInclusion_WhenRunningStartsOnlyNewlyIncludedLane()
    {
        var factory = new MultiFrameSourceFactory(
            new ControlledFrameSource("camera-1", [Frame("camera-1", 1000)]),
            new ControlledFrameSource("camera-2", [Frame("camera-2", 1000)]));
        var output = new RecordingOutputPort();
        await using var controller = CreateController(factory, output);

        controller.SetVisionPipelineInclusion("camera-1", included: true);
        await controller.StartAsync(CancellationToken.None);
        await output.WaitForSnapshotsAsync(1);

        controller.SetVisionPipelineInclusion("camera-2", included: true);
        var snapshots = await output.WaitForSnapshotsAsync(2);
        await controller.StopAsync(CancellationToken.None);

        Assert.Contains(snapshots, snapshot => snapshot.CameraSourceId == "camera-2");
        Assert.Equal(1, factory.Source("camera-1").StartCount);
        Assert.Equal(1, factory.Source("camera-2").StartCount);
    }

    [Fact]
    public async Task SetVisionPipelineInclusion_WhenRunningStopsOnlyExcludedLane()
    {
        var factory = new MultiFrameSourceFactory(
            new ControlledFrameSource("camera-1", [Frame("camera-1", 1000)]),
            new ControlledFrameSource("camera-2", [Frame("camera-2", 1000)]));
        var output = new RecordingOutputPort();
        await using var controller = CreateController(factory, output);

        controller.SetVisionPipelineInclusion("camera-1", included: true);
        controller.SetVisionPipelineInclusion("camera-2", included: true);
        await controller.StartAsync(CancellationToken.None);
        await output.WaitForSnapshotsAsync(2);

        controller.SetVisionPipelineInclusion("camera-2", included: false);
        await WaitUntilAsync(() => factory.Source("camera-2").StopCount == 1);

        Assert.Equal(0, factory.Source("camera-1").StopCount);
        Assert.Equal(1, factory.Source("camera-2").StopCount);

        await controller.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task SetDebugViewEnabled_WhenRunningAffectsNextSnapshotFromLane()
    {
        var source = new ControlledFrameSource("camera-1", [Frame("camera-1", 1000)]);
        var factory = new MultiFrameSourceFactory(source);
        var output = new RecordingOutputPort();
        await using var controller = CreateController(factory, output);

        controller.SetVisionPipelineInclusion("camera-1", included: true);
        await controller.StartAsync(CancellationToken.None);
        var firstSnapshot = await output.WaitForSnapshotAsync();
        Assert.Empty(firstSnapshot.DebugFrames);

        controller.SetDebugViewEnabled("camera-1", enabled: true);
        source.Enqueue(Frame("camera-1", 2000));
        var snapshots = await output.WaitForSnapshotsAsync(2);
        await controller.StopAsync(CancellationToken.None);

        var secondSnapshot = snapshots.Last(snapshot => snapshot.CameraSourceId == "camera-1");
        Assert.Single(secondSnapshot.DebugFrames);
        Assert.Equal(1, source.StartCount);
    }

    [Fact]
    public async Task FailedLane_DoesNotStopOtherLane()
    {
        var healthySource = new ControlledFrameSource("camera-healthy", [Frame("camera-healthy", 1000)]);
        var failingSource = new ControlledFrameSource("camera-failing", [], throwsOnRead: true);
        var factory = new MultiFrameSourceFactory(healthySource, failingSource);
        var output = new RecordingOutputPort();
        await using var controller = CreateController(factory, output);

        controller.SetVisionPipelineInclusion("camera-healthy", included: true);
        controller.SetVisionPipelineInclusion("camera-failing", included: true);
        await controller.StartAsync(CancellationToken.None);
        await output.WaitForSnapshotsAsync(1);

        healthySource.Enqueue(Frame("camera-healthy", 2000));
        var snapshots = await output.WaitForSnapshotsAsync(2);
        await controller.StopAsync(CancellationToken.None);

        Assert.Equal(2, snapshots.Count(snapshot => snapshot.CameraSourceId == "camera-healthy"));
        Assert.Equal(0, snapshots.Count(snapshot => snapshot.CameraSourceId == "camera-failing"));
    }

    [Fact]
    public async Task LaneProcessesOneFreshSourceFramePerCadenceInsteadOfDrainingBacklog()
    {
        var source = new ControlledFrameSource(
            "camera-1",
            [
                Frame("camera-1", 1000),
                Frame("camera-1", 1001),
                Frame("camera-1", 1002)
            ]);
        var factory = new MultiFrameSourceFactory(source);
        var output = new RecordingOutputPort();
        await using var controller = CreateController(factory, output);

        controller.SetVisionPipelineInclusion("camera-1", included: true);
        await controller.StartAsync(CancellationToken.None);
        var snapshot = await output.WaitForSnapshotAsync();
        await controller.StopAsync(CancellationToken.None);

        Assert.Equal(1000, snapshot.SourceFrame.TimestampUtcMs);
    }

    [Fact]
    public async Task LaneSkipsCadenceTickWhenSourceHasNoFreshFrame()
    {
        var source = new ControlledFrameSource("camera-1", [Frame("camera-1", 1000)]);
        var factory = new MultiFrameSourceFactory(source);
        var output = new RecordingOutputPort();
        await using var controller = CreateController(factory, output);

        controller.SetVisionPipelineInclusion("camera-1", included: true);
        controller.SetTargetFramesPerSecond(60);
        await controller.StartAsync(CancellationToken.None);
        await output.WaitForSnapshotAsync();
        await Task.Delay(80);
        await controller.StopAsync(CancellationToken.None);

        Assert.Single(output.Snapshots);
    }

    [Fact]
    public async Task SnapshotReportsConfiguredTargetFpsSeparatelyFromActualProcessedFps()
    {
        var source = new ControlledFrameSource("camera-1", [Frame("camera-1", 1000)]);
        var factory = new MultiFrameSourceFactory(source);
        var output = new RecordingOutputPort();
        await using var controller = CreateController(factory, output);

        controller.SetVisionPipelineInclusion("camera-1", included: true);
        controller.SetTargetFramesPerSecond(12);
        await controller.StartAsync(CancellationToken.None);
        var snapshot = await output.WaitForSnapshotAsync();
        await controller.StopAsync(CancellationToken.None);

        Assert.Equal(12, controller.TargetFramesPerSecond);
        Assert.Equal(12, snapshot.Timing.TargetFramesPerSecond);
        Assert.True(snapshot.Timing.FramesPerSecond >= 0);
    }

    [Fact]
    public async Task SetTargetFramesPerSecond_WhenRunningUpdatesCadenceWithoutRestartOrTrainTrackingReset()
    {
        var source = new ControlledFrameSource("camera-1", [Frame("camera-1", 1000)]);
        var factory = new MultiFrameSourceFactory(source);
        var output = new RecordingOutputPort();
        var tracker = new CountingTracker();
        await using var controller = CreateController(factory, output, tracker);

        controller.SetVisionPipelineInclusion("camera-1", included: true);
        controller.SetTargetFramesPerSecond(1);
        await controller.StartAsync(CancellationToken.None);
        await output.WaitForSnapshotAsync();

        source.Enqueue(Frame("camera-1", 2000));
        await Task.Delay(150);
        Assert.Single(output.Snapshots);

        controller.SetTargetFramesPerSecond(60);
        var snapshots = await output.WaitForSnapshotsAsync(2);

        Assert.Equal(1, source.StartCount);
        Assert.Equal(0, tracker.ResetCount);
        Assert.Equal(60, snapshots.Last().Timing.TargetFramesPerSecond);

        await controller.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task SetTargetFramesPerSecond_WhenRunningAppliesToEveryLane()
    {
        var source1 = new ControlledFrameSource("camera-1", [Frame("camera-1", 1000)]);
        var source2 = new ControlledFrameSource("camera-2", [Frame("camera-2", 1000)]);
        var factory = new MultiFrameSourceFactory(source1, source2);
        var output = new RecordingOutputPort();
        await using var controller = CreateController(factory, output);

        controller.SetVisionPipelineInclusion("camera-1", included: true);
        controller.SetVisionPipelineInclusion("camera-2", included: true);
        controller.SetTargetFramesPerSecond(1);
        await controller.StartAsync(CancellationToken.None);
        await output.WaitForSnapshotsAsync(2);

        controller.SetTargetFramesPerSecond(60);
        source1.Enqueue(Frame("camera-1", 2000));
        source2.Enqueue(Frame("camera-2", 2000));
        var snapshots = await output.WaitForSnapshotsAsync(4);
        await controller.StopAsync(CancellationToken.None);

        var latestByCameraSource = snapshots
            .GroupBy(snapshot => snapshot.CameraSourceId)
            .ToDictionary(group => group.Key, group => group.Last());
        Assert.Equal(60, latestByCameraSource["camera-1"].Timing.TargetFramesPerSecond);
        Assert.Equal(60, latestByCameraSource["camera-2"].Timing.TargetFramesPerSecond);
    }

    [Fact]
    public async Task SnapshotReportsActualProcessedFpsPerLane()
    {
        var source1 = new ControlledFrameSource("camera-1", [Frame("camera-1", 2000)]);
        var source2 = new ControlledFrameSource("camera-2", [Frame("camera-2", 2000)]);
        var factory = new MultiFrameSourceFactory(source1, source2);
        var output = new RecordingOutputPort();
        await using var controller = CreateController(factory, output);

        controller.SetVisionPipelineInclusion("camera-1", included: true);
        controller.SetVisionPipelineInclusion("camera-2", included: true);
        await controller.StartAsync(CancellationToken.None);
        var snapshots = await output.WaitForSnapshotsAsync(2);
        await controller.StopAsync(CancellationToken.None);

        Assert.All(snapshots, snapshot => Assert.Equal(1, snapshot.Timing.FramesPerSecond));
    }

    [Fact]
    public async Task TrainTracking_KeepsLocalTrainIdWhenTrainMovesAcrossCameraSources()
    {
        var source1 = new ControlledFrameSource("camera-1", [Frame("camera-1", 1000)]);
        var source2 = new ControlledFrameSource("camera-2", [Frame("camera-2", 2000)]);
        var factory = new MultiFrameSourceFactory(source1, source2);
        var output = new RecordingOutputPort();
        await using var controller = new PipelineController(
            factory,
            new SourceAwareVisualObservationPipeline(new Dictionary<string, Detection[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["camera-1"] = [Detection("camera-1", 1000, 10, 10, "Red")],
                ["camera-2"] = [Detection("camera-2", 2000, 20, 12, "Red")]
            }),
            new SimpleTracker(),
            [output],
            new IncrementingClock());

        controller.SetVisionPipelineInclusion("camera-1", included: true);
        await controller.StartAsync(CancellationToken.None);
        var firstSnapshot = await output.WaitForSnapshotAsync();

        controller.SetVisionPipelineInclusion("camera-2", included: true);
        var snapshots = await output.WaitForSnapshotsAsync(2);
        await controller.StopAsync(CancellationToken.None);

        var secondSnapshot = snapshots.Last(snapshot => snapshot.CameraSourceId == "camera-2");
        var firstTrainState = Assert.Single(firstSnapshot.TrainStates);
        var secondTrainState = Assert.Single(secondSnapshot.TrainStates);
        Assert.Equal("camera-1", firstTrainState.SourceId);
        Assert.Equal("camera-2", secondTrainState.SourceId);
        Assert.Equal(firstTrainState.LocalTrainId, secondTrainState.LocalTrainId);
    }

    [Fact]
    public async Task TrainTracking_ContinuesTrainStateWhenNoNewObservationAppears()
    {
        var source = new ControlledFrameSource("camera-1", [Frame("camera-1", 1000)]);
        var factory = new MultiFrameSourceFactory(source);
        var output = new RecordingOutputPort();
        await using var controller = new PipelineController(
            factory,
            new SequenceVisualObservationPipeline([
                [Detection("camera-1", 1000, 10, 10, "Red")],
                []
            ]),
            new SimpleTracker(),
            [output],
            new IncrementingClock());

        controller.SetVisionPipelineInclusion("camera-1", included: true);
        await controller.StartAsync(CancellationToken.None);
        var firstSnapshot = await output.WaitForSnapshotAsync();

        source.Enqueue(Frame("camera-1", 2000));
        var snapshots = await output.WaitForSnapshotsAsync(2);
        await controller.StopAsync(CancellationToken.None);

        var firstTrainState = Assert.Single(firstSnapshot.TrainStates);
        var secondTrainState = Assert.Single(snapshots.Last().TrainStates);
        Assert.Equal(firstTrainState.LocalTrainId, secondTrainState.LocalTrainId);
        Assert.Equal(TrainMotionState.Uncertain, secondTrainState.MotionState);
        Assert.True(secondTrainState.Confidence < firstTrainState.Confidence);
    }

    [Fact]
    public async Task TrainTracking_WhenDuplicateLocalTrainIdExceedsHandoffGrace_RaisesAmbiguityAlertOutsidePipelineSnapshot()
    {
        var source1 = new ControlledFrameSource("camera-1", [Frame("camera-1", 1000)]);
        var source2 = new ControlledFrameSource("camera-2", [Frame("camera-2", 6000)]);
        var factory = new MultiFrameSourceFactory(source1, source2);
        var output = new RecordingOutputPort();
        await using var controller = new PipelineController(
            factory,
            new SourceAwareVisualObservationPipeline(new Dictionary<string, Detection[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["camera-1"] = [Detection("camera-1", 1000, 10, 10, "Red")],
                ["camera-2"] = [Detection("camera-2", 6000, 20, 12, "Red")]
            }),
            new DuplicateLocalTrainIdTracker(),
            [output],
            new IncrementingClock());

        controller.SetVisionPipelineInclusion("camera-1", included: true);
        await controller.StartAsync(CancellationToken.None);
        var firstSnapshot = await output.WaitForSnapshotAsync();

        controller.SetVisionPipelineInclusion("camera-2", included: true);
        var snapshots = await output.WaitForSnapshotsAsync(2);
        await WaitUntilAsync(() => output.Statuses.Any(status => status.Contains("Ambiguity Alert", StringComparison.OrdinalIgnoreCase)));
        await controller.StopAsync(CancellationToken.None);

        var secondSnapshot = snapshots.Last(snapshot => snapshot.CameraSourceId == "camera-2");
        Assert.Equal("train-001", Assert.Single(firstSnapshot.TrainStates).LocalTrainId);
        Assert.Equal("train-001", Assert.Single(secondSnapshot.TrainStates).LocalTrainId);
        Assert.Contains(output.Statuses, status => status.Contains("Ambiguity Alert", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TrainTracking_AllowsDuplicateLocalTrainIdDuringHandoffGracePeriod()
    {
        var source1 = new ControlledFrameSource("camera-1", [Frame("camera-1", 1000)]);
        var source2 = new ControlledFrameSource("camera-2", [Frame("camera-2", 2500)]);
        var factory = new MultiFrameSourceFactory(source1, source2);
        var output = new RecordingOutputPort();
        await using var controller = new PipelineController(
            factory,
            new SourceAwareVisualObservationPipeline(new Dictionary<string, Detection[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["camera-1"] = [Detection("camera-1", 1000, 10, 10, "Red")],
                ["camera-2"] = [Detection("camera-2", 2500, 20, 12, "Red")]
            }),
            new DuplicateLocalTrainIdTracker(),
            [output],
            new IncrementingClock());

        controller.SetVisionPipelineInclusion("camera-1", included: true);
        await controller.StartAsync(CancellationToken.None);
        var firstSnapshot = await output.WaitForSnapshotAsync();

        controller.SetVisionPipelineInclusion("camera-2", included: true);
        var snapshots = await output.WaitForSnapshotsAsync(2);
        await Task.Delay(50);
        await controller.StopAsync(CancellationToken.None);

        var secondSnapshot = snapshots.Last(snapshot => snapshot.CameraSourceId == "camera-2");
        Assert.Equal("train-001", Assert.Single(firstSnapshot.TrainStates).LocalTrainId);
        Assert.Equal("train-001", Assert.Single(secondSnapshot.TrainStates).LocalTrainId);
        Assert.DoesNotContain(output.Statuses, status => status.Contains("Ambiguity Alert", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TrainTracking_WhenAmbiguityAlertIsRaised_OtherCameraSourceLanesKeepRunning()
    {
        var source1 = new ControlledFrameSource("camera-1", [Frame("camera-1", 1000)]);
        var source2 = new ControlledFrameSource("camera-2", [Frame("camera-2", 6000)]);
        var healthySource = new ControlledFrameSource("camera-healthy", [Frame("camera-healthy", 1000)]);
        var factory = new MultiFrameSourceFactory(source1, source2, healthySource);
        var output = new RecordingOutputPort();
        await using var controller = new PipelineController(
            factory,
            new SourceAwareVisualObservationPipeline(new Dictionary<string, Detection[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["camera-1"] = [Detection("camera-1", 1000, 10, 10, "Red")],
                ["camera-2"] = [Detection("camera-2", 6000, 20, 12, "Red")],
                ["camera-healthy"] = []
            }),
            new DuplicateLocalTrainIdTracker(),
            [output],
            new IncrementingClock());

        controller.SetVisionPipelineInclusion("camera-1", included: true);
        controller.SetVisionPipelineInclusion("camera-healthy", included: true);
        await controller.StartAsync(CancellationToken.None);
        await output.WaitForSnapshotsAsync(2);

        controller.SetVisionPipelineInclusion("camera-2", included: true);
        await WaitUntilAsync(() => output.Statuses.Any(status => status.Contains("Ambiguity Alert", StringComparison.OrdinalIgnoreCase)));

        healthySource.Enqueue(Frame("camera-healthy", 7000));
        var snapshots = await output.WaitForSnapshotsAsync(output.Snapshots.Count + 1);
        await controller.StopAsync(CancellationToken.None);

        Assert.Contains(snapshots, snapshot => snapshot.CameraSourceId == "camera-healthy" && snapshot.SourceFrame.TimestampUtcMs == 7000);
    }

    private static PipelineController CreateController(IFrameSourceFactory factory, IOutputPort output, ITracker? tracker = null) => new(
        factory,
        new StubDetectorManager(),
        tracker ?? new StubTracker(),
        [output],
        new IncrementingClock());

    private static FramePacket Frame(string sourceId, long timestamp) => new(sourceId, timestamp, 2, 2, [1, 2, 3]);

    private static Detection Detection(string sourceId, long timestamp, float x, float y, string trainColor) => new(
        $"{sourceId}-{trainColor}-{timestamp}",
        x,
        y,
        x - 2,
        y - 2,
        4,
        4,
        0.9f,
        trainColor,
        sourceId,
        timestamp);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10);
        }

        throw new TimeoutException("Expected condition to become true.");
    }

    private sealed class MultiFrameSourceFactory(params ControlledFrameSource[] sources) : IFrameSourceFactory
    {
        private readonly Dictionary<string, ControlledFrameSource> sourcesById = sources.ToDictionary(source => source.Id, StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<FrameSourceInfo> GetAvailableSources() => sources.Select(source => new FrameSourceInfo(source.Id, source.DisplayName)).ToList();

        public IFrameSource Create(string sourceId) => Source(sourceId);

        public ControlledFrameSource Source(string sourceId) => sourcesById[sourceId];
    }

    private sealed class ControlledFrameSource(string id, IReadOnlyList<FramePacket> frames, bool throwsOnRead = false) : IFrameSource
    {
        private readonly Lock sync = new();
        private readonly List<FramePacket> frames = frames.ToList();
        private int nextFrameIndex;

        public string Id => id;

        public string DisplayName => id;

        public string Diagnostics => "test source";

        public int StartCount { get; private set; }

        public int StopCount { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            StartCount++;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            StopCount++;
            return Task.CompletedTask;
        }

        public Task<FramePacket?> ReadFrameAsync(CancellationToken cancellationToken)
        {
            if (throwsOnRead)
            {
                throw new InvalidOperationException($"{id} failed");
            }

            lock (sync)
            {
                if (nextFrameIndex >= this.frames.Count)
                {
                    return Task.FromResult<FramePacket?>(null);
                }

                return Task.FromResult<FramePacket?>(this.frames[nextFrameIndex++]);
            }
        }

        public void Enqueue(FramePacket frame)
        {
            lock (sync)
            {
                frames.Add(frame);
            }
        }

        public string? ConsumeDiagnosticEvent() => null;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class StubDetectorManager : IDetectorManager
    {
        public IReadOnlyList<string> AvailableColorFilters => [];

        public IReadOnlyList<string> EnabledColorFilters => [];

        public IReadOnlyList<ColorCalibrationProfile> ColorCalibrations => [];

        public void SetEnabledColorFilters(IEnumerable<string> colors) { }

        public void SetColorCalibrations(IEnumerable<ColorCalibrationProfile> calibrations) { }

        public Task<IReadOnlyList<Detection>> DetectAsync(FramePacket frame, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Detection>>([]);
    }

    private sealed class SourceAwareDetectorManager(IReadOnlyDictionary<string, Detection[]> detectionsBySourceId) : IDetectorManager
    {
        public IReadOnlyList<string> AvailableColorFilters => [];

        public IReadOnlyList<string> EnabledColorFilters => [];

        public IReadOnlyList<ColorCalibrationProfile> ColorCalibrations => [];

        public void SetEnabledColorFilters(IEnumerable<string> colors) { }

        public void SetColorCalibrations(IEnumerable<ColorCalibrationProfile> calibrations) { }

        public Task<IReadOnlyList<Detection>> DetectAsync(FramePacket frame, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<Detection>>(
                detectionsBySourceId.TryGetValue(frame.SourceId, out var detections) ? detections : []);
        }
    }

    private sealed class SequenceDetectorManager(IReadOnlyList<IReadOnlyList<Detection>> detectionsByCall) : IDetectorManager
    {
        private int callIndex;

        public IReadOnlyList<string> AvailableColorFilters => [];

        public IReadOnlyList<string> EnabledColorFilters => [];

        public IReadOnlyList<ColorCalibrationProfile> ColorCalibrations => [];

        public void SetEnabledColorFilters(IEnumerable<string> colors) { }

        public void SetColorCalibrations(IEnumerable<ColorCalibrationProfile> calibrations) { }

        public Task<IReadOnlyList<Detection>> DetectAsync(FramePacket frame, CancellationToken cancellationToken)
        {
            var index = Math.Min(Interlocked.Increment(ref callIndex) - 1, detectionsByCall.Count - 1);
            return Task.FromResult(detectionsByCall[index]);
        }
    }

    private sealed class SourceAwareVisualObservationPipeline(IReadOnlyDictionary<string, Detection[]> detectionsBySourceId) : IVisualObservationPipeline
    {
        public Task<VisualObservationResult> ObserveAsync(
            FramePacket sourceFrame,
            VisualObservationSettings settings,
            CancellationToken cancellationToken)
        {
            var trainObservations = detectionsBySourceId.TryGetValue(sourceFrame.SourceId, out var detections)
                ? detections.Select(ToTrainObservation).ToList()
                : [];
            return Task.FromResult(new VisualObservationResult([], trainObservations, []));
        }
    }

    private sealed class SequenceVisualObservationPipeline(IReadOnlyList<IReadOnlyList<Detection>> detectionsByCall) : IVisualObservationPipeline
    {
        private int callIndex;

        public Task<VisualObservationResult> ObserveAsync(
            FramePacket sourceFrame,
            VisualObservationSettings settings,
            CancellationToken cancellationToken)
        {
            var index = Math.Min(Interlocked.Increment(ref callIndex) - 1, detectionsByCall.Count - 1);
            return Task.FromResult(new VisualObservationResult([], detectionsByCall[index].Select(ToTrainObservation).ToList(), []));
        }
    }

    private static TrainObservation ToTrainObservation(Detection detection) => new(
        detection.SourceId,
        detection.TimestampUtcMs,
        detection.Kind,
        detection.X,
        detection.Y,
        detection.BoxX,
        detection.BoxY,
        detection.BoxWidth,
        detection.BoxHeight,
        detection.Confidence);

    private sealed class StubTracker : ITracker
    {
        public IReadOnlyList<TrainState> Update(IReadOnlyList<Detection> detections, long frameTimestampUtcMs) => [];

        public void Reset() { }
    }

    private sealed class CountingTracker : ITracker
    {
        public int ResetCount { get; private set; }

        public IReadOnlyList<TrainState> Update(IReadOnlyList<Detection> detections, long frameTimestampUtcMs) => [];

        public void Reset() => ResetCount++;
    }

    private sealed class DuplicateLocalTrainIdTracker : ITracker
    {
        public IReadOnlyList<TrainState> Update(IReadOnlyList<Detection> detections, long frameTimestampUtcMs)
        {
            return detections
                .Select(detection => new TrainState(
                    "train-001",
                    detection.Kind,
                    detection.X,
                    detection.Y,
                    0,
                    0,
                    detection.Confidence,
                    TrainMotionState.Moving,
                    CollisionWarningState.None,
                    detection.SourceId,
                    detection.TimestampUtcMs))
                .ToList();
        }

        public void Reset() { }
    }

    private sealed class IncrementingClock : IClock
    {
        private long value = 1000;

        public long UtcNowMs() => Interlocked.Increment(ref value);
    }
}
