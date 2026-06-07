using ObjectTracker.Core.Domain;
using ObjectTracker.Core.Domain.Enums;
using ObjectTracker.Core.Ports;
using System.Runtime.ExceptionServices;
using Xunit;
using Cv = OpenCvSharp;

namespace ObjectTracker.Vision.Tests;

public sealed class PipelineControllerSnapshotTests
{
    /// <summary>
    /// <description>Feature: PipelineController publishes per-camera-source snapshots with source and annotated frames.
    /// 
    ///   Scenario: StartAsync produces a snapshot containing the original SourceFrame and an AnnotatedFrame with matching metadata.
    ///     Given a PipelineController with one single frame source ("camera-1", timestamp 1234),
    ///      And StubDetectorManager returning no detections,
    ///      And StubTracker returning no train states,
    ///      And StubClock returning the same timestamp as the source frame,
    ///     When StartAsync("camera-1") is called with CancellationToken.None,
    ///     Then the snapshot CameraSourceId should be "camera-1",
    ///      And the snapshot SourceFrame should be the same reference as the input frame packet,
    ///      And the AnnotatedFrame.SourceId should be "camera-1",
    ///      And the AnnotatedFrame.TimestampUtcMs should equal sourceFrame.TimestampUtcMs (1234).</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: PipelineController publishes snapshot with train observations, train states, and timing information.
    /// 
    ///   Scenario: A full observation pipeline produces all fields of a pipeline snapshot.
    ///     Given a PipelineController with one single frame source ("camera-1", timestamp 2000),
    ///      And StubVisualObservationPipeline returning one TrainObservation (Red color, SourceId "camera-1"),
    ///      And StubTracker returning one TrainState (LocalTrainId "local-train-1", Red, Moving, confidence 0.8),
    ///     When StartAsync("camera-1") is called with CancellationToken.None,
    ///     Then the snapshot should contain exactly one TrainObservation with TrainColor "Red" and SourceId "camera-1",
    ///      And MovingObjectObservations should be empty,
    ///      And the snapshot should contain exactly one TrainState (same reference as returned by StubTracker),
    ///      And Timing.TimestampUtcMs should equal sourceFrame.TimestampUtcMs (2000),
    ///      And Timing.FramesPerSecond should be greater than or equal to 0,
    ///      And Timing.ProcessingMs should be greater than or equal to 0.</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: PipelineController separates moving object observations from train observations.
    /// 
    ///   Scenario: A visual observation pipeline returning both types produces distinct lists in the snapshot.
    ///     Given a PipelineController with one single frame source ("camera-1", timestamp 2100),
    ///      And StubVisualObservationPipeline returning one MovingObjectObservation (SourceId "camera-1") and one TrainObservation (Red color, SourceId "camera-1"),
    ///     When StartAsync("camera-1") is called with CancellationToken.None,
    ///     Then the snapshot should contain exactly one MovingObjectObservation with SourceId "camera-1" and X equal to 5,
    ///      And the snapshot should contain exactly one TrainObservation with TrainColor "Red".</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: PipelineController does not emit pipeline-provided debug frames when debug view is disabled.
    /// 
    ///   Scenario: A visual observation pipeline returns a debug frame but the snapshot contains none because SetDebugViewEnabled was never called.
    ///     Given a PipelineController with one single frame source ("camera-1", timestamp 2200),
    ///      And StubVisualObservationPipeline returning one MovingObjectObservation, one TrainObservation, and one DebugFrame named "moving-object-evidence",
    ///     When StartAsync("camera-1") is called with CancellationToken.None (debug view NOT enabled),
    ///     Then the snapshot should contain exactly 0 DebugFrames.</description>
    /// </summary>
    [Fact]
    public async Task StartAsync_WhenDebugViewDisabled_DoesNotEmitPipelineProvidedDebugFrames()
    {
        var sourceFrame = new FramePacket("camera-1", 2200, 2, 2, [1, 2, 3]);
        var movingObservation = new MovingObjectObservation("camera-1", sourceFrame.TimestampUtcMs, 5, 6, 1, 2, 3, 4, 0.5f);
        var trainObservation = new TrainObservation("camera-1", sourceFrame.TimestampUtcMs, "Red", 10, 20, 8, 18, 12, 14, 0.75f);
        var pipelineDebugFrame = new DebugFrame("moving-object-evidence", sourceFrame);
        var output = new RecordingOutputPort();
        await using var controller = new PipelineController(
            new SingleFrameSourceFactory(new SingleFrameSource(sourceFrame)),
            new StubVisualObservationPipeline(new VisualObservationResult([movingObservation], [trainObservation], [pipelineDebugFrame])),
            new StubTracker([]),
            [output],
            new StubClock(sourceFrame.TimestampUtcMs));

        await controller.StartAsync("camera-1", CancellationToken.None);
        var snapshot = await output.WaitForSnapshotAsync();
        await controller.StopAsync(CancellationToken.None);

        Assert.Empty(snapshot.DebugFrames);
    }

