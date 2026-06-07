using ObjectTracker.Core.Domain;
using Xunit;
using Cv = OpenCvSharp;

namespace ObjectTracker.Vision.Tests;

public sealed class VisualObservationPipelineTests
{
    /// <summary>
    /// <description>Feature: VisualObservationPipeline returns a moving-object-evidence debug frame when motion is detected.
    /// 
    ///   Scenario: The first call establishes a background baseline; the second call with a foreground rectangle produces the evidence debug frame.
    ///     Given a VisualObservationPipeline configured with Threshold=20, MotionArea=40, MorphKernelSize=1, ProcessMaxWidth=100,
    ///      And the pipeline's first ObserveAsync call receives a plain frame (timestamp 1000) to establish baseline,
    ///     When the second ObserveAsync call receives a frame with a white rectangle at (20,12) size 12x10 (timestamp 1100),
    ///     Then the result should contain exactly one DebugFrame named "moving-object-evidence" with non-empty EncodedJpeg.</description>
    /// </summary>
    [Fact]
    public async Task ObserveAsync_ReturnsMovingObjectEvidenceDebugFrame_WhenMotionDetected()
    {
        var pipeline = new VisualObservationPipeline();
        var settings = VisualObservationSettings.Default with
        {
            Threshold = 20,
            MotionArea = 40,
            MorphKernelSize = 1,
            ProcessMaxWidth = 100
        };

        await pipeline.ObserveAsync(CreateFrame("camera-1", 1000), settings, CancellationToken.None);

        var result = await pipeline.ObserveAsync(
            CreateFrame("camera-1", 1100, new Cv.Rect(20, 12, 12, 10)),
            settings,
            CancellationToken.None);

        var movingEvidence = Assert.Single(result.DebugFrames, f => f.Name == "moving-object-evidence");
        Assert.NotEqual(default, movingEvidence.Frame.EncodedJpeg);
    }

    /// <summary>
    /// <description>Feature: VisualObservationPipeline returns a motion-mask debug frame when motion is detected.
    /// 
    ///   Scenario: The first call establishes a background baseline; the second call with a foreground rectangle produces the motion mask debug frame.
    ///     Given a VisualObservationPipeline configured with Threshold=20, MotionArea=40, MorphKernelSize=1, ProcessMaxWidth=100,
    ///      And the pipeline's first ObserveAsync call receives a plain frame (timestamp 1000) to establish baseline,
    ///     When the second ObserveAsync call receives a frame with a white rectangle at (20,12) size 12x10 (timestamp 1100),
    ///     Then the result should contain exactly one DebugFrame named "motion-mask" with non-empty EncodedJpeg.</description>
    /// </summary>
    [Fact]
    public async Task ObserveAsync_ReturnsMotionMaskDebugFrame_WhenMotionDetected()
    {
        var pipeline = new VisualObservationPipeline();
        var settings = VisualObservationSettings.Default with
        {
            Threshold = 20,
            MotionArea = 40,
            MorphKernelSize = 1,
            ProcessMaxWidth = 100
        };

        await pipeline.ObserveAsync(CreateFrame("camera-1", 1000), settings, CancellationToken.None);

        var result = await pipeline.ObserveAsync(
            CreateFrame("camera-1", 1100, new Cv.Rect(20, 12, 12, 10)),
            settings,
            CancellationToken.None);

        var motionMaskDebug = Assert.Single(result.DebugFrames, f => f.Name == "motion-mask");
        Assert.NotEqual(default, motionMaskDebug.Frame.EncodedJpeg);
    }

