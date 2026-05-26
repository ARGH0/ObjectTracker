using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace ObjectTracker.Vision;

public sealed class BackgroundEstimationEngine
{
    // COCO 80-class names used by YOLO11/v8 detection models
    private static readonly string[] CocoNames =
    {
        "person", "bicycle", "car", "motorcycle", "airplane", "bus", "train", "truck", "boat",
        "traffic light", "fire hydrant", "stop sign", "parking meter", "bench", "bird", "cat",
        "dog", "horse", "sheep", "cow", "elephant", "bear", "zebra", "giraffe", "backpack",
        "umbrella", "handbag", "tie", "suitcase", "frisbee", "skis", "snowboard", "sports ball",
        "kite", "baseball bat", "baseball glove", "skateboard", "surfboard", "tennis racket",
        "bottle", "wine glass", "cup", "fork", "knife", "spoon", "bowl", "banana", "apple",
        "sandwich", "orange", "broccoli", "carrot", "hot dog", "pizza", "donut", "cake",
        "chair", "couch", "potted plant", "bed", "dining table", "toilet", "tv", "laptop",
        "mouse", "remote", "keyboard", "cell phone", "microwave", "oven", "toaster", "sink",
        "refrigerator", "book", "clock", "vase", "scissors", "teddy bear", "hair drier", "toothbrush"
    };

    // Palette: one vivid BGR color per class (cycling if >80 classes)
    private static readonly Scalar[] ClassColors = GenerateClassColors();

    private static Scalar[] GenerateClassColors()
    {
        var palette = new[]
        {
            new Scalar(56, 220, 100), new Scalar(255, 100, 56), new Scalar(56, 100, 255),
            new Scalar(255, 200, 56), new Scalar(200, 56, 255), new Scalar(56, 255, 200),
            new Scalar(255, 56, 180), new Scalar(180, 255, 56), new Scalar(56, 180, 255),
            new Scalar(255, 150, 80), new Scalar(80, 255, 150), new Scalar(150, 80, 255),
        };
        var colors = new Scalar[80];
        for (var i = 0; i < 80; i++)
        {
            colors[i] = palette[i % palette.Length];
        }

        return colors;
    }

    private static Scalar ColorForClass(int classId) =>
        ClassColors[Math.Abs(classId) % ClassColors.Length];

    private static string NameForClass(int classId) =>
        classId >= 0 && classId < CocoNames.Length ? CocoNames[classId] : $"cls-{classId}";

    // Per-session YOLO cross-frame tracker (track id → last rect + class + missed frames)
    private readonly object _yoloTrackerSync = new();
    private readonly Dictionary<int, YoloTrackEntry> _yoloTracks = new();
    private int _yoloNextTrackId = 1;
    private const int YoloTrackMaxMissed = 8;
    private const float YoloTrackIoUThreshold = 0.3f;

    private sealed class YoloTrackEntry
    {
        public Rect Rect;
        public int ClassId;
        public int MissedFrames;
    }

    private readonly object _bakeSync = new();
    private readonly Dictionary<string, Task<string>> _bakeJobs = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _yoloSync = new();
    private InferenceSession? _yoloSession;
    private string? _yoloModelPath;
    private string _yoloStatus = "YOLO not initialized.";

    public bool ConfigureYoloModel(string? modelPath, out string message)
    {
        if (string.IsNullOrWhiteSpace(modelPath))
        {
            message = "no model file found";
            _yoloStatus = message;
            return false;
        }

        if (!File.Exists(modelPath))
        {
            message = $"model file not found: {modelPath}";
            _yoloStatus = message;
            return false;
        }

        try
        {
            var session = new InferenceSession(modelPath);
            lock (_yoloSync)
            {
                _yoloSession?.Dispose();
                _yoloSession = session;
                _yoloModelPath = Path.GetFullPath(modelPath);
            }

            message = _yoloModelPath!;
            _yoloStatus = $"model loaded: {_yoloModelPath}";

            // Reset tracker when model changes
            lock (_yoloTrackerSync)
            {
                _yoloTracks.Clear();
                _yoloNextTrackId = 1;
            }

            return true;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            _yoloStatus = $"failed to load model '{modelPath}': {ex.Message}";
            return false;
        }
    }

