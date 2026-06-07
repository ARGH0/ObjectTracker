using ObjectTracker.Core.Domain;
using Xunit;
using Cv = OpenCvSharp;

namespace ObjectTracker.Vision.Tests;

public sealed class VisualObservationPipelineTests
{
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

    private readonly record struct ForegroundRegion(Cv.Rect Rect, Cv.Scalar Color);
}