    /// <summary>
    /// <description>Feature: VisualObservationPipeline returns a rail-roi debug frame when rail ROI is configured.
    /// 
    ///   Scenario: A configured EncodedRailRoiMask causes the pipeline to include a rail-roi debug frame in the result.
    ///     Given a VisualObservationPipeline configured with Threshold=20, MotionArea=40, MorphKernelSize=1, ProcessMaxWidth=80, and EncodedRailRoiMask (a white rectangle at 30,10 size 20x30),
    ///      And the pipeline's first ObserveAsync call receives a plain frame (timestamp 1000) to establish baseline,
    ///     When the second ObserveAsync call receives a frame with a white rectangle at (35,20) size 12x10 (timestamp 1100),
    ///     Then the result should contain exactly one DebugFrame named "rail-roi".</description>
    /// </summary>
    [Fact]
    public async Task ObserveAsync_ReturnsRailRoiDebugFrame_WhenRailRoiConfigured()
    {
        var pipeline = new VisualObservationPipeline();
        using var railRoiMat = new Cv.Mat(50, 80, Cv.MatType.CV_8UC1, Cv.Scalar.Black);
        Cv.Cv2.Rectangle(railRoiMat, new Cv.Rect(30, 10, 20, 30), Cv.Scalar.White, -1);
        var railRoiEncoded = EncodeToJpeg(railRoiMat);

        var settings = VisualObservationSettings.Default with
        {
            Threshold = 20,
            MotionArea = 40,
            MorphKernelSize = 1,
            ProcessMaxWidth = 80,
            EncodedBackground = CreateEncodedImage(width: 80, height: 50),
            EncodedRailRoiMask = railRoiEncoded
        };

        await pipeline.ObserveAsync(CreateFrame("camera-1", 1000), settings, CancellationToken.None);

        var result = await pipeline.ObserveAsync(
            CreateFrame("camera-1", 1100, new Cv.Rect(35, 20, 12, 10)),
            settings,
            CancellationToken.None);

        Assert.Single(result.DebugFrames, f => f.Name == "rail-roi");
    }

    /// <summary>
    /// <description>Feature: VisualObservationPipeline returns a train-color-evidence debug frame when a train observation is detected.
    /// 
    ///   Scenario: A red rectangle within the moving evidence region produces both a TrainObservation and its corresponding debug frame.
    ///     Given a VisualObservationPipeline configured with Threshold=20, MotionArea=40, ColorMinPixels=40, MorphKernelSize=1, ProcessMaxWidth=100, and Red color calibration (H: 0-10, S: 100-255, V: 100-255),
    ///      And the pipeline's first ObserveAsync call receives a plain frame (timestamp 1000) to establish baseline,
    ///     When the second ObserveAsync call receives a frame with a white rectangle at (20,12) size 12x10 and Red color overlay (timestamp 1100),
    ///     Then the result should contain exactly one TrainObservation,
    ///      And the result should contain exactly one DebugFrame named "train-color-evidence" with non-empty EncodedJpeg.</description>
    /// </summary>
    [Fact]
    public async Task ObserveAsync_ReturnsTrainColorEvidenceDebugFrame_WhenTrainObservationDetected()
    {
        var pipeline = new VisualObservationPipeline();
        var settings = VisualObservationSettings.Default with
        {
            Threshold = 20,
            MotionArea = 40,
            ColorMinPixels = 40,
            MorphKernelSize = 1,
            ProcessMaxWidth = 100,
            ColorCalibrations = [new ColorCalibrationProfile("Red", 0, 10, 100, 255, 100, 255)]
        };

        await pipeline.ObserveAsync(CreateFrame("camera-1", 1000), settings, CancellationToken.None);

        var result = await pipeline.ObserveAsync(
            CreateFrame("camera-1", 1100, new Cv.Rect(20, 12, 12, 10), Cv.Scalar.Red),
            settings,
            CancellationToken.None);

        Assert.Single(result.TrainObservations);
        var trainColorDebug = Assert.Single(result.DebugFrames, f => f.Name == "train-color-evidence");
        Assert.NotEqual(default, trainColorDebug.Frame.EncodedJpeg);
    }