    public string GetYoloStatus()
    {
        lock (_yoloSync)
        {
            return _yoloStatus;
        }
    }

    public async Task<VideoProcessResult> ProcessVideoAsync(
        string videoPath,
        int sampleCount,
        int threshold,
        ProcessingOptions options,
        DetectionOptions detectionOptions,
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
            detectionOptions,
            onFrame,
            onStatus,
            getLiveTuning,
            shouldStopEarly,
            cancellationToken,
            pacePlayback: true);
    }

    public async Task<VideoProcessResult> ProcessUsbCameraAsync(
        int cameraIndex,
        string apiId,
        string sourceLabel,
        int sampleCount,
        int threshold,
        ProcessingOptions options,
        DetectionOptions detectionOptions,
        string? bakeImagePath,
        Func<PreviewFrameSet, Task> onFrame,
        Func<string, Task> onStatus,
        Func<LiveTuning>? getLiveTuning,
        Func<bool>? shouldStopEarly,
        CancellationToken cancellationToken)
    {
        var api = ResolveCaptureApi(apiId);
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
            detectionOptions,
            onFrame,
            onStatus,
            getLiveTuning,
            shouldStopEarly,
            cancellationToken,
            pacePlayback: false);
    }

    private static VideoCaptureAPIs ResolveCaptureApi(string? apiId)
    {
        if (string.IsNullOrWhiteSpace(apiId))
        {
            return VideoCaptureAPIs.ANY;
        }

        return Enum.TryParse<VideoCaptureAPIs>(apiId, ignoreCase: true, out var parsed)
            ? parsed
            : VideoCaptureAPIs.ANY;
    }

    private async Task<VideoProcessResult> ProcessCaptureFramesAsync(
        VideoCapture capture,
        string sourceLabel,
        double fps,
        Mat medianBackground,
        int threshold,
        ProcessingOptions options,
        DetectionOptions detectionOptions,
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
        var yoloMissingReported = false;
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
                if (detectionOptions.UseYolo)
                {
                    var yoloRendered = TryRenderYoloDetections(colorDetections, colorResized, detectionOptions, out var yoloReason);
                    if (!yoloRendered)
                    {
                        if (!yoloMissingReported)
                        {
                            yoloMissingReported = true;
                            await onStatus($"YOLO selected but model is unavailable ({yoloReason}). Falling back to motion+color detection.");
                        }

                        RenderColorDetections(colorDetections, colorResized, cleanMask, hsv, movingRects, activeMinColorPixels);
                    }
                }
                else
                {
                    RenderColorDetections(colorDetections, colorResized, cleanMask, hsv, movingRects, activeMinColorPixels);
                }

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
        return PreBakeBackgroundInternalAsync(videoPath, sampleCount, ProcessingOptions.Default, cancellationToken);
    }

    public async Task<string> EnsureBakedBackgroundAsync(
        string videoPath,
        int sampleCount,
        ProcessingOptions options,
        string? bakeImagePath,
        CancellationToken cancellationToken,
        Func<string, Task>? onStatus = null)
    {
        if (!File.Exists(videoPath))
        {
            throw new FileNotFoundException("Video file not found.", videoPath);
        }

        using var capture = new VideoCapture(videoPath);
        if (!capture.IsOpened())
        {
            throw new InvalidOperationException($"Unable to open video: {Path.GetFileName(videoPath)}");
        }

        var frameWidth = (int)capture.FrameWidth;
        var frameHeight = (int)capture.FrameHeight;
        if (frameWidth <= 0 || frameHeight <= 0)
        {
            throw new InvalidOperationException("Video has invalid dimensions.");
        }

        var processSize = BuildProcessSize(frameWidth, frameHeight, options.ProcessMaxWidth);
        if (!string.IsNullOrWhiteSpace(bakeImagePath))
        {
            if (onStatus is not null)
            {
                await onStatus($"preparing baked background from image {Path.GetFileName(bakeImagePath)}...");
            }

            return await BuildBackgroundFromImageAsync(bakeImagePath, processSize, cancellationToken);
        }

        if (onStatus is not null)
        {
            await onStatus($"baking background for {Path.GetFileName(videoPath)}...");
        }

        return await BakeBackgroundAsync(videoPath, sampleCount, processSize, cancellationToken, onStatus);
    }

    public Task PreBakeBackgroundAsync(string videoPath, int sampleCount, ProcessingOptions options, CancellationToken cancellationToken)
    {
        return PreBakeBackgroundInternalAsync(videoPath, sampleCount, options, cancellationToken);
    }

    private async Task PreBakeBackgroundInternalAsync(string videoPath, int sampleCount, ProcessingOptions options, CancellationToken cancellationToken)
    {
        if (!File.Exists(videoPath))
        {
            return;
        }

        using var capture = new VideoCapture(videoPath);
        if (!capture.IsOpened())
        {
            return;
        }

        var frameWidth = (int)capture.FrameWidth;
        var frameHeight = (int)capture.FrameHeight;
        if (frameWidth <= 0 || frameHeight <= 0)
        {
            return;
        }

        var processSize = BuildProcessSize(frameWidth, frameHeight, options.ProcessMaxWidth);
        await BakeBackgroundAsync(videoPath, sampleCount, processSize, cancellationToken);
    }

    private async Task<Mat> CreateMedianBackgroundForUsbCameraAsync(
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

    private Task<string> BakeBackgroundAsync(
        string videoPath,
        int sampleCount,
        Size processSize,
        CancellationToken cancellationToken,
        Func<string, Task>? onStatus = null)
    {
        var bakedPath = BuildBackgroundFilePath(videoPath, sampleCount, processSize);

        if (File.Exists(bakedPath))
        {
            return Task.FromResult(bakedPath);
        }

        lock (_bakeSync)
        {
            if (_bakeJobs.TryGetValue(bakedPath, out var running))
            {
                return running;
            }

            var bakeTask = BakeBackgroundCoreAsync(videoPath, sampleCount, processSize, bakedPath, cancellationToken, onStatus);
            _bakeJobs[bakedPath] = bakeTask;
            _ = bakeTask.ContinueWith(_ =>
            {
                lock (_bakeSync)
                {
                    _bakeJobs.Remove(bakedPath);
                }
            }, TaskScheduler.Default);

            return bakeTask;
        }
    }

    private static async Task<string> BakeBackgroundCoreAsync(
        string videoPath,
        int sampleCount,
        Size processSize,
        string bakedPath,
        CancellationToken cancellationToken,
        Func<string, Task>? onStatus)
    {
        await Task.Yield();

        using var capture = new VideoCapture(videoPath);
        if (!capture.IsOpened())
        {
            throw new InvalidOperationException($"Unable to open video for baking: {Path.GetFileName(videoPath)}");
        }

        var frameCount = (int)Math.Max(0, capture.Get(VideoCaptureProperties.FrameCount));
        var progressStep = Math.Max(1, sampleCount / 10);
        var fileName = Path.GetFileName(videoPath);

        using var medianBackground = EstimateMedianBackground(
            capture,
            frameCount,
            sampleCount,
            processSize,
            cancellationToken,
            reportProgress: (completed, total) =>
            {
                if (onStatus is null)
                {
                    return;
                }

                if (completed % progressStep != 0 && completed != total)
                {
                    return;
                }

                onStatus($"baking {fileName}: sample {completed}/{total}").GetAwaiter().GetResult();
            });

        var directory = Path.GetDirectoryName(bakedPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        Cv2.ImWrite(bakedPath, medianBackground);
        return bakedPath;
    }

    private async Task<string> BuildBackgroundFromImageAsync(
        string imagePath,
        Size processSize,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(imagePath))
        {
            throw new FileNotFoundException("Bake image not found.", imagePath);
        }

        var bakedPath = BuildBackgroundImageFilePath(imagePath, processSize);
        if (File.Exists(bakedPath))
        {
            return bakedPath;
        }

        await Task.Yield();

        using var background = LoadBackgroundImageMat(imagePath, processSize);
        var directory = Path.GetDirectoryName(bakedPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        Cv2.ImWrite(bakedPath, background);
        return bakedPath;
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

    private static string BuildBackgroundFilePath(string videoPath, int sampleCount, Size processSize)
    {
        var info = new FileInfo(videoPath);
        var keyRaw = $"{videoPath}|{info.Length}|{info.LastWriteTimeUtc.Ticks}|{sampleCount}|{processSize.Width}|{processSize.Height}";
        var key = ComputeSha256Hex(keyRaw);
        return Path.Combine(GetBackgroundCacheRoot(), $"{key}.png");
    }

    private static string BuildBackgroundImageFilePath(string imagePath, Size processSize)
    {
        var info = new FileInfo(imagePath);
        var keyRaw = $"image|{imagePath}|{info.Length}|{info.LastWriteTimeUtc.Ticks}|{processSize.Width}|{processSize.Height}";
        var key = ComputeSha256Hex(keyRaw);
        return Path.Combine(GetBackgroundCacheRoot(), $"{key}.png");
    }

    private static string GetBackgroundCacheRoot()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ObjectTracker",
            "background-cache");
    }

    private static string ComputeSha256Hex(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
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

            await Task.Delay(slice);
        }
    }

    private bool TryRenderYoloDetections(Mat destination, Mat sourceColor, DetectionOptions options, out string reason)
    {
        InferenceSession? session;
        string? activeModelPath;
        lock (_yoloSync)
        {
            session = _yoloSession;
            activeModelPath = _yoloModelPath;
        }

        if (session is null)
        {
            var modelPath = DetectorRegistry.FindYoloModel();
            if (ConfigureYoloModel(modelPath, out var configureMessage))
            {
                lock (_yoloSync)
                {
                    session = _yoloSession;
                }
            }

            if (session is null)
            {
                reason = configureMessage;
                return false;
            }
        }

        var inputSize = Math.Clamp(options.ImageSize, 320, 1280);
        var confidenceThreshold = Math.Clamp(options.ConfidencePercent, 1, 99) / 100f;
        var nmsThreshold = Math.Clamp(options.NmsIouPercent, 1, 99) / 100f;

        try
        {
            // Preprocess: resize to input, normalize to [0,1], convert BGR→RGB, layout to NCHW
            using var resized = new Mat();
            Cv2.Resize(sourceColor, resized, new Size(inputSize, inputSize));

            var tensor = new DenseTensor<float>(new[] { 1, 3, inputSize, inputSize });
            for (var py = 0; py < inputSize; py++)
            {
                for (var px = 0; px < inputSize; px++)
                {
                    var pixel = resized.At<Vec3b>(py, px);
                    tensor[0, 0, py, px] = pixel.Item2 / 255f; // R
                    tensor[0, 1, py, px] = pixel.Item1 / 255f; // G
                    tensor[0, 2, py, px] = pixel.Item0 / 255f; // B
                }
            }

            var inputName = session.InputMetadata.Keys.First();
            var inputs = new[] { NamedOnnxValue.CreateFromTensor(inputName, tensor) };

            using var results = session.Run(inputs);
            var output = results.First().AsTensor<float>();
            var dims = output.Dimensions;

            // YOLO11/v8 standard output: [1, 4+classes, anchors] e.g. [1, 84, 8400]
            // Some variants may be transposed:              [1, anchors, 4+classes] e.g. [1, 8400, 84]
            var transposed = dims.Length >= 3 && dims[1] > dims[2];
            var anchors = transposed ? dims[1] : dims[2];
            var channels = transposed ? dims[2] : dims[1];

            var scaleX = (float)sourceColor.Width / inputSize;
            var scaleY = (float)sourceColor.Height / inputSize;
            var candidates = new List<(Rect Rect, float Score, int ClassId)>();

            for (var i = 0; i < anchors; i++)
            {
                var cx = (transposed ? output[0, i, 0] : output[0, 0, i]) * scaleX;
                var cy = (transposed ? output[0, i, 1] : output[0, 1, i]) * scaleY;
                var w  = (transposed ? output[0, i, 2] : output[0, 2, i]) * scaleX;
                var h  = (transposed ? output[0, i, 3] : output[0, 3, i]) * scaleY;

                var bestClass = -1;
                var bestScore = confidenceThreshold;
                for (var c = 4; c < channels; c++)
                {
                    var score = transposed ? output[0, i, c] : output[0, c, i];
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestClass = c - 4;
                    }
                }

                if (bestClass < 0)
                {
                    continue;
                }

                var x = Math.Max(0, (int)Math.Round(cx - (w / 2f)));
                var y = Math.Max(0, (int)Math.Round(cy - (h / 2f)));
                var rw = Math.Max(1, Math.Min(sourceColor.Width - x, (int)Math.Round(w)));
                var rh = Math.Max(1, Math.Min(sourceColor.Height - y, (int)Math.Round(h)));
                candidates.Add((new Rect(x, y, rw, rh), bestScore, bestClass));
            }

            candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
            var kept = new List<int>();
            for (var i = 0; i < candidates.Count; i++)
            {
                var keep = true;
                foreach (var keptIndex in kept)
                {
                    if (ComputeIoU(candidates[i].Rect, candidates[keptIndex].Rect) > nmsThreshold)
                    {
                        keep = false;
                        break;
                    }
                }

                if (keep)
                {
                    kept.Add(i);
                }
            }

            // --- Cross-frame IoU tracking ---
            // Match kept candidates against existing tracks, assign stable IDs
            var detectedRects = kept.Select(i => candidates[i]).ToList();
            var trackAssignments = new Dictionary<int, int>(); // trackId → candidate index
            var usedCandidateIndices = new HashSet<int>();

            lock (_yoloTrackerSync)
            {
                // Greedy match: for each existing track find best IoU candidate
                foreach (var (trackId, entry) in _yoloTracks)
                {
                    var bestIdx = -1;
                    var bestIou = YoloTrackIoUThreshold;
                    for (var ci = 0; ci < detectedRects.Count; ci++)
                    {
                        if (usedCandidateIndices.Contains(ci)) continue;
                        var iou = ComputeIoU(entry.Rect, detectedRects[ci].Rect);
                        if (iou > bestIou)
                        {
                            bestIou = iou;
                            bestIdx = ci;
                        }
                    }

                    if (bestIdx >= 0)
                    {
                        trackAssignments[trackId] = bestIdx;
                        usedCandidateIndices.Add(bestIdx);
                        entry.Rect = detectedRects[bestIdx].Rect;
                        entry.ClassId = detectedRects[bestIdx].ClassId;
                        entry.MissedFrames = 0;
                    }
                    else
                    {
                        entry.MissedFrames++;
                    }
                }

                // Create new tracks for unmatched candidates
                var newTrackMap = new Dictionary<int, int>(); // candidateIdx → newTrackId
                for (var ci = 0; ci < detectedRects.Count; ci++)
                {
                    if (usedCandidateIndices.Contains(ci)) continue;
                    var newId = _yoloNextTrackId++;
                    _yoloTracks[newId] = new YoloTrackEntry
                    {
                        Rect = detectedRects[ci].Rect,
                        ClassId = detectedRects[ci].ClassId,
                        MissedFrames = 0,
                    };
                    newTrackMap[ci] = newId;
                }

                // Age out stale tracks
                foreach (var stale in _yoloTracks.Where(kv => kv.Value.MissedFrames > YoloTrackMaxMissed).Select(kv => kv.Key).ToList())
                {
                    _yoloTracks.Remove(stale);
                }

                // Draw all active (non-stale) tracks
                foreach (var (trackId, entry) in _yoloTracks)
                {
                    if (entry.MissedFrames > 0) continue; // Only draw tracks seen this frame
                    var color = ColorForClass(entry.ClassId);
                    var label = $"#{trackId} {NameForClass(entry.ClassId)}";
                    Cv2.Rectangle(destination, entry.Rect, color, 2);

                    // Background pill behind text
                    var textSize = Cv2.GetTextSize(label, HersheyFonts.HersheySimplex, 0.48, 1, out var baseline);
                    var textOrigin = new Point(entry.Rect.X, Math.Max(textSize.Height + 2, entry.Rect.Y - 4));
                    var pillTl = new Point(textOrigin.X, textOrigin.Y - textSize.Height - baseline);
                    var pillBr = new Point(textOrigin.X + textSize.Width, textOrigin.Y + baseline);
                    Cv2.Rectangle(destination, pillTl, pillBr, color, -1);
                    Cv2.PutText(destination, label, textOrigin, HersheyFonts.HersheySimplex, 0.48, new Scalar(10, 10, 10), 1);
                }
            }

            reason = "ok";
            return true;
        }
        catch (Exception ex)
        {
            lock (_yoloSync)
            {
                _yoloSession?.Dispose();
                _yoloSession = null;
                _yoloModelPath = null;
                _yoloStatus = $"inference error: {ex.Message}";
            }

            reason = ex.Message;
            return false;
        }
    }

    private static float ComputeIoU(Rect a, Rect b)
    {
        var x1 = Math.Max(a.X, b.X);
        var y1 = Math.Max(a.Y, b.Y);
        var x2 = Math.Min(a.Right, b.Right);
        var y2 = Math.Min(a.Bottom, b.Bottom);
        var interW = Math.Max(0, x2 - x1);
        var interH = Math.Max(0, y2 - y1);
        var inter = interW * interH;
        var union = a.Width * a.Height + b.Width * b.Height - inter;
        return union > 0 ? (float)inter / union : 0f;
    }

    private static PreviewFrameSet BuildPreviewFrameSet(Mat backgroundMask, Mat movingColor, Mat colorDetections, Mat motionView)
    {
        Cv2.ImEncode(".jpg", backgroundMask, out var backgroundMaskJpeg, new[] { (int)ImwriteFlags.JpegQuality, 80 });
        Cv2.ImEncode(".jpg", movingColor, out var movingColorJpeg, new[] { (int)ImwriteFlags.JpegQuality, 75 });
        Cv2.ImEncode(".jpg", colorDetections, out var colorDetectionJpeg, new[] { (int)ImwriteFlags.JpegQuality, 75 });
        Cv2.ImEncode(".jpg", motionView, out var motionJpeg, new[] { (int)ImwriteFlags.JpegQuality, 75 });

        return new PreviewFrameSet(backgroundMaskJpeg, movingColorJpeg, colorDetectionJpeg, motionJpeg);
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

    public readonly struct VideoProcessResult
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

    public readonly record struct ProcessingOptions(
        int ProcessMaxWidth,
        int MinMotionArea,
        int MinColorPixels,
        int MorphKernelSize)
    {
        public static ProcessingOptions Default => new(640, 220, 40, 3);
    }

    public readonly record struct DetectionOptions(
        bool UseYolo,
        int ConfidencePercent,
        int NmsIouPercent,
        int ImageSize)
    {
        public static DetectionOptions Default => new(false, 50, 45, 640);
    }

    public readonly record struct LiveTuning(
        int Threshold,
        int MinMotionArea,
        int MinColorPixels,
        int MorphKernelSize);

    private readonly record struct MotionTrackState(Point2f Center, Rect Rect, double TimestampSec);

    public readonly struct PreviewFrameSet
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
}