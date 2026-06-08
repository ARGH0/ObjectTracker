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

    public async Task<VideoProcessResult> ProcessVideoAsync(
        string videoPath,
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
        if (!File.Exists(videoPath))
        {
            return VideoProcessResult.Fail($"Video file not found: {videoPath}");
        }

        using var capture = new VideoCapture(videoPath);
        if (!capture.IsOpened())
        {
            return VideoProcessResult.Fail($"Unable to open video: {Path.GetFileName(videoPath)}");
        }

        var fps = capture.Fps;
        var frameCount = (int)Math.Max(0, capture.Get(VideoCaptureProperties.FrameCount));
        var frameWidth = capture.FrameWidth;
        var frameHeight = capture.FrameHeight;
        if (frameWidth <= 0 || frameHeight <= 0)
        {
            return VideoProcessResult.Fail("Video has invalid dimensions.");
        }

        var processSize = BuildProcessSize(frameWidth, frameHeight, options.ProcessMaxWidth);
        var bakedPath = await EnsureBakedBackgroundAsync(videoPath, sampleCount, options, bakeImagePath, cancellationToken, onStatus);

        using var medianBackground = Cv2.ImRead(bakedPath, ImreadModes.Grayscale);
        if (medianBackground.Empty())
        {
            return VideoProcessResult.Fail("Unable to load baked background.");
        }

        if (medianBackground.Size() != processSize)
        {
            return VideoProcessResult.Fail("Baked background dimensions do not match video processing dimensions.");
        }

        await onStatus($"using baked background: {Path.GetFileName(bakedPath)}");

        capture.PosFrames = 0;
        using var railRoiMask = _railRoiMaskBuilder.BuildFromBackground(medianBackground);
        return await ProcessCaptureFramesAsync(
            capture,
            Path.GetFileName(videoPath),
            fps,
            medianBackground,
            railRoiMask,
            threshold,
            options,
            onFrame,
            onStatus,
            getLiveTuning,
            shouldStopEarly,
            cancellationToken,
            pacePlayback: true);
    }

    public async Task<VideoProcessResult> ProcessUsbCameraSourceAsync(
        UsbCameraOwnerManager ownerManager,
        UsbCameraKey key,
        UsbCaptureSettings startupSettings,
        string sourceLabel,
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
        await using var lease = await ownerManager.AcquireAsync(key, startupSettings, cancellationToken);
        var previousVersion = 0L;
        var firstSnapshot = await lease.WaitForNextFrameAsync(previousVersion, TimeSpan.FromSeconds(1), cancellationToken);
        if (firstSnapshot is null)
        {
            return VideoProcessResult.Fail($"USB camera {key.CameraIndex} did not return frames.");
        }

        previousVersion = firstSnapshot.Value.FrameVersion;
        var processSize = BuildProcessSize(firstSnapshot.Value.Width, firstSnapshot.Value.Height, options.ProcessMaxWidth);
        var background = await CreateMedianBackgroundForUsbCameraSourceAsync(
            lease,
            firstSnapshot.Value,
            sourceLabel,
            sampleCount,
            processSize,
            bakeImagePath,
            onStatus,
            cancellationToken);
        previousVersion = background.LastFrameVersion;

        using var medianBackground = background.MedianBackground;
        using var railRoiMask = _railRoiMaskBuilder.BuildFromBackground(medianBackground);

        return await ProcessUsbCameraSourceFramesAsync(
            lease,
            previousVersion,
            sourceLabel,
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

    private async Task<VideoProcessResult> ProcessCaptureFramesAsync(
        VideoCapture capture,
        string sourceLabel,
        double fps,
        Mat medianBackground,
        Mat railRoiMask,
        int threshold,
        ProcessingOptions options,
        Func<PreviewFrameSet, Task> onFrame,
        Func<string, Task> onStatus,
        Func<LiveTuning>? getLiveTuning,
        Func<bool>? shouldStopEarly,
        CancellationToken cancellationToken,
        bool pacePlayback)
    {
        var frameIndex = 0;
        var pacingFps = GetPacingFps(fps);
        var playbackClock = Stopwatch.StartNew();
        var processSize = medianBackground.Size();
        var roiCoverage = ComputeMaskCoverage(railRoiMask);

        await onStatus($"rail ROI active: {roiCoverage * 100:0.0}% of frame");

        if (pacePlayback)
        {
            await onStatus($"playback pacing: {pacingFps:0.0} fps");
        }

        using var frame = new Mat();
        using var gray = new Mat();
        using var colorResized = new Mat();
        using var resized = new Mat();
        using var diff = new Mat();
        using var mask = new Mat();
        using var refinedMask = new Mat();
        var activeMorphKernelSize = options.MorphKernelSize;
        using var movingColor = new Mat();
        using var colorDetections = new Mat();
        using var motionView = new Mat();
        using var hsv = new Mat();
        var activeColorCalibrations = options.ColorCalibrations;

        var previousTracks = new Dictionary<int, MotionTrackState>();
        var nextTrackId = 1;
        while (!cancellationToken.IsCancellationRequested)
        {
            if (shouldStopEarly?.Invoke() == true)
            {
                return VideoProcessResult.Stopped();
            }

            if (!capture.Read(frame) || frame.Empty())
            {
                break;
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
                activeColorCalibrations = live.ColorCalibrations;
                activeMorphKernelSize = live.MorphKernelSize;
            }

            Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.Resize(frame, colorResized, processSize, interpolation: InterpolationFlags.Area);
            Cv2.Resize(gray, resized, processSize, interpolation: InterpolationFlags.Area);
            Cv2.Absdiff(medianBackground, resized, diff);
            Cv2.Threshold(diff, mask, activeThreshold, 255, ThresholdTypes.Binary);
            Cv2.BitwiseAnd(mask, railRoiMask, mask);
            using var refined = _motionMaskRefiner.Refine(mask, BuildRefinerOptions(activeMorphKernelSize));
            refined.CopyTo(refinedMask);

            var movingRects = GetMovingObjectRectangles(refinedMask, activeMinMotionArea);

            movingColor.SetTo(Scalar.Black);
            colorResized.CopyTo(movingColor, refinedMask);
            DrawMovingObjectBoxes(movingColor, movingRects);

            colorResized.CopyTo(colorDetections);
            RenderColorDetections(colorDetections, colorResized, refinedMask, hsv, movingRects, activeColorCalibrations, activeMinColorPixels);

            colorResized.CopyTo(motionView);
            var timestampSec = capture.PosMsec / 1000.0;
            RenderMotionOverlay(motionView, movingRects, timestampSec, ref previousTracks, ref nextTrackId);

            var preview = BuildPreviewFrameSet(refinedMask, movingColor, colorDetections, motionView);
            await onFrame(preview);

            frameIndex++;
            if (frameIndex % 20 == 0)
            {
                var fpsText = fps > 0 ? $"{fps:0.0}" : "n/a";
                if (pacePlayback)
                {
                    var positionMs = capture.PosMsec;
                    await onStatus($"processing {sourceLabel} | frame {frameIndex} | source fps {fpsText} | t={positionMs / 1000:0.0}s");
                }
                else
                {
                    await onStatus($"processing {sourceLabel} | frame {frameIndex} | source fps {fpsText}");
                }
            }

            if (pacePlayback)
            {
                await WaitForPlaybackScheduleAsync(
                    frameIndex,
                    pacingFps,
                    playbackClock,
                    shouldStopEarly,
                    cancellationToken);
            }
        }

        return VideoProcessResult.Ok();
    }

    public Task PreBakeBackgroundAsync(string videoPath, int sampleCount, CancellationToken cancellationToken)
    {
        return _sessionCalibration.PreBakeBackgroundAsync(videoPath, sampleCount, ProcessingOptions.Default.ProcessMaxWidth, cancellationToken);
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

    public Task PreBakeBackgroundAsync(string videoPath, int sampleCount, ProcessingOptions options, CancellationToken cancellationToken)
    {
        return _sessionCalibration.PreBakeBackgroundAsync(videoPath, sampleCount, options.ProcessMaxWidth, cancellationToken);
    }

    private static async Task<UsbBackgroundSample> CreateMedianBackgroundForUsbCameraSourceAsync(
        UsbCameraLease lease,
        UsbFrameSnapshot firstSnapshot,
        string sourceLabel,
        int sampleCount,
        Size processSize,
        string? bakeImagePath,
        Func<string, Task> onStatus,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(bakeImagePath))
        {
            await onStatus($"using bake image {Path.GetFileName(bakeImagePath)} for {sourceLabel}");
            return new UsbBackgroundSample(LoadBackgroundImageMat(bakeImagePath, processSize), firstSnapshot.FrameVersion);
        }

        var samplingCount = Math.Max(5, sampleCount);
        var progressStep = Math.Max(1, samplingCount / 5);
        var sampledFrames = new List<Mat>(samplingCount);
        var previousVersion = firstSnapshot.FrameVersion;

        await onStatus($"estimating background for {sourceLabel}...");
        AddSnapshotSample(firstSnapshot, processSize, sampledFrames);
        await ReportUsbSamplingProgressAsync(sourceLabel, sampledFrames.Count, samplingCount, progressStep, onStatus);

        while (sampledFrames.Count < samplingCount && !cancellationToken.IsCancellationRequested)
        {
            var snapshot = await lease.WaitForNextFrameAsync(previousVersion, TimeSpan.FromSeconds(1), cancellationToken);
            if (snapshot is null)
            {
                continue;
            }

            previousVersion = snapshot.Value.FrameVersion;
            AddSnapshotSample(snapshot.Value, processSize, sampledFrames);
            await ReportUsbSamplingProgressAsync(sourceLabel, sampledFrames.Count, samplingCount, progressStep, onStatus);
        }

        if (sampledFrames.Count == 0)
        {
            throw new InvalidOperationException("No live frames available to estimate background.");
        }

        await onStatus($"live capture ready: {sourceLabel}");
        return new UsbBackgroundSample(BuildMedianBackground(sampledFrames, processSize), previousVersion);
    }

    private async Task<VideoProcessResult> ProcessUsbCameraSourceFramesAsync(
        UsbCameraLease lease,
        long previousVersion,
        string sourceLabel,
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
        var processSize = medianBackground.Size();
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
        using var motionView = new Mat();
        using var hsv = new Mat();
        var activeColorCalibrations = options.ColorCalibrations;

        var previousTracks = new Dictionary<int, MotionTrackState>();
        var nextTrackId = 1;
        while (!cancellationToken.IsCancellationRequested)
        {
            if (shouldStopEarly?.Invoke() == true)
            {
                return VideoProcessResult.Stopped();
            }

            var snapshot = await lease.WaitForNextFrameAsync(previousVersion, TimeSpan.FromSeconds(1), cancellationToken);
            if (snapshot is null)
            {
                continue;
            }

            previousVersion = snapshot.Value.FrameVersion;
            using var frame = DecodeUsbSnapshot(snapshot.Value);
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
                activeColorCalibrations = live.ColorCalibrations;
                activeMorphKernelSize = live.MorphKernelSize;
            }

            Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.Resize(frame, colorResized, processSize, interpolation: InterpolationFlags.Area);
            Cv2.Resize(gray, resized, processSize, interpolation: InterpolationFlags.Area);
            Cv2.Absdiff(medianBackground, resized, diff);
            Cv2.Threshold(diff, mask, activeThreshold, 255, ThresholdTypes.Binary);
            Cv2.BitwiseAnd(mask, railRoiMask, mask);
            using var refined = _motionMaskRefiner.Refine(mask, BuildRefinerOptions(activeMorphKernelSize));
            refined.CopyTo(refinedMask);

            var movingRects = GetMovingObjectRectangles(refinedMask, activeMinMotionArea);

            movingColor.SetTo(Scalar.Black);
            colorResized.CopyTo(movingColor, refinedMask);
            DrawMovingObjectBoxes(movingColor, movingRects);

            colorResized.CopyTo(colorDetections);
            RenderColorDetections(colorDetections, colorResized, refinedMask, hsv, movingRects, activeColorCalibrations, activeMinColorPixels);

            colorResized.CopyTo(motionView);
            RenderMotionOverlay(motionView, movingRects, snapshot.Value.TimestampUtcMs / 1000.0, ref previousTracks, ref nextTrackId);

            var preview = BuildPreviewFrameSet(refinedMask, movingColor, colorDetections, motionView);
            await onFrame(preview);

            frameIndex++;
            if (frameIndex % 20 == 0)
            {
                await onStatus($"processing {sourceLabel} | frame {frameIndex} | source fps n/a");
            }
        }

        return VideoProcessResult.Ok();
    }

    private static async Task ReportUsbSamplingProgressAsync(string sourceLabel, int completed, int total, int progressStep, Func<string, Task> onStatus)
    {
        if (completed % progressStep == 0 || completed == total)
        {
            await onStatus($"sampling {sourceLabel}: frame {completed}/{total}");
        }
    }

    private static void AddSnapshotSample(UsbFrameSnapshot snapshot, Size processSize, List<Mat> sampledFrames)
    {
        using var frame = DecodeUsbSnapshot(snapshot);
        using var gray = new Mat();
        Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
        var resized = new Mat();
        Cv2.Resize(gray, resized, processSize, interpolation: InterpolationFlags.Area);
        sampledFrames.Add(resized);
    }

    private static Mat DecodeUsbSnapshot(UsbFrameSnapshot snapshot)
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
        IReadOnlyList<ColorCalibrationProfile> colorCalibrations,
        int minColorPixels)
    {
        foreach (var rect in movingRects)
        {
            using var colorRoi = new Mat(sourceColor, rect);
            using var motionRoi = new Mat(motionMask, rect);
            Cv2.CvtColor(colorRoi, hsv, ColorConversionCodes.BGR2HSV);

            var (label, color) = ClassifyDominantColor(hsv, motionRoi, colorCalibrations, minColorPixels);

            Cv2.Rectangle(destination, rect, color, 2);
            Cv2.PutText(destination, label, new Point(rect.X, Math.Max(16, rect.Y - 4)), HersheyFonts.HersheySimplex, 0.55, color, 2);
        }
    }

    private static (string Label, Scalar Color) ClassifyDominantColor(
        Mat hsvRoi,
        Mat motionRoiMask,
        IReadOnlyList<ColorCalibrationProfile> colorCalibrations,
        int minColorPixels)
    {
        if (colorCalibrations.Count == 0)
        {
            return ("Unknown", new Scalar(180, 180, 180));
        }

        var bestCount = 0;
        ColorCalibrationProfile? bestProfile = null;

        foreach (var profile in colorCalibrations)
        {
            using var mask = BuildMask(hsvRoi, profile);
            Cv2.BitwiseAnd(mask, motionRoiMask, mask);

            var count = Cv2.CountNonZero(mask);
            if (count > bestCount)
            {
                bestCount = count;
                bestProfile = profile;
            }
        }

        if (bestProfile is null || bestCount < minColorPixels)
        {
            return ("Unknown", new Scalar(180, 180, 180));
        }

        return (bestProfile.Value.Name, GetOverlayColor(bestProfile.Value.Name));
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

    private static Scalar GetOverlayColor(string name)
    {
        return name.ToUpperInvariant() switch
        {
            "RED" => new Scalar(60, 60, 255),
            "GREEN" => new Scalar(60, 220, 60),
            "BLUE" => new Scalar(255, 120, 50),
            "YELLOW" => new Scalar(40, 220, 240),
            "WHITE" => new Scalar(255, 255, 255),
            _ => new Scalar(180, 180, 180)
        };
    }

    private static void RenderMotionOverlay(
        Mat destination,
        IReadOnlyList<Rect> movingRects,
        double timestampSec,
        ref Dictionary<int, MotionTrackState> previousTracks,
        ref int nextTrackId)
    {
        var currentTracks = new Dictionary<int, MotionTrackState>();
        var availablePreviousIds = new HashSet<int>(previousTracks.Keys);

        foreach (var rect in movingRects)
        {
            var center = new Point2f(rect.X + (rect.Width / 2f), rect.Y + (rect.Height / 2f));
            var matchedId = FindBestTrackMatch(center, previousTracks, availablePreviousIds, maxDistancePixels: 80f);

            if (matchedId is null)
            {
                matchedId = nextTrackId++;
            }
            else
            {
                availablePreviousIds.Remove(matchedId.Value);
            }

            currentTracks[matchedId.Value] = new MotionTrackState(center, rect, timestampSec);
        }

        foreach (var (id, current) in currentTracks)
        {
            Cv2.Rectangle(destination, current.Rect, new Scalar(0, 255, 255), 2);
            Cv2.Circle(destination, (Point)current.Center, 5, new Scalar(255, 255, 0), -1);

            var speed = 0.0;
            var directionDeg = 0.0;
            var hasHistory = previousTracks.TryGetValue(id, out var previous);

            if (hasHistory)
            {
                var deltaTime = Math.Max(0.0001, current.TimestampSec - previous.TimestampSec);
                var dx = current.Center.X - previous.Center.X;
                var dy = current.Center.Y - previous.Center.Y;
                speed = Math.Sqrt((dx * dx) + (dy * dy)) / deltaTime;
                directionDeg = Math.Atan2(dy, dx) * 180.0 / Math.PI;

                Cv2.ArrowedLine(destination, (Point)previous.Center, (Point)current.Center, new Scalar(0, 255, 255), 2, LineTypes.Link8, 0, 0.2);
            }

            var infoText = hasHistory
                ? $"#{id} {speed:0.0}px/s {directionDeg:0.0}deg"
                : $"#{id} acquiring";

            Cv2.PutText(
                destination,
                infoText,
                new Point(current.Rect.X, Math.Max(16, current.Rect.Y - 6)),
                HersheyFonts.HersheySimplex,
                0.48,
                new Scalar(255, 255, 255),
                2);
        }

        previousTracks = currentTracks;

        if (currentTracks.Count == 0)
        {
            Cv2.PutText(destination, "No moving objects", new Point(10, 24), HersheyFonts.HersheySimplex, 0.55, new Scalar(255, 255, 255), 2);
        }
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
        IReadOnlyList<ColorCalibrationProfile> ColorCalibrations)
    {
        public static ProcessingOptions Default => new(640, 220, 40, 3, MainWindow.CreateDefaultColorCalibrations());
    }

    internal readonly record struct LiveTuning(
        int Threshold,
        int MinMotionArea,
        int MinColorPixels,
        int MorphKernelSize,
        IReadOnlyList<ColorCalibrationProfile> ColorCalibrations);

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct MotionTrackState(Point2f Center, Rect Rect, double TimestampSec);

    private readonly record struct UsbBackgroundSample(Mat MedianBackground, long LastFrameVersion);

    internal readonly struct PreviewFrameSet
    {
        public PreviewFrameSet(byte[] backgroundMaskJpeg, byte[] movingColorJpeg, byte[] colorDetectionJpeg, byte[] motionJpeg)
        {
            BackgroundMaskJpeg = backgroundMaskJpeg;
            MovingColorJpeg = movingColorJpeg;
            ColorDetectionJpeg = colorDetectionJpeg;
            MotionJpeg = motionJpeg;
        }

        public byte[] BackgroundMaskJpeg { get; }

        public byte[] MovingColorJpeg { get; }

        public byte[] ColorDetectionJpeg { get; }

        public byte[] MotionJpeg { get; }
    }

    private static double GetPacingFps(double sourceFps)
    {
        if (double.IsNaN(sourceFps) || double.IsInfinity(sourceFps) || sourceFps <= 0)
        {
            return 30.0;
        }

        return Math.Clamp(sourceFps, 1.0, 240.0);
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

    private static async Task WaitForPlaybackScheduleAsync(
        int frameIndex,
        double pacingFps,
        Stopwatch playbackClock,
        Func<bool>? shouldStopEarly,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var targetTicks = (long)Math.Round(frameIndex * TimeSpan.TicksPerSecond / pacingFps);
        var targetTime = new TimeSpan(targetTicks);
        while (true)
        {
            if (cancellationToken.IsCancellationRequested || (shouldStopEarly?.Invoke() == true))
            {
                return;
            }

            var remaining = targetTime - playbackClock.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                return;
            }

            var slice = remaining > TimeSpan.FromMilliseconds(20)
                ? TimeSpan.FromMilliseconds(20)
                : remaining;

            await Task.Delay(slice);
        }
    }

    private static PreviewFrameSet BuildPreviewFrameSet(Mat backgroundMask, Mat movingColor, Mat colorDetections, Mat motionView)
    {
        Cv2.ImEncode(".jpg", backgroundMask, out var backgroundMaskJpeg, new[] { (int)ImwriteFlags.JpegQuality, 80 });
        Cv2.ImEncode(".jpg", movingColor, out var movingColorJpeg, new[] { (int)ImwriteFlags.JpegQuality, 75 });
        Cv2.ImEncode(".jpg", colorDetections, out var colorDetectionJpeg, new[] { (int)ImwriteFlags.JpegQuality, 75 });
        Cv2.ImEncode(".jpg", motionView, out var motionJpeg, new[] { (int)ImwriteFlags.JpegQuality, 75 });

        return new PreviewFrameSet(backgroundMaskJpeg, movingColorJpeg, colorDetectionJpeg, motionJpeg);
    }
}