    /// <summary>
    /// <description>Feature: VisualObservationPipeline emits MovingObjectObservation when foreground motion exceeds threshold and motion area.
    /// 
    ///   Scenario: A white rectangle in the second frame produces a moving object observation with correct spatial properties.
    ///     Given a VisualObservationPipeline configured with Threshold=20, MotionArea=40, MorphKernelSize=1, ProcessMaxWidth=100,
    ///      And the pipeline's first ObserveAsync call receives a plain frame (timestamp 1000) to establish baseline,
    ///     When the second ObserveAsync call receives a frame with a white rectangle at (20,12) size 12x10 (timestamp 1100),
    ///     Then the result should contain exactly one MovingObjectObservation with SourceId "camera-1",
    ///      And TimestampUtcMs should be 1100,
    ///      And BoxX should be in range [19,21],
    ///      And BoxY should be in range [11,13],
    ///      AND BoxWidth should be in range [11,13],
    ///      AND BoxHeight should be in range [9,11],
    ///      AND X (center) should be in range [25,27],
    ///      AND Y (center) should be in range [16,18].</description>
    /// </summary>
    [Fact]
    public async Task ObserveAsync_EmitsMovingObjectObservation_WhenForegroundMotionExceedsThresholdAndMotionArea()
    {
        var pipeline = new VisualObservationPipeline();
        var settings = VisualObservationSettings.Default with
        {
            Threshold = 20,
            MotionArea = 40,
            MorphKernelSize = 1,
            ProcessMaxWidth = 100
        };

        await pipeline.ObserveAsync(CreateFrame("camera-1", 1000), settings, CancellationToken.None);

        var result = await pipeline.ObserveAsync(
            CreateFrame("camera-1", 1100, new Cv.Rect(20, 12, 12, 10)),
            settings,
            CancellationToken.None);

        var observation = Assert.Single(result.MovingObjectObservations);
        Assert.Equal("camera-1", observation.SourceId);
        Assert.Equal(1100, observation.TimestampUtcMs);
        Assert.InRange(observation.BoxX, 19, 21);
        Assert.InRange(observation.BoxY, 11, 13);
        Assert.InRange(observation.BoxWidth, 11, 13);
        Assert.InRange(observation.BoxHeight, 9, 11);
        Assert.InRange(observation.X, 25, 27);
        Assert.InRange(observation.Y, 16, 18);
    }

    /// <summary>
    /// <description>Feature: VisualObservationPipeline does not emit MovingObjectObservation when foreground noise is below motion area.
    /// 
    ///   Scenario: A tiny white rectangle (4x4) that falls below the MotionArea threshold produces no moving object observations.
    ///     Given a VisualObservationPipeline configured with Threshold=20, MotionArea=40, MorphKernelSize=1, ProcessMaxWidth=100,
    ///      And the pipeline's first ObserveAsync call receives a plain frame (timestamp 1000) to establish baseline,
    ///     When the second ObserveAsync call receives a frame with a white rectangle at (20,12) size 4x4 (below MotionArea=40),
    ///     Then the result should contain exactly 0 MovingObjectObservations.</description>
    /// </summary>
    [Fact]
    public async Task ObserveAsync_DoesNotEmitMovingObjectObservation_WhenForegroundNoiseIsBelowMotionArea()
    {
        var pipeline = new VisualObservationPipeline();
        var settings = VisualObservationSettings.Default with
        {
            Threshold = 20,
            MotionArea = 40,
            MorphKernelSize = 1,
            ProcessMaxWidth = 100
        };

        await pipeline.ObserveAsync(CreateFrame("camera-1", 1000), settings, CancellationToken.None);

        var result = await pipeline.ObserveAsync(
            CreateFrame("camera-1", 1100, new Cv.Rect(20, 12, 4, 4)),
            settings,
            CancellationToken.None);

        Assert.Empty(result.MovingObjectObservations);
    }

    /// <summary>
    /// <description>Feature: VisualObservationPipeline emits TrainObservation when moving region matches train color calibration.
    /// 
    ///   Scenario: A red rectangle within the moving evidence region produces a train observation with correct properties.
    ///     Given a VisualObservationPipeline configured with Threshold=20, MotionArea=40, ColorMinPixels=40, MorphKernelSize=1, ProcessMaxWidth=100, and Red color calibration (H: 0-10, S: 100-255, V: 100-255),
    ///      And the pipeline's first ObserveAsync call receives a plain frame (timestamp 1000) to establish baseline,
    ///     When the second ObserveAsync call receives a frame with a white rectangle at (20,12) size 12x10 and Red color overlay (timestamp 1100),
    ///     Then the result should contain exactly one TrainObservation with SourceId "camera-1",
    ///      And TimestampUtcMs should be 1100,
    ///      And TrainColor should be "Red",
    ///      AND BoxX should be in range [19,21],
    ///      AND BoxY should be in range [11,13],
    ///      AND BoxWidth should be in range [11,13],
    ///      AND BoxHeight should be in range [9,11].</description>
    /// </summary>
    [Fact]
    public async Task ObserveAsync_EmitsTrainObservation_WhenMovingRegionMatchesTrainColorCalibration()
    {
        var pipeline = new VisualObservationPipeline();
        var settings = VisualObservationSettings.Default with
        {
            Threshold = 20,
            MotionArea = 40,
            ColorMinPixels = 40,
            MorphKernelSize = 1,
            ProcessMaxWidth = 100,
            ColorCalibrations = [new ColorCalibrationProfile("Red", 0, 10, 100, 255, 100, 255)]
        };

        await pipeline.ObserveAsync(CreateFrame("camera-1", 1000), settings, CancellationToken.None);

        var result = await pipeline.ObserveAsync(
            CreateFrame("camera-1", 1100, new Cv.Rect(20, 12, 12, 10), Cv.Scalar.Red),
            settings,
            CancellationToken.None);

        var observation = Assert.Single(result.TrainObservations);
        Assert.Equal("camera-1", observation.SourceId);
        Assert.Equal(1100, observation.TimestampUtcMs);
        Assert.Equal("Red", observation.TrainColor);
        Assert.InRange(observation.BoxX, 19, 21);
        Assert.InRange(observation.BoxY, 11, 13);
        Assert.InRange(observation.BoxWidth, 11, 13);
        Assert.InRange(observation.BoxHeight, 9, 11);
    }

