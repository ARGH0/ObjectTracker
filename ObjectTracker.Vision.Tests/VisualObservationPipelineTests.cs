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

        await pipeline.ObserveAsync(CreateFrame("camera-1", 1000, null), settings, CancellationToken.None);

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

        await pipeline.ObserveAsync(CreateFrame("camera-1", 1000, null), settings, CancellationToken.None);

        var result = await pipeline.ObserveAsync(
            CreateFrame("camera-1", 1100, new Cv.Rect(20, 12, 4, 4)),
            settings,
            CancellationToken.None);

        Assert.Empty(result.MovingObjectObservations);
    }

    private static FramePacket CreateFrame(string sourceId, long timestampUtcMs, Cv.Rect? foreground)
    {
        using var image = new Cv.Mat(50, 80, Cv.MatType.CV_8UC3, Cv.Scalar.Black);
        if (foreground is { } rect)
        {
            Cv.Cv2.Rectangle(image, rect, Cv.Scalar.White, -1);
        }

        Cv.Cv2.ImEncode(".jpg", image, out var encoded, [new Cv.ImageEncodingParam(Cv.ImwriteFlags.JpegQuality, 100)]);
        return new FramePacket(sourceId, timestampUtcMs, image.Width, image.Height, encoded);
    }
}
