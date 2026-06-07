using ObjectTracker.Core.Domain;
using ObjectTracker.Core.Domain.Enums;
using ObjectTracker.Core.Ports;
using Xunit;

namespace ObjectTracker.Vision.Tests;

public sealed class PipelineControllerLaneReconciliationTests
{
    /// <summary>
    /// <description>Feature: PipelineController starts processing lanes for all included camera sources.
    /// 
    ///   Scenario: StartAsync with two cameras both set as included produces snapshots from each lane.
    ///     Given a PipelineController backed by MultiFrameSourceFactory with two controlled frame sources ("camera-1" and "camera-2"),
    ///      And the output port is configured to record snapshots,
    ///      And camera-1 is set as included in the vision pipeline,
    ///      And camera-2 is set as included in the vision pipeline,
    ///     When StartAsync is called with CancellationToken.None,
    ///     Then 2 snapshots should be published (one per camera),
    ///      And at least one snapshot should have CameraSourceId "camera-1",
    ///      And at least one snapshot should have CameraSourceId "camera-2",
    ///      And factory.Source("camera-1").StartCount should be 1,
    ///      And factory.Source("camera-2").StartCount should be 1.</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: PipelineController stops all active lanes when StopAsync is called.
    /// 
    ///   Scenario: All camera source lanes are stopped and their stop counts reflect the shutdown.
    ///     Given a PipelineController backed by MultiFrameSourceFactory with two controlled frame sources,
    ///      And both cameras are set as included in the vision pipeline,
    ///      And StartAsync has been called and 2 snapshots have been published,
    ///     When StopAsync is called with CancellationToken.None,
    ///     Then factory.Source("camera-1").StopCount should be 1,
    ///      And factory.Source("camera-2").StopCount should be 1.</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: PipelineController dynamically adds lanes while running via SetVisionPipelineInclusion.
    /// 
    ///   Scenario: A newly included lane starts producing snapshots without restarting already-running lanes.
    ///     Given a PipelineController with two controlled frame sources ("camera-1" and "camera-2"),
    ///      And camera-1 is set as included,
    ///      And StartAsync has been called and 1 snapshot from camera-1 has been published,
    ///     When SetVisionPipelineInclusion("camera-2", true) is called while the controller is running,
    ///      And 2 snapshots are collected in total,
    ///     Then at least one snapshot should have CameraSourceId "camera-2",
    ///      And factory.Source("camera-1").StartCount should be 1 (no restart),
    ///      And factory.Source("camera-2").StartCount should be 1.</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: PipelineController dynamically removes lanes while running via SetVisionPipelineInclusion.
    /// 
    ///   Scenario: An excluded lane is stopped without affecting other active lanes.
    ///     Given a PipelineController with two controlled frame sources, both set as included and running,
    ///      And 2 snapshots have been published (one from each camera),
    ///     When SetVisionPipelineInclusion("camera-2", false) is called while the controller is running,
    ///      And we wait until factory.Source("camera-2").StopCount reaches 1,
    ///     Then factory.Source("camera-1").StopCount should be 0 (unaffected),
    ///      And factory.Source("camera-2").StopCount should be 1.</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: PipelineController toggles debug view per lane while running.
    /// 
    ///   Scenario: Enabling debug view on a camera source affects the next snapshot from that lane.
    ///     Given a PipelineController with one controlled frame source, set as included and running,
    ///      And the first snapshot has empty DebugFrames (debug view disabled),
    ///     When SetDebugViewEnabled("camera-1", true) is called while the controller is running,
    ///      And a new frame is enqueued to produce another snapshot,
    ///     Then the second snapshot should contain exactly one debug frame,
    ///      And the source StartCount should be 1 (no lane restart).</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: PipelineController isolates lane failures so one failing source does not stop other lanes.
    /// 
    ///   Scenario: A healthy lane continues producing snapshots while a failing lane produces none.
    ///     Given a PipelineController with two controlled frame sources — "camera-healthy" (produces frames) and "camera-failing" (throws on read),
    ///      And both cameras are set as included in the vision pipeline,
    ///      And StartAsync has been called and 1 snapshot from camera-healthy has been published,
    ///     When a new frame is enqueued for camera-healthy,
    ///      And 2 snapshots are collected in total,
    ///     Then exactly 2 snapshots should have CameraSourceId "camera-healthy",
    ///      And exactly 0 snapshots should have CameraSourceId "camera-failing".</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: PipelineController processes one fresh source frame per cadence tick instead of draining a backlog.
    /// 
    ///   Scenario: A lane with three pre-enqueued frames produces only the first snapshot and respects cadence boundaries.
    ///     Given a PipelineController with one controlled frame source that has 3 pre-enqueued frames (timestamps 1000, 1001, 1002),
    ///      And the camera is set as included in the vision pipeline,
    ///      And StartAsync has been called,
    ///     When one snapshot is collected and StopAsync is called,
    ///     Then the snapshot's SourceFrame.TimestampUtcMs should be 1000 (only the first frame was processed).</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: PipelineController skips cadence ticks when the source has no fresh frame available.
    /// 
    ///   Scenario: A lane with a single frame produces only one snapshot even after waiting past the next cadence tick.
    ///     Given a PipelineController with one controlled frame source that has 1 pre-enqueued frame,
    ///      And SetTargetFramesPerSecond(60) is called to set an aggressive cadence,
    ///      And camera-1 is set as included and StartAsync is called,
    ///      And one snapshot is collected,
    ///     When we wait 80ms (more than the ~60 FPS cadence interval of ~17ms),
    ///      And StopAsync is called,
    ///     Then exactly 1 snapshot should be in the output.</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: PipelineSnapshot reports configured target FPS separately from actual processed FPS.
    /// 
    ///   Scenario: The snapshot timing contains both the intended cadence and measured throughput.
    ///     Given a PipelineController with one controlled frame source, set as included,
    ///      And SetTargetFramesPerSecond(12) is called to configure an expected cadence,
    ///      And StartAsync has been called,
    ///     When one snapshot is collected and StopAsync is called,
    ///     Then controller.TargetFramesPerSecond should be 12,
    ///      And snapshot.Timing.TargetFramesPerSecond should be 12,
    ///      And snapshot.Timing.FramesPerSecond should be greater than or equal to 0.</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: PipelineController updates cadence at runtime without restarting lanes or resetting train tracking.
    /// 
    ///   Scenario: Changing target FPS while running applies to the next cadence tick and does not interrupt processing.
    ///     Given a PipelineController with one controlled frame source set as included,
    ///      And SetTargetFramesPerSecond(1) is called before StartAsync,
    ///      And StartAsync has been called and 1 snapshot collected,
    ///      And a new frame is enqueued to produce another snapshot,
    ///     When SetTargetFramesPerSecond(60) is called while the controller is running,
    ///      And 2 snapshots are collected in total after waiting 150ms,
    ///     Then factory.Source("camera-1").StartCount should be 1 (no restart),
    ///      And tracker.ResetCount should be 0 (no train tracking reset),
    ///      And the last snapshot's Timing.TargetFramesPerSecond should be 60.</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: PipelineController applies target FPS changes to every lane when running.
    /// 
    ///   Scenario: Changing the cadence while running affects all active lanes uniformly.
    ///     Given a PipelineController with two controlled frame sources, both set as included and running at 1 FPS,
    ///      And 2 snapshots have been collected (one from each camera),
    ///     When SetTargetFramesPerSecond(60) is called while the controller is running,
    ///      And new frames are enqueued to produce additional snapshots,
    ///      And 4 snapshots are collected in total,
    ///     Then both cameras' latest snapshots should have Timing.TargetFramesPerSecond equal to 60.</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: PipelineController passes per-camera visual observation settings to each lane at startup.
    /// 
    ///   Scenario: Each camera source receives its own calibrated detection parameters when lanes are started.
    ///     Given a PipelineController with two controlled frame sources, both set as included,
    ///      And distinct VisualObservationSettings configured for each camera (different Threshold, MotionArea, ColorMinPixels, MorphKernelSize, ProcessMaxWidth, and ColorCalibrations),
    ///      And a RecordingVisualObservationPipeline is used to capture the settings passed to ObserveAsync,
    ///     When StartAsync is called and 2 snapshots are collected,
    ///     Then observationPipeline.SettingsBySourceId["camera-1"] should equal camera1Settings (Threshold=31, MotionArea=41, ColorMinPixels=51, MorphKernelSize=5, ProcessMaxWidth=320, Red calibration),
    ///      And observationPipeline.SettingsBySourceId["camera-2"] should equal camera2Settings (Threshold=67, MotionArea=77, ColorMinPixels=87, MorphKernelSize=7, ProcessMaxWidth=640, Blue calibration).</description>
    /// </summary>
    [Fact]
    public async Task StartAsync_PassesPerCameraObservationSettingsToEachLane()
    {
        var source1 = new ControlledFrameSource("camera-1", [Frame("camera-1", 1000)]);
        var source2 = new ControlledFrameSource("camera-2", [Frame("camera-2", 1000)]);
        var factory = new MultiFrameSourceFactory(source1, source2);
        var output = new RecordingOutputPort();
        var observationPipeline = new RecordingVisualObservationPipeline();
        var camera1Settings = VisualObservationSettings.Default with
        {
            Threshold = 31,
            MotionArea = 41,
            ColorMinPixels = 51,
            MorphKernelSize = 5,
            ProcessMaxWidth = 320,
            ColorCalibrations = [new ColorCalibrationProfile("Red", 0, 10, 100, 255, 100, 255)]
        };
        var camera2Settings = VisualObservationSettings.Default with
        {
            Threshold = 67,
            MotionArea = 77,
            ColorMinPixels = 87,
            MorphKernelSize = 7,
            ProcessMaxWidth = 640,
            ColorCalibrations = [new ColorCalibrationProfile("Blue", 100, 120, 100, 255, 100, 255)]
        };
        await using var controller = new PipelineController(
            factory,
            observationPipeline,
            new StubTracker(),
            [output],
            new IncrementingClock());

        controller.SetVisionPipelineInclusion("camera-1", included: true);
        controller.SetVisionPipelineInclusion("camera-2", included: true);
        controller.SetVisualObservationSettings("camera-1", camera1Settings);
        controller.SetVisualObservationSettings("camera-2", camera2Settings);

        await controller.StartAsync(CancellationToken.None);
        await output.WaitForSnapshotsAsync(2);
        await controller.StopAsync(CancellationToken.None);

        Assert.Equal(camera1Settings, observationPipeline.SettingsBySourceId["camera-1"]);
        Assert.Equal(camera2Settings, observationPipeline.SettingsBySourceId["camera-2"]);
    }