    /// <summary>
    /// <description>Feature: VisualObservationPipeline does not emit TrainObservation for static train color outside moving evidence.
    /// 
    ///   Scenario: A red region that exists only in the background (first frame) and a white region that moves should produce moving object evidence but no train observation, because the red is static.
    ///     Given a VisualObservationPipeline configured with Threshold=20, MotionArea=40, ColorMinPixels=40, MorphKernelSize=1, ProcessMaxWidth=100, and Red color calibration (H: 0-10, S: 100-255, V: 100-255),
    ///      And the first ObserveAsync call receives a frame with red rectangle at (5,5) size 12x10 to establish baseline containing the red region,
    ///     When the second ObserveAsync call receives a frame with white moving evidence at (40,12) and red static region at (5,5),
    ///     Then the result should contain exactly one MovingObjectObservation (from the white motion),
    ///      And the result should contain exactly 0 TrainObservations (red is static).</description>
    /// </summary>
    [Fact]
    public async Task ObserveAsync_DoesNotEmitTrainObservation_ForStaticTrainColorOutsideMovingEvidence()
    {
        var pipeline = new VisualObservationPipeline();
        var settings = VisualObservationSettings.Default with
        {
            Threshold = 20,
            MotionArea = 40,
            ColorMinPixels = 40,
            MorphKernelSize = 1,
            ProcessMaxWidth = 100,
            ColorCalibrations = [new ColorCalibrationProfile("Red", 0, 10, 100, 255, 100, 255)]
        };

        await pipeline.ObserveAsync(
            CreateFrame("camera-1", 1000, new Cv.Rect(5, 5, 12, 10), Cv.Scalar.Red),
            settings,
            CancellationToken.None);

        var result = await pipeline.ObserveAsync(
            CreateFrame(
                "camera-1",
                1100,
                new ForegroundRegion(new Cv.Rect(5, 5, 12, 10), Cv.Scalar.Red),
                new ForegroundRegion(new Cv.Rect(40, 12, 12, 10), Cv.Scalar.White)),
            settings,
            CancellationToken.None);

        Assert.Single(result.MovingObjectObservations);
        Assert.Empty(result.TrainObservations);
    }

    /// <summary>
    /// <description>Feature: VisualObservationPipeline does not emit TrainObservation when moving color evidence is below ColorMinPixels.
    /// 
    ///   Scenario: A small red region (4x4) inside the moving evidence area that falls below ColorMinPixels=40 produces no train observation.
    ///     Given a VisualObservationPipeline configured with Threshold=20, MotionArea=40, ColorMinPixels=40, MorphKernelSize=1, ProcessMaxWidth=100, and Red color calibration (H: 0-10, S: 100-255, V: 100-255),
    ///      And the pipeline's first ObserveAsync call receives a plain frame (timestamp 1000) to establish baseline,
    ///     When the second ObserveAsync call receives a frame with white moving evidence at (20,12) size 12x10 containing a small red patch of 4x4 pixels,
    ///     Then the result should contain exactly one MovingObjectObservation (white motion),
    ///      And the result should contain exactly 0 TrainObservations (red area below ColorMinPixels).</description>
    /// </summary>
    [Fact]
    public async Task ObserveAsync_DoesNotEmitTrainObservation_WhenMovingColorEvidenceIsBelowColorMinPixels()
    {
        var pipeline = new VisualObservationPipeline();
        var settings = VisualObservationSettings.Default with
        {
            Threshold = 20,
            MotionArea = 40,
            ColorMinPixels = 40,
            MorphKernelSize = 1,
            ProcessMaxWidth = 100,
            ColorCalibrations = [new ColorCalibrationProfile("Red", 0, 10, 100, 255, 100, 255)]
        };

        await pipeline.ObserveAsync(CreateFrame("camera-1", 1000), settings, CancellationToken.None);

        var result = await pipeline.ObserveAsync(
            CreateFrame(
                "camera-1",
                1100,
                new ForegroundRegion(new Cv.Rect(20, 12, 12, 10), Cv.Scalar.White),
                new ForegroundRegion(new Cv.Rect(20, 12, 4, 4), Cv.Scalar.Red)),
            settings,
            CancellationToken.None);

        Assert.Single(result.MovingObjectObservations);
        Assert.Empty(result.TrainObservations);
    }

