using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ObjectTracker.Core.Domain;
using OpenCvSharp;

namespace ObjectTracker.UI.Desktop;

internal sealed class BackgroundEstimationEngine(
    SessionCalibrationService? sessionCalibration = null,
    RailRoiMaskBuilder? railRoiMaskBuilder = null,
    MotionMaskRefiner? motionMaskRefiner = null)
{
    private readonly SessionCalibrationService _sessionCalibration = sessionCalibration ?? new();
    private readonly RailRoiMaskBuilder _railRoiMaskBuilder = railRoiMaskBuilder ?? new();
    private readonly MotionMaskRefiner _motionMaskRefiner = motionMaskRefiner ?? new();

    public Action<TrainDetected>? OnTrainDetected { get; set; }

    internal readonly record struct TrainDetected(
        string CameraZoneId,
        int LocalTrainId,
        ConfiguredTrain Train,
        float PositionX,
        float PositionY,
        float BoundingBoxWidth,
        float BoundingBoxHeight,
        string TrainColor,
        float Confidence,
        long Timestamp,
        int FrameNumber);

    public async Task<VideoProcessResult> ProcessAsync(
        IVideoSource source,
        int sampleCount,
        int threshold,
        ProcessingOptions options,
        string? bakeImagePath,
        Func<PreviewFrameSet, Task> onFrame,
        Func<string, Task> onStatus,
        Func<LiveTuning>? getLiveTuning,
        Func<bool>? shouldStopEarly,
        CancellationToken cancellationToken)
    {
        var first = source.ReadLatestFrame();
        if (first is null)
        {
            return VideoProcessResult.Fail($"{source.SourceLabel} did not return frames.");
        }

        var firstSnapshot = first.Value;
        var processSize = BuildProcessSize(firstSnapshot.Width, firstSnapshot.Height, options.ProcessMaxWidth);
        var background = await CreateMedianBackgroundFromSourceAsync(
            source,
            firstSnapshot,
            sampleCount,
            processSize,
            bakeImagePath,
            onStatus,
            cancellationToken);

        using var medianBackground = background.MedianBackground;
        using var railRoiMask = _railRoiMaskBuilder.BuildFromBackground(medianBackground);

        return await ProcessFramesFromSourceAsync(
            source,
            processSize,
            medianBackground,
            railRoiMask,
            threshold,
            options,
            onFrame,
            onStatus,
            getLiveTuning,
            shouldStopEarly,
            cancellationToken);
    }

    private static async Task<UnifiedBackgroundSample> CreateMedianBackgroundFromSourceAsync(
        IVideoSource source,
        VideoFrameSnapshot firstSnapshot,
        int sampleCount,
        Size processSize,
        string? bakeImagePath,
        Func<string, Task> onStatus,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(bakeImagePath))
        {
            await onStatus($"using bake image {Path.GetFileName(bakeImagePath)} for {source.SourceLabel}");
            return new UnifiedBackgroundSample(LoadBackgroundImageMat(bakeImagePath, processSize), firstSnapshot.FrameVersion);
        }

        var samplingCount = Math.Max(5, sampleCount);
        var progressStep = Math.Max(1, samplingCount / 5);
        var sampledFrames = new List<Mat>(samplingCount);

        await onStatus($"estimating background for {source.SourceLabel}...");
        AddFrameSample(firstSnapshot, processSize, sampledFrames);
        await ReportSamplingProgressAsync(source.SourceLabel, sampledFrames.Count, samplingCount, progressStep, onStatus);

        while (sampledFrames.Count < samplingCount && !cancellationToken.IsCancellationRequested)
        {
            var snapshot = source.ReadLatestFrame();
            if (snapshot is null)
            {
                await Task.Delay(50, cancellationToken);
                continue;
            }

            AddFrameSample(snapshot.Value, processSize, sampledFrames);
            await ReportSamplingProgressAsync(source.SourceLabel, sampledFrames.Count, samplingCount, progressStep, onStatus);
        }

        if (sampledFrames.Count == 0)
        {
            throw new InvalidOperationException("No live frames available to estimate background.");
        }

        await onStatus($"live capture ready: {source.SourceLabel}");
        return new UnifiedBackgroundSample(BuildMedianBackground(sampledFrames, processSize), firstSnapshot.FrameVersion);
    }

    private async Task<VideoProcessResult> ProcessFramesFromSourceAsync(
        IVideoSource source,
        Size processSize,
        Mat medianBackground,
        Mat railRoiMask,
        int threshold,
        ProcessingOptions options,
        Func<PreviewFrameSet, Task> onFrame,
        Func<string, Task> onStatus,
        Func<LiveTuning>? getLiveTuning,
        Func<bool>? shouldStopEarly,
        CancellationToken cancellationToken)
    {
        var frameIndex = 0;
        var roiCoverage = ComputeMaskCoverage(railRoiMask);

        await onStatus($"rail ROI active: {roiCoverage * 100:0.0}% of frame");

        using var gray = new Mat();
        using var colorResized = new Mat();
        using var resized = new Mat();
        using var diff = new Mat();
        using var mask = new Mat();
        using var refinedMask = new Mat();
        var activeMorphKernelSize = options.MorphKernelSize;
        using var movingColor = new Mat();
        using var colorDetections = new Mat();
        using var hsv = new Mat();
        var activeTrains = options.Trains;

        var previousTracks = new Dictionary<int, MotionTrackState>();
        var nextTrackId = 1;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (shouldStopEarly?.Invoke() == true)
            {
                return VideoProcessResult.Stopped();
            }

            var snapshot = await source.ReadFrameAsync(cancellationToken);
            if (snapshot is null)
            {
                await Task.Delay(16, cancellationToken);
                continue;
            }

            using var frame = DecodeSnapshot(snapshot.Value);
            if (frame.Empty())
            {
                continue;
            }

            var activeThreshold = threshold;
            var activeMinMotionArea = options.MinMotionArea;
            var activeMinColorPixels = options.MinColorPixels;

            if (getLiveTuning is not null)
            {
                var live = getLiveTuning();
                activeThreshold = live.Threshold;
                activeMinMotionArea = live.MinMotionArea;
                activeMinColorPixels = live.MinColorPixels;
                activeMorphKernelSize = live.MorphKernelSize;
            }

            // Create the analysis frames first. Rendering is added later so the source mats stay clean.
            CreateProcessFrames(
                frame,
                processSize,
                medianBackground,
                railRoiMask,
                activeThreshold,
                activeMorphKernelSize,
                gray,
                colorResized,
                resized,
                diff,
                mask,
                refinedMask);

            // Detect train candidates and classify their operator-recognized Train Color.
            var movingRects = GetMovingObjectRectangles(refinedMask, activeMinMotionArea);
            var frameTrains = ClassifyTrainsPerRect(
                colorResized, refinedMask, hsv, movingRects, activeTrains, activeMinColorPixels);

            // Build preview frames last by drawing overlays on top of the processed frames.
            RenderPreviewFrames(
                colorResized,
                refinedMask,
                hsv,
                movingRects,
                activeTrains,
                activeMinColorPixels,
                movingColor,
                colorDetections);

            var preview = BuildPreviewFrameSet(movingColor, colorDetections);
            await onFrame(preview);

            if (OnTrainDetected is not null && movingRects.Count > 0)
            {
                foreach (var candidate in BuildTrainDetectionCandidates(movingRects, frameTrains))
                {
                    var rect = candidate.Rect;
                    var train = candidate.Train;
                    var center = new Point2f(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);
                    OnTrainDetected(new TrainDetected(
                        CameraZoneId: source.SourceLabel,
                        LocalTrainId: GetNextTrackId(previousTracks, ref nextTrackId, center),
                        Train: train.Train,
                        PositionX: center.X,
                        PositionY: center.Y,
                        BoundingBoxWidth: rect.Width,
                        BoundingBoxHeight: rect.Height,
                        TrainColor: train.TrainName,
                        Confidence: 1.0f,
                        Timestamp: snapshot.Value.TimestampUtcMs,
                        FrameNumber: frameIndex));
                }
            }

            frameIndex++;
            if (frameIndex % 20 == 0)
            {
                await onStatus($"processing {source.SourceLabel} | frame {frameIndex}");
            }
        }

        return VideoProcessResult.Ok();
    }

    public async Task<string> EnsureBakedBackgroundAsync(
        string videoPath,
        int sampleCount,
        ProcessingOptions options,
        string? bakeImagePath,
        CancellationToken cancellationToken,
        Func<string, Task>? onStatus = null)
    {
        return await _sessionCalibration.EnsureBakedBackgroundAsync(
            videoPath,
            sampleCount,
            options.ProcessMaxWidth,
            bakeImagePath,
            cancellationToken,
            onStatus);
    }

    private static async Task ReportSamplingProgressAsync(string sourceLabel, int completed, int total, int progressStep, Func<string, Task> onStatus)
    {
        if (completed % progressStep == 0 || completed == total)
        {
            await onStatus($"sampling {sourceLabel}: frame {completed}/{total}");
        }
    }

    private static void AddFrameSample(VideoFrameSnapshot snapshot, Size processSize, List<Mat> sampledFrames)
    {
        using var frame = DecodeSnapshot(snapshot);
        using var gray = new Mat();
        Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
        var resized = new Mat();
        Cv2.Resize(gray, resized, processSize, interpolation: InterpolationFlags.Area);
        sampledFrames.Add(resized);
    }

    private static Mat DecodeSnapshot(VideoFrameSnapshot snapshot)
    {
        return Cv2.ImDecode(snapshot.EncodedJpeg, ImreadModes.Color);
    }

    private static Mat LoadBackgroundImageMat(string imagePath, Size processSize)
    {
        if (!File.Exists(imagePath))
        {
            throw new FileNotFoundException("Bake image not found.", imagePath);
        }

        using var source = Cv2.ImRead(imagePath, ImreadModes.Color);
        if (source.Empty())
        {
            throw new InvalidOperationException($"Unable to load bake image: {Path.GetFileName(imagePath)}");
        }

        using var gray = new Mat();
        Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);
        var resized = new Mat();
        Cv2.Resize(gray, resized, processSize, interpolation: InterpolationFlags.Area);
        return resized;
    }

    private static List<Rect> GetMovingObjectRectangles(Mat motionMask, int minMotionArea)
    {
        Cv2.FindContours(motionMask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        var rects = new List<Rect>();
        foreach (var contour in contours)
        {
            var area = Cv2.ContourArea(contour);
            if (area < minMotionArea)
            {
                continue;
            }

            rects.Add(Cv2.BoundingRect(contour));
        }

        return rects;
    }

    private void CreateProcessFrames(
        Mat frame,
        Size processSize,
        Mat medianBackground,
        Mat railRoiMask,
        int threshold,
        int morphKernelSize,
        Mat gray,
        Mat colorResized,
        Mat resized,
        Mat diff,
        Mat mask,
        Mat refinedMask)
    {
        Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.Resize(frame, colorResized, processSize, interpolation: InterpolationFlags.Area);
        Cv2.Resize(gray, resized, processSize, interpolation: InterpolationFlags.Area);
        Cv2.Absdiff(medianBackground, resized, diff);
        Cv2.Threshold(diff, mask, threshold, 255, ThresholdTypes.Binary);
        Cv2.BitwiseAnd(mask, railRoiMask, mask);

        using var refined = _motionMaskRefiner.Refine(mask, BuildRefinerOptions(morphKernelSize));
        refined.CopyTo(refinedMask);
    }

    private static void RenderPreviewFrames(
        Mat colorResized,
        Mat refinedMask,
        Mat hsv,
        IReadOnlyList<Rect> movingRects,
        IReadOnlyList<TrainDetectionProfile> trains,
        int minColorPixels,
        Mat movingColor,
        Mat colorDetections)
    {
        movingColor.SetTo(Scalar.Black);
        colorResized.CopyTo(movingColor, refinedMask);
        DrawMovingObjectBoxes(movingColor, movingRects);

        colorResized.CopyTo(colorDetections);
        RenderColorDetections(colorDetections, colorResized, refinedMask, hsv, movingRects, trains, minColorPixels);
    }

    private static void DrawMovingObjectBoxes(Mat destination, IReadOnlyList<Rect> movingRects)
    {
        foreach (var rect in movingRects)
        {
            Cv2.Rectangle(destination, rect, new Scalar(0, 240, 255), 2);
            Cv2.PutText(destination, "Moving", new Point(rect.X, Math.Max(16, rect.Y - 4)), HersheyFonts.HersheySimplex, 0.5, new Scalar(0, 240, 255), 1);
        }
    }

    private static void RenderColorDetections(
        Mat destination,
        Mat sourceColor,
        Mat motionMask,
        Mat hsv,
        IReadOnlyList<Rect> movingRects,
        IReadOnlyList<TrainDetectionProfile> trains,
        int minColorPixels)
    {
        foreach (var rect in movingRects)
        {
            using var colorRoi = new Mat(sourceColor, rect);
            using var motionRoi = new Mat(motionMask, rect);
            Cv2.CvtColor(colorRoi, hsv, ColorConversionCodes.BGR2HSV);

            var train = ClassifyDominantTrain(hsv, motionRoi, trains, minColorPixels);
            var label = train?.TrainName ?? "Unknown";
            var color = train?.OverlayColor ?? new Scalar(180, 180, 180);

            Cv2.Rectangle(destination, rect, color, 2);
            Cv2.PutText(destination, label, new Point(rect.X, Math.Max(16, rect.Y - 4)), HersheyFonts.HersheySimplex, 0.55, color, 2);
        }
    }

    private static TrainDetectionProfile? ClassifyDominantTrain(
        Mat hsvRoi,
        Mat motionRoiMask,
        IReadOnlyList<TrainDetectionProfile> trains,
        int minColorPixels)
    {
        if (trains.Count == 0)
        {
            return null;
        }

        var bestCount = 0;
        TrainDetectionProfile? bestTrain = null;

        foreach (var train in trains)
        {
            using var mask = BuildMask(hsvRoi, train.Calibration);
            Cv2.BitwiseAnd(mask, motionRoiMask, mask);

            var count = Cv2.CountNonZero(mask);
            if (count > bestCount)
            {
                bestCount = count;
                bestTrain = train;
            }
        }

        if (bestTrain is null || bestCount < minColorPixels)
        {
            return null;
        }

        return bestTrain;
    }

    private static Mat BuildMask(Mat hsv, ColorCalibrationProfile profile)
    {
        if (profile.HueLower <= profile.HueUpper)
        {
            var mask = new Mat();
            Cv2.InRange(hsv, new Scalar(profile.HueLower, profile.SaturationLower, profile.ValueLower), new Scalar(profile.HueUpper, profile.SaturationUpper, profile.ValueUpper), mask);
            return mask;
        }

        var primary = new Mat();
        var secondary = new Mat();
        var combined = new Mat();

        Cv2.InRange(hsv, new Scalar(profile.HueLower, profile.SaturationLower, profile.ValueLower), new Scalar(180, profile.SaturationUpper, profile.ValueUpper), primary);
        Cv2.InRange(hsv, new Scalar(0, profile.SaturationLower, profile.ValueLower), new Scalar(profile.HueUpper, profile.SaturationUpper, profile.ValueUpper), secondary);
        Cv2.BitwiseOr(primary, secondary, combined);

        primary.Dispose();
        secondary.Dispose();
        return combined;
    }

    private static int? FindBestTrackMatch(
        Point2f center,
        IReadOnlyDictionary<int, MotionTrackState> previousTracks,
        IEnumerable<int> candidateIds,
        float maxDistancePixels)
    {
        int? bestId = null;
        var bestDistance = double.MaxValue;

        foreach (var id in candidateIds)
        {
            if (!previousTracks.TryGetValue(id, out var previous))
            {
                continue;
            }

            var dx = center.X - previous.Center.X;
            var dy = center.Y - previous.Center.Y;
            var distance = Math.Sqrt((dx * dx) + (dy * dy));
            if (distance > maxDistancePixels || distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestId = id;
        }

        return bestId;
    }

    private static Mat BuildMedianBackground(List<Mat> sampledFrames, Size processSize)
    {
        var pixelCount = processSize.Width * processSize.Height;
        var samples = sampledFrames.Select(ToByteArray).ToArray();
        var median = new byte[pixelCount];
        var values = new byte[samples.Length];

        for (var pixel = 0; pixel < pixelCount; pixel++)
        {
            for (var i = 0; i < samples.Length; i++)
            {
                values[i] = samples[i][pixel];
            }

            Array.Sort(values);
            median[pixel] = values[values.Length / 2];
        }

        foreach (var mat in sampledFrames)
        {
            mat.Dispose();
        }

        var medianMat = new Mat(processSize.Height, processSize.Width, MatType.CV_8UC1);
        medianMat.SetArray(median);
        return medianMat;
    }

    private static Size BuildProcessSize(int sourceWidth, int sourceHeight, int maxWidth)
    {
        if (sourceWidth <= maxWidth)
        {
            return new Size(sourceWidth, sourceHeight);
        }

        var scale = (double)maxWidth / sourceWidth;
        var targetHeight = Math.Max(1, (int)Math.Round(sourceHeight * scale));
        return new Size(maxWidth, targetHeight);
    }

    private static byte[] ToByteArray(Mat mat)
    {
        var bytes = new byte[mat.Rows * mat.Cols];
        mat.GetArray(out byte[] raw);
        Buffer.BlockCopy(raw, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    internal readonly struct VideoProcessResult
    {
        private VideoProcessResult(bool success, string message, bool stoppedEarly)
        {
            Success = success;
            Message = message;
            StoppedEarly = stoppedEarly;
        }

        public bool Success { get; }

        public string Message { get; }

        public bool StoppedEarly { get; }

        public static VideoProcessResult Ok() => new(true, string.Empty, false);

        public static VideoProcessResult Stopped() => new(true, string.Empty, true);

        public static VideoProcessResult Fail(string message) => new(false, message, false);
    }

    internal readonly record struct ProcessingOptions(
        int ProcessMaxWidth,
        int MinMotionArea,
        int MinColorPixels,
        int MorphKernelSize,
        IReadOnlyList<TrainDetectionProfile> Trains)
    {
        public static ProcessingOptions Default => new(640, 220, 40, 3, TrainDetectionProfile.FromConfiguredTrains(TrainStore.CreateDefaultTrains()));
    }

    internal readonly record struct LiveTuning(
        int Threshold,
        int MinMotionArea,
        int MinColorPixels,
        int MorphKernelSize);

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct MotionTrackState(Point2f Center, Rect Rect, double TimestampSec);

    private readonly record struct UnifiedBackgroundSample(Mat MedianBackground, long LastFrameVersion);

    internal readonly struct PreviewFrameSet
    {
        public PreviewFrameSet(byte[] movingColorJpeg, byte[] colorDetectionJpeg)
        {
            MovingColorJpeg = movingColorJpeg;
            ColorDetectionJpeg = colorDetectionJpeg;
        }

        public byte[] MovingColorJpeg { get; }

        public byte[] ColorDetectionJpeg { get; }
    }

    private static double ComputeMaskCoverage(Mat mask)
    {
        var total = mask.Rows * mask.Cols;
        if (total <= 0)
        {
            return 0;
        }

        return (double)Cv2.CountNonZero(mask) / total;
    }

    private static MotionMaskRefiner.Options BuildRefinerOptions(int morphKernelSize)
    {
        var closeKernelSize = Math.Max(1, morphKernelSize);
        var openKernelSize = closeKernelSize >= 5 ? 3 : 1;
        return new MotionMaskRefiner.Options(closeKernelSize, openKernelSize);
    }

    private static List<TrainDetectionProfile?> ClassifyTrainsPerRect(
        Mat colorResized,
        Mat motionMask,
        Mat hsv,
        IReadOnlyList<Rect> movingRects,
        IReadOnlyList<TrainDetectionProfile> trains,
        int minColorPixels)
    {
        var matches = new List<TrainDetectionProfile?>(movingRects.Count);

        foreach (var rect in movingRects)
        {
            using var colorRoi = new Mat(colorResized, rect);
            using var motionRoi = new Mat(motionMask, rect);
            Cv2.CvtColor(colorRoi, hsv, ColorConversionCodes.BGR2HSV);

            matches.Add(ClassifyDominantTrain(hsv, motionRoi, trains, minColorPixels));
        }

        return matches;
    }

    internal static IReadOnlyList<TrainDetectionCandidate> BuildTrainDetectionCandidates(
        IReadOnlyList<Rect> movingRects,
        IReadOnlyList<TrainDetectionProfile?> trainMatches)
    {
        var candidates = new List<TrainDetectionCandidate>();
        var emittedTrainIds = new HashSet<Guid>();

        foreach (var (rect, train) in movingRects.Zip(trainMatches))
        {
            if (train is null
                || rect.Width > train.Value.Train.MaxWidth
                || rect.Height > train.Value.Train.MaxHeight
                || !emittedTrainIds.Add(train.Value.TrainId))
            {
                continue;
            }

            candidates.Add(new TrainDetectionCandidate(rect, train.Value));
        }

        return candidates;
    }

    private static int GetNextTrackId(
        Dictionary<int, MotionTrackState> previousTracks,
        ref int nextTrackId,
        Point2f center)
    {
        var availablePreviousIds = new HashSet<int>(previousTracks.Keys);
        var matchedId = FindBestTrackMatch(center, previousTracks, availablePreviousIds, maxDistancePixels: 80f);

        if (matchedId is null)
        {
            return nextTrackId++;
        }

        return matchedId.Value;
    }

    private static PreviewFrameSet BuildPreviewFrameSet(Mat movingColor, Mat colorDetections)
    {
        Cv2.ImEncode(".jpg", movingColor, out var movingColorJpeg, new[] { (int)ImwriteFlags.JpegQuality, 75 });
        Cv2.ImEncode(".jpg", colorDetections, out var colorDetectionJpeg, new[] { (int)ImwriteFlags.JpegQuality, 75 });

        return new PreviewFrameSet(movingColorJpeg, colorDetectionJpeg);
    }

    internal readonly record struct TrainDetectionCandidate(Rect Rect, TrainDetectionProfile Train);
}
