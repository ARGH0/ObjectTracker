using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ObjectTracker.Vision;
using OpenCvSharp;

namespace ObjectTracker.UI.Desktop;

internal sealed class BackgroundEstimationEngine
{
    private readonly SessionCalibrationService _sessionCalibration = new();

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
        var frameWidth = (int)capture.FrameWidth;
        var frameHeight = (int)capture.FrameHeight;
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
        return await ProcessCaptureFramesAsync(
            capture,
            Path.GetFileName(videoPath),
            fps,
            medianBackground,
            threshold,
            options,
            onFrame,
            onStatus,
            getLiveTuning,
            shouldStopEarly,
            cancellationToken,
            pacePlayback: true);
    }

    public async Task<VideoProcessResult> ProcessUsbCameraAsync(
        int cameraIndex,
        VideoCaptureAPIs api,
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
        using var capture = new VideoCapture(cameraIndex, api);
        capture.Set(VideoCaptureProperties.FrameWidth, 640);
        capture.Set(VideoCaptureProperties.FrameHeight, 480);
        capture.Set(VideoCaptureProperties.Fps, 20);
        capture.Set(VideoCaptureProperties.BufferSize, 1);

        if (!capture.IsOpened())
        {
            return VideoProcessResult.Fail($"Unable to open USB camera {cameraIndex}.");
        }

        var frameWidth = (int)Math.Max(0, capture.Get(VideoCaptureProperties.FrameWidth));
        var frameHeight = (int)Math.Max(0, capture.Get(VideoCaptureProperties.FrameHeight));
        if (frameWidth <= 0 || frameHeight <= 0)
        {
            using var probeFrame = new Mat();
            if (!capture.Read(probeFrame) || probeFrame.Empty())
            {
                return VideoProcessResult.Fail($"USB camera {cameraIndex} did not return frames.");
            }

            frameWidth = probeFrame.Width;
            frameHeight = probeFrame.Height;
        }

        var processSize = BuildProcessSize(frameWidth, frameHeight, options.ProcessMaxWidth);
        using var medianBackground = await CreateMedianBackgroundForUsbCameraAsync(
            capture,
            sourceLabel,
            sampleCount,
            processSize,
            bakeImagePath,
            onStatus,
            cancellationToken);

        return await ProcessCaptureFramesAsync(
            capture,
            sourceLabel,
            capture.Fps,
            medianBackground,
            threshold,
            options,
            onFrame,
            onStatus,
            getLiveTuning,
            shouldStopEarly,
            cancellationToken,
            pacePlayback: false);
    }

    private static async Task<VideoProcessResult> ProcessCaptureFramesAsync(
        VideoCapture capture,
        string sourceLabel,
        double fps,
        Mat medianBackground,
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
        using var cleanMask = new Mat();
        var activeMorphKernelSize = options.MorphKernelSize;
        var morphologyKernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(activeMorphKernelSize, activeMorphKernelSize));
        using var movingColor = new Mat();
        using var colorDetections = new Mat();
        using var motionView = new Mat();
        using var hsv = new Mat();

        var previousTracks = new Dictionary<int, MotionTrackState>();
        var nextTrackId = 1;
        try
        {
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

                    if (live.MorphKernelSize != activeMorphKernelSize)
                    {
                        morphologyKernel.Dispose();
                        activeMorphKernelSize = live.MorphKernelSize;
                        morphologyKernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(activeMorphKernelSize, activeMorphKernelSize));
                    }
                }

                Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
                Cv2.Resize(frame, colorResized, processSize, interpolation: InterpolationFlags.Area);
                Cv2.Resize(gray, resized, processSize, interpolation: InterpolationFlags.Area);
                Cv2.Absdiff(medianBackground, resized, diff);
                Cv2.Threshold(diff, mask, activeThreshold, 255, ThresholdTypes.Binary);
                Cv2.MorphologyEx(mask, cleanMask, MorphTypes.Open, morphologyKernel);
                Cv2.MorphologyEx(cleanMask, cleanMask, MorphTypes.Close, morphologyKernel);

                var movingRects = GetMovingObjectRectangles(cleanMask, activeMinMotionArea);

                movingColor.SetTo(Scalar.Black);
                colorResized.CopyTo(movingColor, cleanMask);
                DrawMovingObjectBoxes(movingColor, movingRects);

                colorResized.CopyTo(colorDetections);
                RenderColorDetections(colorDetections, colorResized, cleanMask, hsv, movingRects, activeMinColorPixels);

                colorResized.CopyTo(motionView);
                var timestampSec = capture.PosMsec / 1000.0;
                RenderMotionOverlay(motionView, movingRects, timestampSec, ref previousTracks, ref nextTrackId);

                var preview = BuildPreviewFrameSet(cleanMask, movingColor, colorDetections, motionView);
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
        }
        finally
        {
            morphologyKernel.Dispose();
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

    private static async Task<Mat> CreateMedianBackgroundForUsbCameraAsync(
        VideoCapture capture,
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
            return LoadBackgroundImageMat(bakeImagePath, processSize);
        }

        var samplingCount = Math.Max(5, sampleCount);
        var progressStep = Math.Max(1, samplingCount / 5);

        await onStatus($"estimating background for {sourceLabel}...");
        var medianBackground = EstimateMedianBackgroundFromLiveCapture(
            capture,
            samplingCount,
            processSize,
            cancellationToken,
            reportProgress: (completed, total) =>
            {
                if (completed % progressStep != 0 && completed != total)
                {
                    return;
                }

                onStatus($"sampling {sourceLabel}: frame {completed}/{total}").GetAwaiter().GetResult();
            });

        await onStatus($"live capture ready: {sourceLabel}");
        return medianBackground;
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
        int minColorPixels)
    {
        foreach (var rect in movingRects)
        {
            using var colorRoi = new Mat(sourceColor, rect);
            using var motionRoi = new Mat(motionMask, rect);
            Cv2.CvtColor(colorRoi, hsv, ColorConversionCodes.BGR2HSV);

            var (label, color) = ClassifyDominantColor(hsv, motionRoi, minColorPixels);

            Cv2.Rectangle(destination, rect, color, 2);
            Cv2.PutText(destination, label, new Point(rect.X, Math.Max(16, rect.Y - 4)), HersheyFonts.HersheySimplex, 0.55, color, 2);
        }
    }

    private static (string Label, Scalar Color) ClassifyDominantColor(Mat hsvRoi, Mat motionRoiMask, int minColorPixels)
    {
        using var redMask1 = new Mat();
        using var redMask2 = new Mat();
        using var redMask = new Mat();
        using var greenMask = new Mat();
        using var blueMask = new Mat();
        using var yellowMask = new Mat();

        Cv2.InRange(hsvRoi, new Scalar(0, 90, 70), new Scalar(10, 255, 255), redMask1);
        Cv2.InRange(hsvRoi, new Scalar(170, 90, 70), new Scalar(180, 255, 255), redMask2);
        Cv2.BitwiseOr(redMask1, redMask2, redMask);

        Cv2.InRange(hsvRoi, new Scalar(40, 70, 60), new Scalar(85, 255, 255), greenMask);
        Cv2.InRange(hsvRoi, new Scalar(95, 90, 70), new Scalar(130, 255, 255), blueMask);
        Cv2.InRange(hsvRoi, new Scalar(15, 90, 80), new Scalar(38, 255, 255), yellowMask);

        // Restrict color voting to pixels that are currently moving.
        Cv2.BitwiseAnd(redMask, motionRoiMask, redMask);
        Cv2.BitwiseAnd(greenMask, motionRoiMask, greenMask);
        Cv2.BitwiseAnd(blueMask, motionRoiMask, blueMask);
        Cv2.BitwiseAnd(yellowMask, motionRoiMask, yellowMask);

        var red = Cv2.CountNonZero(redMask);
        var green = Cv2.CountNonZero(greenMask);
        var blue = Cv2.CountNonZero(blueMask);
        var yellow = Cv2.CountNonZero(yellowMask);

        var best = Math.Max(Math.Max(red, green), Math.Max(blue, yellow));
        if (best < minColorPixels)
        {
            return ("Unknown", new Scalar(180, 180, 180));
        }

        if (best == red)
        {
            return ("Red", new Scalar(60, 60, 255));
        }

        if (best == green)
        {
            return ("Green", new Scalar(60, 220, 60));
        }

        if (best == blue)
        {
            return ("Blue", new Scalar(255, 120, 50));
        }

        return ("Yellow", new Scalar(40, 220, 240));
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

    private static Mat EstimateMedianBackground(
        VideoCapture capture,
        int frameCount,
        int sampleCount,
        Size processSize,
        CancellationToken cancellationToken,
        Action<int, int>? reportProgress = null)
    {
        var validFrameCount = Math.Max(1, frameCount);
        var ids = Enumerable.Range(0, sampleCount)
            .Select(_ => Random.Shared.Next(0, validFrameCount))
            .ToArray();

        var sampledFrames = new List<Mat>(sampleCount);
        using var sampledFrame = new Mat();
        using var sampledGray = new Mat();

        var completedSamples = 0;
        foreach (var frameId in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();

            capture.PosFrames = frameId;
            if (!capture.Read(sampledFrame) || sampledFrame.Empty())
            {
                continue;
            }

            Cv2.CvtColor(sampledFrame, sampledGray, ColorConversionCodes.BGR2GRAY);
            var resized = new Mat();
            Cv2.Resize(sampledGray, resized, processSize, interpolation: InterpolationFlags.Area);
            sampledFrames.Add(resized);

            completedSamples++;
            reportProgress?.Invoke(completedSamples, ids.Length);
        }

        if (sampledFrames.Count == 0)
        {
            throw new InvalidOperationException("No frames available to estimate background.");
        }

        return BuildMedianBackground(sampledFrames, processSize);
    }

    private static Mat EstimateMedianBackgroundFromLiveCapture(
        VideoCapture capture,
        int sampleCount,
        Size processSize,
        CancellationToken cancellationToken,
        Action<int, int>? reportProgress = null)
    {
        var sampledFrames = new List<Mat>(sampleCount);
        using var sampledFrame = new Mat();
        using var sampledGray = new Mat();

        var attempts = 0;
        var maxAttempts = Math.Max(sampleCount * 4, 20);
        while (sampledFrames.Count < sampleCount && attempts < maxAttempts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            attempts++;

            if (!capture.Read(sampledFrame) || sampledFrame.Empty())
            {
                continue;
            }

            Cv2.CvtColor(sampledFrame, sampledGray, ColorConversionCodes.BGR2GRAY);
            var resized = new Mat();
            Cv2.Resize(sampledGray, resized, processSize, interpolation: InterpolationFlags.Area);
            sampledFrames.Add(resized);
            reportProgress?.Invoke(sampledFrames.Count, sampleCount);
        }

        if (sampledFrames.Count == 0)
        {
            throw new InvalidOperationException("No live frames available to estimate background.");
        }

        return BuildMedianBackground(sampledFrames, processSize);
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
        int MorphKernelSize)
    {
        public static ProcessingOptions Default => new(640, 220, 40, 3);
    }

    internal readonly record struct LiveTuning(
        int Threshold,
        int MinMotionArea,
        int MinColorPixels,
        int MorphKernelSize);

    private readonly record struct MotionTrackState(Point2f Center, Rect Rect, double TimestampSec);

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

            await Task.Delay(slice, cancellationToken);
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