    /// <summary>
    /// <description>Feature: VisualObservationPipeline connects fragmented moving evidence when morph kernel size closes the gap.
    /// 
    ///   Scenario: Two adjacent white rectangles separated by a 10-pixel gap produce two observations without refinement but merge into one with MorphKernelSize=5.
    ///     Given a VisualObservationPipeline without refinement (MorphKernelSize=1) and one with refinement (MorphKernelSize=5), both configured with Threshold=20, MotionArea=20, ProcessMaxWidth=100,
    ///      And both pipelines have established baseline from plain frames,
    ///     When both observe a frame containing two white rectangles at (20,20) size 10x8 and (32,20) size 10x8 (gap of 10 pixels),
    ///     Then the unrefined result should contain exactly 2 MovingObjectObservations,
    ///      And the refined result should contain exactly 1 MovingObjectObservation with BoxX in range [19,21] and BoxWidth in range [21,23] (merged).</description>
    /// </summary>
    [Fact]
    public async Task ObserveAsync_ConnectsFragmentedMovingEvidence_WhenMorphKernelSizeClosesGap()
    {
        var pipelineWithoutRefinement = new VisualObservationPipeline();
        var pipelineWithRefinement = new VisualObservationPipeline();
        var baseSettings = VisualObservationSettings.Default with
        {
            Threshold = 20,
            MotionArea = 20,
            ProcessMaxWidth = 100
        };

        await pipelineWithoutRefinement.ObserveAsync(CreateFrame("camera-1", 1000), baseSettings with { MorphKernelSize = 1 }, CancellationToken.None);
        await pipelineWithRefinement.ObserveAsync(CreateFrame("camera-1", 1000), baseSettings with { MorphKernelSize = 5 }, CancellationToken.None);

        var fragmentedFrame = CreateFrame(
            "camera-1",
            1100,
            new ForegroundRegion(new Cv.Rect(20, 20, 10, 8), Cv.Scalar.White),
            new ForegroundRegion(new Cv.Rect(32, 20, 10, 8), Cv.Scalar.White));

        var unrefinedResult = await pipelineWithoutRefinement.ObserveAsync(
            fragmentedFrame,
            baseSettings with { MorphKernelSize = 1 },
            CancellationToken.None);
        var refinedResult = await pipelineWithRefinement.ObserveAsync(
            fragmentedFrame,
            baseSettings with { MorphKernelSize = 5 },
            CancellationToken.None);

        Assert.Equal(2, unrefinedResult.MovingObjectObservations.Count);
        var observation = Assert.Single(refinedResult.MovingObjectObservations);
        Assert.InRange(observation.BoxX, 19, 21);
        Assert.InRange(observation.BoxWidth, 21, 23);
    }

    /// <summary>
    /// <description>Feature: VisualObservationPipeline does not promote tiny noise below motion area when morph kernel size refines the mask.
    /// 
    ///   Scenario: A 4x4 white rectangle that falls below MotionArea=40 is filtered out even with MorphKernelSize=5 refinement enabled.
    ///     Given a VisualObservationPipeline configured with Threshold=20, MotionArea=40, MorphKernelSize=5, ProcessMaxWidth=100,
    ///      And the pipeline's first ObserveAsync call receives a plain frame (timestamp 1000) to establish baseline,
    ///     When the second ObserveAsync call receives a frame with a white rectangle at (20,12) size 4x4 (below MotionArea=40),
    ///     Then the result should contain exactly 0 MovingObjectObservations.</description>
    /// </summary>
    [Fact]
    public async Task ObserveAsync_DoesNotPromoteTinyNoiseBelowMotionArea_WhenMorphKernelSizeRefinesMask()
    {
        var pipeline = new VisualObservationPipeline();
        var settings = VisualObservationSettings.Default with
        {
            Threshold = 20,
            MotionArea = 40,
            MorphKernelSize = 5,
            ProcessMaxWidth = 100
        };

        await pipeline.ObserveAsync(CreateFrame("camera-1", 1000), settings, CancellationToken.None);

        var result = await pipeline.ObserveAsync(
            CreateFrame("camera-1", 1100, new Cv.Rect(20, 12, 4, 4)),
            settings,
            CancellationToken.None);

        Assert.Empty(result.MovingObjectObservations);
    }