    /// <summary>
    /// <description>Feature: PipelineController publishes snapshot from visual observation pipeline when debug view is enabled.
    /// 
    ///   Scenario: Enabling debug view causes the snapshot to include moving object observations, train observations, and named debug frames.
    ///     Given a PipelineController with one single frame source ("camera-1", timestamp 2200),
    ///      And StubVisualObservationPipeline returning one MovingObjectObservation, one TrainObservation, and one DebugFrame named "moving-object-observation",
    ///     When SetDebugViewEnabled("camera-1", true) is called before StartAsync,
    ///      And StartAsync("camera-1") is called with CancellationToken.None,
    ///     Then the snapshot should contain exactly one MovingObjectObservation (same reference as pipeline output),
    ///      And the snapshot should contain exactly one TrainObservation (same reference as pipeline output),
    ///      And the snapshot should contain a DebugFrame named "moving-object-observation" with Frame matching the debug frame from the pipeline.</description>
    /// </summary>
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

        controller.SetDebugViewEnabled("camera-1", enabled: true);

        await controller.StartAsync("camera-1", CancellationToken.None);
        var snapshot = await output.WaitForSnapshotAsync();
        await controller.StopAsync(CancellationToken.None);

        Assert.Same(movingObservation, Assert.Single(snapshot.MovingObjectObservations));
        Assert.Same(trainObservation, Assert.Single(snapshot.TrainObservations));
        Assert.Same(debugFrame, snapshot.DebugFrames.First(f => f.Name == "moving-object-observation" && ReferenceEquals(f.Frame, debugFrame.Frame)));
    }

    /// <summary>
    /// <description>Feature: PipelineController does not publish full-frame color detections as train observations.
    /// 
    ///   Scenario: A static color blob detected by StubDetectorManager is excluded from TrainObservations to prevent false positives on full-frame detections.
    ///     Given a PipelineController with one single frame source ("camera-1", timestamp 2300),
    ///      And StubDetectorManager returning one Detection (Red, kind "static-color-blob"),
    ///     When StartAsync("camera-1") is called with CancellationToken.None,
    ///     Then the snapshot should contain exactly 0 TrainObservations.</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: PipelineController with VisualObservationPipeline does not produce train observations from static color blobs.
    /// 
    ///   Scenario: A red rectangle in a background-subtracted frame is ignored because it appears only in the static background, not as moving evidence.
    ///     Given a PipelineController with one single frame source ("camera-1", timestamp 2500),
    ///      And VisualObservationPipeline configured with Threshold=30, MotionArea=40, EncodedBackground containing a red rectangle at (10,10) size 8x8, and Red color calibration,
    ///     When StartAsync("file-bridge") is called with CancellationToken.None,
    ///     Then the snapshot should contain exactly 0 TrainObservations.</description>
    /// </summary>
    [Fact]
    public async Task StartAsync_WithVisualObservationPipeline_DoesNotProduceTrainObservationsFromStaticColorBlobs()
    {
        using var staticBackgroundMat = new Cv.Mat(50, 80, Cv.MatType.CV_8UC3, Cv.Scalar.Black);
        Cv.Cv2.Rectangle(staticBackgroundMat, new Cv.Rect(10, 10, 8, 8), new Cv.Scalar(0, 0, 255), -1);
        Cv.Cv2.ImEncode(".jpg", staticBackgroundMat, out var staticBackgroundBytes);
        var sourceFrame = JpegFrame("camera-1", 2500);
        var redCalibration = new ColorCalibrationProfile("Red", 0, 10, 100, 255, 100, 255);
        var output = new RecordingOutputPort();
        await using var controller = new PipelineController(
            new SingleFrameSourceFactory(new SingleFrameSource(sourceFrame)),
            new VisualObservationPipeline(),
            new StubTracker([]),
            [output],
            new StubClock(sourceFrame.TimestampUtcMs));

        controller.SetVisualObservationSettings("camera-1", VisualObservationSettings.Default with
        {
            Threshold = 30,
            MotionArea = 20,
            EncodedBackground = staticBackgroundBytes,
            ColorCalibrations = [redCalibration]
        });

        await controller.StartAsync("camera-1", CancellationToken.None);
        var snapshot = await output.WaitForSnapshotAsync();
        await controller.StopAsync(CancellationToken.None);

        Assert.Empty(snapshot.TrainObservations);
    }

    /// <summary>
    /// <description>Feature: PipelineController with configured background publishes moving object observation from first file source frame.
    /// 
    ///   Scenario: A foreground region (white rectangle) in a file camera frame produces a MovingObjectObservation when compared against the encoded background.
    ///     Given a PipelineController with one single frame source ("file-bridge", timestamp 2400, white rectangle at (20,12) size 12x10),
    ///      And VisualObservationPipeline configured with Threshold=20, MotionArea=40, MorphKernelSize=1, ProcessMaxWidth=100, EncodedBackground from an empty black image,
    ///     When StartAsync("file-bridge") is called with CancellationToken.None,
    ///     Then the snapshot should contain exactly one MovingObjectObservation with SourceId "file-bridge".</description>
    /// </summary>
    [Fact]
    public async Task StartAsync_WithConfiguredBackground_PublishesMovingObjectObservationFromFirstFileSourceFrame()
    {
        var sourceFrame = JpegFrame("file-bridge", 2400, new Cv.Rect(20, 12, 12, 10));
        var output = new RecordingOutputPort();
        await using var controller = new PipelineController(
            new SingleFrameSourceFactory(new SingleFrameSource(sourceFrame)),
            new VisualObservationPipeline(),
            new StubTracker([]),
            [output],
            new StubClock(sourceFrame.TimestampUtcMs));
        controller.SetVisualObservationSettings("file-bridge", VisualObservationSettings.Default with
        {
            Threshold = 20,
            MotionArea = 40,
            MorphKernelSize = 1,
            ProcessMaxWidth = 100,
            EncodedBackground = CreateEncodedImage(width: 80, height: 50)
        });

        await controller.StartAsync("file-bridge", CancellationToken.None);
        var snapshot = await output.WaitForSnapshotAsync();
        await controller.StopAsync(CancellationToken.None);

        var observation = Assert.Single(snapshot.MovingObjectObservations);
        Assert.Equal("file-bridge", observation.SourceId);
    }

    /// <summary>
    /// <description>Feature: PipelineController publishes named debug frames when debug view is enabled.
    /// 
    ///   Scenario: Enabling debug view causes the snapshot to include source, train-observation, and train-tracking debug frames from the pipeline stages.
    ///     Given a PipelineController with one single frame source ("camera-1", timestamp 1234),
    ///      And StubVisualObservationPipeline returning one TrainObservation (Red color),
    ///      And StubTracker returning one TrainState (LocalTrainId "local-train-1"),
    ///     When SetDebugViewEnabled("camera-1", true) is called before StartAsync,
    ///      And StartAsync("camera-1") is called with CancellationToken.None,
    ///     Then the snapshot should contain a DebugFrame named "source",
    ///      And the snapshot should contain a DebugFrame named "train-observation",
    ///      And the snapshot should contain a DebugFrame named "train-tracking".</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: PipelineController publishes no debug frames when debug view is disabled.
    /// 
    ///   Scenario: Without debug view enabled, the snapshot contains zero DebugFrames regardless of pipeline content.
    ///     Given a PipelineController with one single frame source ("camera-1", timestamp 1234),
    ///      And StubDetectorManager returning no detections,
    ///      And StubTracker returning no train states,
    ///     When StartAsync("camera-1") is called with CancellationToken.None (debug view NOT enabled),
    ///     Then the snapshot should contain exactly 0 DebugFrames.</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: PipelineController publishes unmodified annotated frame when only moving object observations are present.
    /// 
    ///   Scenario: MovingObjectObservations alone do not modify the annotated frame; it remains identical to the source frame bytes.
    ///     Given a PipelineController with one single JPEG frame source ("camera-1", timestamp 1234),
    ///      And StubVisualObservationPipeline returning one MovingObjectObservation (SourceId "camera-1") and no TrainStates,
    ///     When StartAsync("camera-1") is called with CancellationToken.None,
    ///     Then the snapshot should contain exactly one MovingObjectObservation,
    ///      And TrainStates should be empty,
    ///      And AnnotatedFrame.EncodedJpeg should equal sourceFrame.EncodedJpeg (unmodified).</description>
    /// </summary>
    [Fact]
    public async Task StartAsync_WhenOnlyMovingObjectObservations_PublishesUnmodifiedAnnotatedFrame()
    {
        var sourceFrame = JpegFrame("camera-1", 1234);
        var movingEvidence = new MovingObjectObservation("camera-1", sourceFrame.TimestampUtcMs, 5, 6, 1, 2, 3, 4, 0.5f);
        var output = new RecordingOutputPort();
        await using var controller = new PipelineController(
            new SingleFrameSourceFactory(new SingleFrameSource(sourceFrame)),
            new StubVisualObservationPipeline(new VisualObservationResult([movingEvidence], [], [])),
            new StubTracker([]),
            [output],
            new StubClock(sourceFrame.TimestampUtcMs));

        await controller.StartAsync("camera-1", CancellationToken.None);
        var snapshot = await output.WaitForSnapshotAsync();
        await controller.StopAsync(CancellationToken.None);

        Assert.Single(snapshot.MovingObjectObservations);
        Assert.Empty(snapshot.TrainStates);
        Assert.Equal(sourceFrame.EncodedJpeg, snapshot.AnnotatedFrame.EncodedJpeg);
    }

    /// <summary>
    /// <description>Feature: PipelineController publishes unmodified annotated frame when only train observations are present without a corresponding TrainState.
    /// 
    ///   Scenario: A TrainObservation alone does not modify the annotated frame; it remains identical to the source frame bytes.
    ///     Given a PipelineController with one single JPEG frame source ("camera-1", timestamp 1234),
    ///      And StubVisualObservationPipeline returning one TrainObservation (Red color) and no TrainStates,
    ///     When StartAsync("camera-1") is called with CancellationToken.None,
    ///     Then the snapshot should contain exactly one TrainObservation,
    ///      And TrainStates should be empty,
    ///      And AnnotatedFrame.EncodedJpeg should equal sourceFrame.EncodedJpeg (unmodified).</description>
    /// </summary>
    [Fact]
    public async Task StartAsync_WhenOnlyTrainObservations_PublishesUnmodifiedAnnotatedFrame()
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

    /// <summary>
    /// <description>Feature: PipelineController does not draw observation on annotated frame when train observation has no corresponding TrainState.
    /// 
    ///   Scenario: A TrainObservation without a matching TrainState should not produce annotations on the output frame.
    ///     Given a PipelineController with one single JPEG frame source ("camera-1", timestamp 1234),
    ///      And StubVisualObservationPipeline returning one TrainObservation (Red color) and no TrainStates,
    ///     When StartAsync("camera-1") is called with CancellationToken.None,
    ///     Then the snapshot should contain exactly one TrainObservation,
    ///      And TrainStates should be empty,
    ///      And AnnotatedFrame.EncodedJpeg should equal sourceFrame.EncodedJpeg (unmodified — no observation drawn).</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: PipelineController draws train state on annotated frame when a TrainState exists.
    /// 
    ///   Scenario: The presence of a TrainState causes the annotated frame to differ from the source frame bytes, indicating annotations were applied.
    ///     Given a PipelineController with one single JPEG frame source ("camera-1", timestamp 1234),
    ///      And StubDetectorManager returning no detections,
    ///      And StubTracker returning one TrainState (LocalTrainId "local-train-1", Red, Moving, confidence 0.8),
    ///     When StartAsync("camera-1") is called with CancellationToken.None,
    ///     Then the snapshot should contain exactly one TrainState (same reference as returned by StubTracker),
    ///      And AnnotatedFrame.EncodedJpeg should not equal sourceFrame.EncodedJpeg (annotations applied).</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: PipelineController does not throw FirstChanceTaskCanceledException when stopping a lane waiting for fresh source frame.
    /// 
    ///   Scenario: Calling StopAsync while the lane is idle (waiting for next frame) should not produce any TaskCanceledException in the PipelineController call stack.
    ///     Given a PipelineController with one single frame source ("camera-1", timestamp 1234),
    ///      And StubDetectorManager returning no detections,
    ///      And StubTracker returning no train states,
    ///     When StartAsync("camera-1") is called and one snapshot is collected,
    ///      And a FirstChanceException handler counts TaskCanceledExceptions originating from PipelineController,
    ///     When we wait 25ms (allowing the lane to enter its idle wait state),
    ///      And StopAsync is called with CancellationToken.None,
    ///     Then taskCanceledExceptions should be exactly 0.</description>
    /// </summary>
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
        return JpegFrame(sourceId, timestampUtcMs, foreground: null);
    }

    private static FramePacket JpegFrame(string sourceId, long timestampUtcMs, Cv.Rect? foreground)
    {
        var encoded = CreateEncodedImage(width: 80, height: 50, foreground);
        return new FramePacket(sourceId, timestampUtcMs, 80, 50, encoded);
    }

    private static byte[] CreateEncodedImage(int width, int height, Cv.Rect? foreground = null)
    {
        using var image = new Cv.Mat(height, width, Cv.MatType.CV_8UC3, Cv.Scalar.Black);
        if (foreground is { } rect)
        {
            Cv.Cv2.Rectangle(image, rect, Cv.Scalar.White, -1);
        }

        Cv.Cv2.ImEncode(".jpg", image, out var encoded, [new Cv.ImageEncodingParam(Cv.ImwriteFlags.JpegQuality, 90)]);
        return encoded;
    }
}