    /// <summary>
    /// <description>Feature: PipelineSnapshot reports actual processed FPS per lane.
    /// 
    ///   Scenario: Each snapshot's timing reflects the real throughput of its lane.
    ///     Given a PipelineController with two controlled frame sources, both set as included and running,
    ///      And 2 snapshots are collected (one from each camera),
    ///     When StopAsync is called,
    ///     Then every snapshot should have Timing.FramesPerSecond equal to 1.</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: PipelineController train tracking keeps Local Train ID stable as a train moves across camera sources.
    /// 
    ///   Scenario: A red train detected on camera-1 then camera-2 retains the same LocalTrainId.
    ///     Given a PipelineController with two controlled frame sources ("camera-1" and "camera-2"),
    ///      And SourceAwareVisualObservationPipeline that returns a Red detection for each source,
    ///      And a SimpleTracker is used to maintain train identity across frames,
    ///      And camera-1 is included in the vision pipeline,
    ///     When StartAsync is called and 1 snapshot from camera-1 is collected,
    ///      Then the first snapshot should contain exactly one TrainState with SourceId "camera-1",
    ///     When SetVisionPipelineInclusion("camera-2", true) is called while running,
    ///      And 2 snapshots are collected in total,
    ///     Then the second snapshot (from camera-2) should have the same LocalTrainId as the first snapshot's TrainState.</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: PipelineController train tracking continues TrainState when no new observation appears.
    /// 
    ///   Scenario: A train that disappears from a camera's view keeps its LocalTrainId but enters uncertain motion state with reduced confidence.
    ///     Given a PipelineController with one controlled frame source,
    ///      And SequenceVisualObservationPipeline that returns [Red detection] on first call and [] (empty) on second call,
    ///      And a SimpleTracker is used to maintain train identity across frames,
    ///      And camera-1 is included in the vision pipeline,
    ///     When StartAsync is called and 1 snapshot from camera-1 is collected,
    ///      Then the first snapshot should contain exactly one TrainState with "Red" color,
    ///     When a new frame is enqueued to produce another snapshot (with no detection),
    ///      And 2 snapshots are collected in total,
    ///     Then the second snapshot's TrainState should have the same LocalTrainId as the first,
    ///      And the second TrainState.MotionState should be Uncertain,
    ///      And the second TrainState.Confidence should be less than the first.</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: PipelineController train tracking raises Ambiguity Alert when duplicate Local Train ID exceeds handoff grace period.
    /// 
    ///   Scenario: A red train appearing on camera-2 with the same LocalTrainId as one already tracked from camera-1 triggers an alert.
    ///     Given a PipelineController with two controlled frame sources ("camera-1" and "camera-2"),
    ///      And SourceAwareVisualObservationPipeline that returns a Red detection for each source,
    ///      And DuplicateLocalTrainIdTracker is used to detect identity conflicts (both detections return LocalTrainId "train-001"),
    ///      And camera-1 is included in the vision pipeline and StartAsync has been called with 1 snapshot collected,
    ///     When SetVisionPipelineInclusion("camera-2", true) is called while running,
    ///      And 2 snapshots are collected in total,
    ///      And we wait until output.Statuses contains "Ambiguity Alert",
    ///     Then both the first and second snapshots should have TrainState with LocalTrainId "train-001" (identity preserved),
    ///      And at least one status message should contain "Ambiguity Alert".</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: PipelineController train tracking allows duplicate Local Train ID during handoff grace period.
    /// 
    ///   Scenario: A red train appearing on camera-2 with the same LocalTrainId as one already tracked from camera-1 does not trigger an alert within the grace window.
    ///     Given a PipelineController with two controlled frame sources ("camera-1" and "camera-2"),
    ///      And SourceAwareVisualObservationPipeline that returns a Red detection for each source,
    ///      And DuplicateLocalTrainIdTracker is used to detect identity conflicts (both detections return LocalTrainId "train-001"),
    ///      And camera-1 is included in the vision pipeline and StartAsync has been called with 1 snapshot collected,
    ///     When SetVisionPipelineInclusion("camera-2", true) is called while running,
    ///      And 2 snapshots are collected in total,
    ///      And we wait 50ms (within handoff grace period),
    ///     Then both the first and second snapshots should have TrainState with LocalTrainId "train-001" (identity preserved),
    ///      And no status message should contain "Ambiguity Alert".</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: PipelineController train tracking raises Ambiguity Alert but other camera source lanes keep running.
    /// 
    ///   Scenario: When an ambiguity alert is raised for one lane, healthy lanes continue producing snapshots unaffected.
    ///     Given a PipelineController with three controlled frame sources ("camera-1", "camera-2", and "camera-healthy"),
    ///      And SourceAwareVisualObservationPipeline that returns Red detections for camera-1 and camera-2 (triggering duplicate LocalTrainId),
    ///      And DuplicateLocalTrainIdTracker is used to detect identity conflicts,
    ///      And camera-1 and camera-healthy are included in the vision pipeline with StartAsync called and 2 snapshots collected,
    ///     When SetVisionPipelineInclusion("camera-2", true) is called while running,
    ///      And we wait until output.Statuses contains "Ambiguity Alert",
    ///      And a new frame is enqueued for camera-healthy with timestamp 7000,
    ///      And additional snapshots are collected,
    ///     Then at least one snapshot should have CameraSourceId "camera-healthy" with SourceFrame.TimestampUtcMs equal to 7000 (healthy lane continues).</description>
    /// </summary>
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

    private sealed class RecordingVisualObservationPipeline : IVisualObservationPipeline
    {
        private readonly Lock sync = new();
        private readonly Dictionary<string, VisualObservationSettings> settingsBySourceId = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, VisualObservationSettings> SettingsBySourceId
        {
            get
            {
                lock (sync)
                {
                    return new Dictionary<string, VisualObservationSettings>(settingsBySourceId, StringComparer.OrdinalIgnoreCase);
                }
            }
        }

        public Task<VisualObservationResult> ObserveAsync(
            FramePacket sourceFrame,
            VisualObservationSettings settings,
            CancellationToken cancellationToken)
        {
            lock (sync)
            {
                settingsBySourceId[sourceFrame.SourceId] = settings;
            }

            return Task.FromResult(VisualObservationResult.Empty);
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