    /// <summary>
    /// <description>Feature: VisualObservationPipeline reports observations in ProcessMaxWidth coordinate space when source frame is resized.
    /// 
    ///   Scenario: A 160x100 source frame with a red rectangle at (40,20) size 40x20 is scaled to ProcessMaxWidth=80 and produces observations in the downscaled coordinate system.
    ///     Given a VisualObservationPipeline configured with Threshold=20, MotionArea=20, MorphKernelSize=1, ProcessMaxWidth=80, ColorMinPixels=20, and Red color calibration (H: 0-10, S: 100-255, V: 100-255),
    ///      And the pipeline's first ObserveAsync call receives a 160x100 plain frame (timestamp 1000) to establish baseline,
    ///     When the second ObserveAsync call receives a 160x100 frame with red rectangle at (40,20) size 40x20 (timestamp 1100),
    ///     Then the MovingObjectObservation should have BoxX in range [19,21], BoxY in range [9,11], BoxWidth in range [19,21], BoxHeight in range [9,11] (all halved from original 40x40),
    ///      And the TrainObservation should have TrainColor "Red" with same halved bounding box coordinates.</description>
    /// </summary>
    [Fact]
    public async Task ObserveAsync_ReportsObservationsInProcessMaxWidthCoordinateSpace_WhenSourceFrameIsResized()
    {
        var pipeline = new VisualObservationPipeline();
        var settings = VisualObservationSettings.Default with
        {
            Threshold = 20,
            MotionArea = 20,
            MorphKernelSize = 1,
            ProcessMaxWidth = 80,
            ColorMinPixels = 20,
            ColorCalibrations = [new ColorCalibrationProfile("Red", 0, 10, 100, 255, 100, 255)]
        };

        await pipeline.ObserveAsync(CreateFrame("camera-1", 1000, width: 160, height: 100), settings, CancellationToken.None);

        var result = await pipeline.ObserveAsync(
            CreateFrame("camera-1", 1100, width: 160, height: 100, new ForegroundRegion(new Cv.Rect(40, 20, 40, 20), Cv.Scalar.Red)),
            settings,
            CancellationToken.None);

        var movingObjectObservation = Assert.Single(result.MovingObjectObservations);
        Assert.InRange(movingObjectObservation.BoxX, 19, 21);
        Assert.InRange(movingObjectObservation.BoxY, 9, 11);
        Assert.InRange(movingObjectObservation.BoxWidth, 19, 21);
        Assert.InRange(movingObjectObservation.BoxHeight, 9, 11);

        var trainObservation = Assert.Single(result.TrainObservations);
        Assert.Equal("Red", trainObservation.TrainColor);
        Assert.InRange(trainObservation.BoxX, 19, 21);
        Assert.InRange(trainObservation.BoxY, 9, 11);
        Assert.InRange(trainObservation.BoxWidth, 19, 21);
        Assert.InRange(trainObservation.BoxHeight, 9, 11);
    }

