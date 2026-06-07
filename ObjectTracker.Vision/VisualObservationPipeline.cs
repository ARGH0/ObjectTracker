using ObjectTracker.Core.Domain;
using Cv = OpenCvSharp;

namespace ObjectTracker.Vision;

public interface IVisualObservationPipeline
{
    Task<VisualObservationResult> ObserveAsync(
        FramePacket sourceFrame,
        VisualObservationSettings settings,
        CancellationToken cancellationToken);
}

public sealed record VisualObservationSettings(
    int Threshold,
    int MotionArea,
    int ColorMinPixels,
    int MorphKernelSize,
    int ProcessMaxWidth,
    IReadOnlyList<ColorCalibrationProfile> ColorCalibrations,
    bool DebugViewEnabled)
{
    public static VisualObservationSettings Default { get; } = new(
        Threshold: 100,
        MotionArea: 220,
        ColorMinPixels: 40,
        MorphKernelSize: 3,
        ProcessMaxWidth: 640,
        ColorCalibrations: [],
        DebugViewEnabled: false);
}

public sealed record VisualObservationResult(
    IReadOnlyList<MovingObjectObservation> MovingObjectObservations,
    IReadOnlyList<TrainObservation> TrainObservations,
    IReadOnlyList<DebugFrame> DebugFrames)
{
    public static VisualObservationResult Empty { get; } = new([], [], []);
}

public sealed class VisualObservationPipeline : IVisualObservationPipeline
{
    private readonly MotionMaskRefiner motionMaskRefiner = new();
    private readonly Lock backgroundLock = new();
    private readonly Dictionary<string, Cv.Mat> backgroundsBySourceId = new(StringComparer.OrdinalIgnoreCase);

    public Task<VisualObservationResult> ObserveAsync(
        FramePacket sourceFrame,
        VisualObservationSettings settings,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var color = Cv.Cv2.ImDecode(sourceFrame.EncodedJpeg, Cv.ImreadModes.Color);
        if (color.Empty())
        {
            return Task.FromResult(VisualObservationResult.Empty);
        }

        var processSize = BuildProcessSize(sourceFrame.Width, sourceFrame.Height, settings.ProcessMaxWidth);
        using var gray = new Cv.Mat();
        using var resized = new Cv.Mat();
        Cv.Cv2.CvtColor(color, gray, Cv.ColorConversionCodes.BGR2GRAY);
        Cv.Cv2.Resize(gray, resized, processSize, interpolation: Cv.InterpolationFlags.Area);

        Cv.Mat background;
        lock (backgroundLock)
        {
            if (!backgroundsBySourceId.TryGetValue(sourceFrame.SourceId, out var existingBackground) || existingBackground.Size() != processSize)
            {
                existingBackground?.Dispose();
                backgroundsBySourceId[sourceFrame.SourceId] = resized.Clone();
                return Task.FromResult(VisualObservationResult.Empty);
            }

            background = existingBackground.Clone();
        }

        using (background)
        using (var diff = new Cv.Mat())
        using (var mask = new Cv.Mat())
        {
            Cv.Cv2.Absdiff(background, resized, diff);
            Cv.Cv2.Threshold(diff, mask, settings.Threshold, 255, Cv.ThresholdTypes.Binary);
            using var refinedMask = motionMaskRefiner.Refine(mask, BuildRefinerOptions(settings.MorphKernelSize));
            var movingObjectObservations = GetMovingObjectObservations(refinedMask, sourceFrame, settings.MotionArea);

            return Task.FromResult(new VisualObservationResult(movingObjectObservations, [], []));
        }
    }

    private static IReadOnlyList<MovingObjectObservation> GetMovingObjectObservations(
        Cv.Mat motionMask,
        FramePacket sourceFrame,
        int motionArea)
    {
        Cv.Cv2.FindContours(motionMask, out var contours, out _, Cv.RetrievalModes.External, Cv.ContourApproximationModes.ApproxSimple);

        var observations = new List<MovingObjectObservation>();
        foreach (var contour in contours)
        {
            var area = Cv.Cv2.ContourArea(contour);
            if (area < motionArea)
            {
                continue;
            }

            var rect = Cv.Cv2.BoundingRect(contour);
            observations.Add(new MovingObjectObservation(
                sourceFrame.SourceId,
                sourceFrame.TimestampUtcMs,
                rect.X + (rect.Width / 2f),
                rect.Y + (rect.Height / 2f),
                rect.X,
                rect.Y,
                rect.Width,
                rect.Height,
                1f));
        }

        return observations;
    }

    private static Cv.Size BuildProcessSize(int sourceWidth, int sourceHeight, int maxWidth)
    {
        if (sourceWidth <= maxWidth)
        {
            return new Cv.Size(sourceWidth, sourceHeight);
        }

        var scale = (double)maxWidth / sourceWidth;
        var targetHeight = Math.Max(1, (int)Math.Round(sourceHeight * scale));
        return new Cv.Size(maxWidth, targetHeight);
    }

    private static MotionMaskRefiner.Options BuildRefinerOptions(int morphKernelSize)
    {
        var closeKernelSize = Math.Max(1, morphKernelSize);
        var openKernelSize = closeKernelSize >= 5 ? 3 : 1;
        return new MotionMaskRefiner.Options(closeKernelSize, openKernelSize);
    }
}