    /// <summary>
    /// <description>Feature: VisualObservationPipeline does not emit MovingObjectObservations when motion is outside the rail ROI mask.
    /// 
    ///   Scenario: A white rectangle at (5,5) falls outside a rail ROI mask defined as white area at (30,10) size 20x30, so no observation is produced.
    ///     Given a VisualObservationPipeline configured with Threshold=20, MotionArea=40, MorphKernelSize=1, ProcessMaxWidth=80, EncodedBackground from empty image, and EncodedRailRoiMask (white rectangle at 30,10 size 20x30),
    ///     When ObserveAsync is called with a frame containing white rectangle at (5,5) size 12x10 (outside rail ROI),
    ///     Then the result should contain exactly 0 MovingObjectObservations.</description>
    /// </summary>
    [Fact]
    public async Task ObserveAsync_DoesNotEmitMovingObjectObservations_WhenMotionIsOutsideRailRoiMask()
    {
        var pipeline = new VisualObservationPipeline();
        using var railRoiMat = new Cv.Mat(50, 80, Cv.MatType.CV_8UC1, Cv.Scalar.Black);
        Cv.Cv2.Rectangle(railRoiMat, new Cv.Rect(30, 10, 20, 30), Cv.Scalar.White, -1);
        var railRoiEncoded = EncodeToJpeg(railRoiMat);

        var settings = VisualObservationSettings.Default with
        {
            Threshold = 20,
            MotionArea = 40,
            MorphKernelSize = 1,
            ProcessMaxWidth = 80,
            EncodedBackground = CreateEncodedImage(width: 80, height: 50),
            EncodedRailRoiMask = railRoiEncoded
        };

        var result = await pipeline.ObserveAsync(
            CreateFrame("camera-1", 1100, new Cv.Rect(5, 5, 12, 10)),
            settings,
            CancellationToken.None);

        Assert.Empty(result.MovingObjectObservations);
    }

    /// <summary>
    /// <description>Feature: VisualObservationPipeline emits MovingObjectObservations when motion is inside the rail ROI mask.
    /// 
    ///   Scenario: A white rectangle at (35,20) falls inside a rail ROI mask defined as white area at (30,10) size 20x30, so an observation is produced.
    ///     Given a VisualObservationPipeline configured with Threshold=20, MotionArea=40, MorphKernelSize=1, ProcessMaxWidth=80, EncodedBackground from empty image, and EncodedRailRoiMask (white rectangle at 30,10 size 20x30),
    ///     When ObserveAsync is called with a frame containing white rectangle at (35,20) size 12x10 (inside rail ROI),
    ///     Then the result should contain exactly one MovingObjectObservation with SourceId "camera-1".</description>
    /// </summary>
    [Fact]
    public async Task ObserveAsync_EmitsMovingObjectObservations_WhenMotionIsInsideRailRoiMask()
    {
        var pipeline = new VisualObservationPipeline();
        using var railRoiMat = new Cv.Mat(50, 80, Cv.MatType.CV_8UC1, Cv.Scalar.Black);
        Cv.Cv2.Rectangle(railRoiMat, new Cv.Rect(30, 10, 20, 30), Cv.Scalar.White, -1);
        var railRoiEncoded = EncodeToJpeg(railRoiMat);

        var settings = VisualObservationSettings.Default with
        {
            Threshold = 20,
            MotionArea = 40,
            MorphKernelSize = 1,
            ProcessMaxWidth = 80,
            EncodedBackground = CreateEncodedImage(width: 80, height: 50),
            EncodedRailRoiMask = railRoiEncoded
        };

        var result = await pipeline.ObserveAsync(
            CreateFrame("camera-1", 1100, new Cv.Rect(35, 20, 12, 10)),
            settings,
            CancellationToken.None);

        var observation = Assert.Single(result.MovingObjectObservations);
        Assert.Equal("camera-1", observation.SourceId);
    }

    /// <summary>
    /// <description>Feature: VisualObservationPipeline uses configured background for first runtime source frame.
    /// 
    ///   Scenario: An encoded background image is used as the baseline for motion detection on the very first ObserveAsync call (no warm-up needed).
    ///     Given a VisualObservationPipeline configured with Threshold=20, MotionArea=40, MorphKernelSize=1, ProcessMaxWidth=100, and EncodedBackground from an empty 80x50 image,
    ///     When ObserveAsync is called directly (without prior baseline call) with a frame containing white rectangle at (20,12) size 12x10,
    ///     Then the result should contain exactly one MovingObjectObservation with SourceId "file-bridge",
    ///      And TimestampUtcMs should be 1100,
    ///      AND BoxX should be in range [19,21],
    ///      AND BoxY should be in range [11,13].</description>
    /// </summary>
    [Fact]
    public async Task ObserveAsync_UsesConfiguredBackgroundForFirstRuntimeSourceFrame()
    {
        var pipeline = new VisualObservationPipeline();
        var settings = VisualObservationSettings.Default with
        {
            Threshold = 20,
            MotionArea = 40,
            MorphKernelSize = 1,
            ProcessMaxWidth = 100,
            EncodedBackground = CreateEncodedImage(width: 80, height: 50)
        };

        var result = await pipeline.ObserveAsync(
            CreateFrame("file-bridge", 1100, new Cv.Rect(20, 12, 12, 10)),
            settings,
            CancellationToken.None);

        var observation = Assert.Single(result.MovingObjectObservations);
        Assert.Equal("file-bridge", observation.SourceId);
        Assert.Equal(1100, observation.TimestampUtcMs);
        Assert.InRange(observation.BoxX, 19, 21);
        Assert.InRange(observation.BoxY, 11, 13);
    }

    /// <summary>
    /// <description>Feature: VisualObservationPipeline does not emit TrainObservations when motion is outside rail ROI mask.
    /// 
    ///   Scenario: A red rectangle at (5,5) falls outside a rail ROI mask defined as white area at (30,10) size 20x30, so no train observation is produced even though the color matches calibration.
    ///     Given a VisualObservationPipeline configured with Threshold=20, MotionArea=40, ColorMinPixels=40, MorphKernelSize=1, ProcessMaxWidth=80, EncodedBackground from empty image, EncodedRailRoiMask (white rectangle at 30,10 size 20x30), and Red color calibration (H: 0-10, S: 100-255, V: 100-255),
    ///     When ObserveAsync is called with a frame containing red rectangle at (5,5) size 12x10 (outside rail ROI),
    ///     Then the result should contain exactly 0 TrainObservations.</description>
    /// </summary>
    [Fact]
    public async Task ObserveAsync_DoesNotEmitTrainObservations_WhenMotionIsOutsideRailRoiMask()
    {
        var pipeline = new VisualObservationPipeline();
        using var railRoiMat = new Cv.Mat(50, 80, Cv.MatType.CV_8UC1, Cv.Scalar.Black);
        Cv.Cv2.Rectangle(railRoiMat, new Cv.Rect(30, 10, 20, 30), Cv.Scalar.White, -1);
        var railRoiEncoded = EncodeToJpeg(railRoiMat);

        var settings = VisualObservationSettings.Default with
        {
            Threshold = 20,
            MotionArea = 40,
            ColorMinPixels = 40,
            MorphKernelSize = 1,
            ProcessMaxWidth = 80,
            EncodedBackground = CreateEncodedImage(width: 80, height: 50),
            EncodedRailRoiMask = railRoiEncoded,
            ColorCalibrations = [new ColorCalibrationProfile("Red", 0, 10, 100, 255, 100, 255)]
        };

        var result = await pipeline.ObserveAsync(
            CreateFrame("camera-1", 1100, new Cv.Rect(5, 5, 12, 10), Cv.Scalar.Red),
            settings,
            CancellationToken.None);

        Assert.Empty(result.TrainObservations);
    }

    private static FramePacket CreateFrame(string sourceId, long timestampUtcMs, Cv.Rect foreground, Cv.Scalar? foregroundColor = null)
    {
        return CreateFrame(
            sourceId,
            timestampUtcMs,
            new ForegroundRegion(foreground, foregroundColor ?? Cv.Scalar.White));
    }

    private static FramePacket CreateFrame(string sourceId, long timestampUtcMs, params ForegroundRegion[] foregroundRegions)
    {
        return CreateFrame(sourceId, timestampUtcMs, width: 80, height: 50, foregroundRegions);
    }

    private static FramePacket CreateFrame(string sourceId, long timestampUtcMs, int width, int height, params ForegroundRegion[] foregroundRegions)
    {
        var encoded = CreateEncodedImage(width, height, foregroundRegions);
        return new FramePacket(sourceId, timestampUtcMs, width, height, encoded);
    }

    private static byte[] CreateEncodedImage(int width, int height, params ForegroundRegion[] foregroundRegions)
    {
        using var image = new Cv.Mat(height, width, Cv.MatType.CV_8UC3, Cv.Scalar.Black);
        foreach (var foregroundRegion in foregroundRegions)
        {
            Cv.Cv2.Rectangle(image, foregroundRegion.Rect, foregroundRegion.Color, -1);
        }

        Cv.Cv2.ImEncode(".jpg", image, out var encoded, [new Cv.ImageEncodingParam(Cv.ImwriteFlags.JpegQuality, 100)]);
        return encoded;
    }

    private static byte[] EncodeToJpeg(Cv.Mat mat)
    {
        Cv.Cv2.ImEncode(".jpg", mat, out var encoded, [new Cv.ImageEncodingParam(Cv.ImwriteFlags.JpegQuality, 100)]);
        return encoded;
    }

    private readonly record struct ForegroundRegion(Cv.Rect Rect, Cv.Scalar Color);
}
