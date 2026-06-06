using System.Diagnostics;
using ObjectTracker.Core.Domain;
using ObjectTracker.Core.Ports;
using Cv = OpenCvSharp;

namespace ObjectTracker.Vision;

public sealed class PipelineController : IPipelineController, IAsyncDisposable
{
    private readonly IFrameSourceFactory frameSourceFactory;
    private readonly IDetectorManager detectorManager;
    private readonly ITracker tracker;
    private readonly IReadOnlyList<IOutputPort> outputs;
    private readonly IClock clock;
    private readonly SemaphoreSlim lifecycleLock = new(1, 1);
    private readonly Lock overlaySettingsLock = new();
    private readonly Dictionary<string, RgbColor> overlayColors = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> debugViewCameraSourceIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> includedCameraSourceIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, LaneState> activeLanes = new(StringComparer.OrdinalIgnoreCase);

    private int framesInWindow;
    private long windowStartMs;
    private int overlayLineThickness = 2;
    private bool isRunning;

    public PipelineController(
        IFrameSourceFactory frameSourceFactory,
        IDetectorManager detectorManager,
        ITracker tracker,
        IEnumerable<IOutputPort> outputs,
        IClock clock)
    {
        this.frameSourceFactory = frameSourceFactory;
        this.detectorManager = detectorManager;
        this.tracker = tracker;
        this.outputs = outputs.ToList();
        this.clock = clock;
        windowStartMs = this.clock.UtcNowMs();
        ResetOverlaySettings();
    }

    public bool IsRunning => isRunning;

    public IReadOnlyList<FrameSourceInfo> AvailableSources => frameSourceFactory.GetAvailableSources();

    public IReadOnlyList<DetectorMode> AvailableDetectors => detectorManager.SupportedModes;

    public IReadOnlyList<string> AvailableColorFilters => detectorManager.AvailableColorFilters;

    public IReadOnlyList<string> EnabledColorFilters => detectorManager.EnabledColorFilters;

    public IReadOnlyList<ColorCalibrationProfile> ColorCalibrations => detectorManager.ColorCalibrations;

    public int OverlayLineThickness
    {
        get
        {
            lock (overlaySettingsLock)
            {
                return overlayLineThickness;
            }
        }
    }

    public IReadOnlyDictionary<string, RgbColor> OverlayColors
    {
        get
        {
            lock (overlaySettingsLock)
            {
                return new Dictionary<string, RgbColor>(overlayColors, StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    public DetectorMode ActiveDetector => detectorManager.ActiveMode;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            if (IsRunning)
            {
                return;
            }

            isRunning = true;
            foreach (var cameraSourceId in includedCameraSourceIds.ToList())
            {
                await StartLaneAsync(cameraSourceId, cancellationToken);
            }
        }
        finally
        {
            lifecycleLock.Release();
        }
    }

    public async Task StartAsync(string sourceId, CancellationToken cancellationToken)
    {
        SetVisionPipelineInclusion(sourceId, included: true);
        await StartAsync(cancellationToken);
    }

    private async Task StartLaneAsync(string sourceId, CancellationToken cancellationToken)
    {
        if (activeLanes.ContainsKey(sourceId))
        {
            return;
        }

        var source = frameSourceFactory.Create(sourceId);
        await source.StartAsync(cancellationToken);

        var laneCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var laneTask = Task.Run(() => RunLoopAsync(source, laneCts.Token), laneCts.Token);
        activeLanes[sourceId] = new LaneState(source, laneCts, laneTask);
        await PublishStatusAsync($"Pipeline gestart met bron '{source.DisplayName}'.", cancellationToken);
        await PublishStatusAsync($"Capture diagnostics: {source.Diagnostics}", cancellationToken);
    }

    private async Task StopLaneAsync(string sourceId, LaneState lane, CancellationToken cancellationToken)
    {
        lane.Cancellation.Cancel();
        try
        {
            await lane.Task;
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            activeLanes.Remove(sourceId);
            await lane.Source.StopAsync(cancellationToken);
            await lane.Source.DisposeAsync();
            lane.Cancellation.Dispose();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            if (!IsRunning)
            {
                return;
            }

            foreach (var lane in activeLanes.ToList())
            {
                await StopLaneAsync(lane.Key, lane.Value, cancellationToken);
            }

            tracker.Reset();
            await PublishStatusAsync("Pipeline gestopt.", cancellationToken);
        }
        finally
        {
            isRunning = false;
            lifecycleLock.Release();
        }
    }

    public async Task SwitchSourceAsync(string sourceId, CancellationToken cancellationToken)
    {
        await StopAsync(cancellationToken);
        includedCameraSourceIds.Clear();
        SetVisionPipelineInclusion(sourceId, included: true);
        await StartAsync(cancellationToken);
    }

    public void SwitchDetector(DetectorMode mode)
    {
        detectorManager.SwitchMode(mode);
        _ = PublishStatusAsync($"Detector gewijzigd naar '{mode}'.", CancellationToken.None);
    }

    public void SetEnabledColorFilters(IEnumerable<string> colors)
    {
        var selected = colors.ToList();
        detectorManager.SetEnabledColorFilters(selected);
        var joined = selected.Count == 0 ? "(geen)" : string.Join(", ", selected);
        _ = PublishStatusAsync($"Kleurfilters bijgewerkt: {joined}", CancellationToken.None);
    }

    public void SetColorCalibrations(IEnumerable<ColorCalibrationProfile> calibrations)
    {
        var snapshot = calibrations.ToList();
        detectorManager.SetColorCalibrations(snapshot);
        _ = PublishStatusAsync($"Kleurkalibraties bijgewerkt: {snapshot.Count}", CancellationToken.None);
    }

    public void SetVisionPipelineInclusion(string cameraSourceId, bool included)
    {
        if (string.IsNullOrWhiteSpace(cameraSourceId))
        {
            return;
        }

        if (included)
        {
            includedCameraSourceIds.Add(cameraSourceId);
        }
        else
        {
            includedCameraSourceIds.Remove(cameraSourceId);
        }

        if (IsRunning)
        {
            _ = ReconcileLaneAsync(cameraSourceId, included, CancellationToken.None);
        }
    }

    private async Task ReconcileLaneAsync(string cameraSourceId, bool included, CancellationToken cancellationToken)
    {
        await lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            if (!IsRunning)
            {
                return;
            }

            if (included)
            {
                await StartLaneAsync(cameraSourceId, cancellationToken);
            }
            else if (activeLanes.TryGetValue(cameraSourceId, out var lane))
            {
                await StopLaneAsync(cameraSourceId, lane, cancellationToken);
            }
        }
        finally
        {
            lifecycleLock.Release();
        }
    }

    public void SetDebugViewEnabled(string cameraSourceId, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(cameraSourceId))
        {
            return;
        }

        lock (overlaySettingsLock)
        {
            if (enabled)
            {
                debugViewCameraSourceIds.Add(cameraSourceId);
            }
            else
            {
                debugViewCameraSourceIds.Remove(cameraSourceId);
            }
        }
    }

    public void SetOverlayLineThickness(int thickness)
    {
        lock (overlaySettingsLock)
        {
            overlayLineThickness = Math.Clamp(thickness, 1, 10);
        }
    }

    public void SetOverlayColor(string kind, byte r, byte g, byte b)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return;
        }

        lock (overlaySettingsLock)
        {
            overlayColors[kind.Trim().ToUpperInvariant()] = new RgbColor(r, g, b);
        }
    }

    public void ResetOverlaySettings()
    {
        lock (overlaySettingsLock)
        {
            overlayLineThickness = 2;
            overlayColors.Clear();
            foreach (var (kind, color) in CreateDefaultOverlayColors())
            {
                overlayColors[kind] = color;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None);
        lifecycleLock.Dispose();
    }

    private async Task RunLoopAsync(IFrameSource source, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var frame = await source.ReadFrameAsync(cancellationToken);
                if (frame is null)
                {
                    await Task.Delay(10, cancellationToken);
                    continue;
                }

                var sw = Stopwatch.StartNew();
                var detections = await detectorManager.DetectAsync(frame, cancellationToken);
                var trainStates = tracker.Update(detections, frame.TimestampUtcMs);
                sw.Stop();

                var fps = CalculateFps(frame.TimestampUtcMs);
                var renderedFrame = RenderDetections(frame, detections);
                var debugFrames = BuildDebugFrames(frame);
                var snapshot = new PipelineSnapshot(
                    frame.SourceId,
                    frame,
                    renderedFrame,
                    [],
                    detections.Select(ToTrainObservation).ToList(),
                    trainStates,
                    debugFrames,
                    detectorManager.ActiveMode,
                    new PipelineSnapshotTiming(frame.TimestampUtcMs, fps, sw.Elapsed.TotalMilliseconds));

                foreach (var output in outputs)
                {
                    await output.PublishSnapshotAsync(snapshot, cancellationToken);
                }

                var sourceDiagnosticEvent = source.ConsumeDiagnosticEvent();
                if (!string.IsNullOrWhiteSpace(sourceDiagnosticEvent))
                {
                    await PublishStatusAsync(sourceDiagnosticEvent, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            await PublishStatusAsync($"Vision Pipeline lane '{source.DisplayName}' failed: {ex.Message}", CancellationToken.None);
        }
    }

    private async Task PublishStatusAsync(string status, CancellationToken cancellationToken)
    {
        foreach (var output in outputs)
        {
            await output.PublishStatusAsync(status, cancellationToken);
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

    private IReadOnlyList<DebugFrame> BuildDebugFrames(FramePacket sourceFrame)
    {
        lock (overlaySettingsLock)
        {
            if (!debugViewCameraSourceIds.Contains(sourceFrame.SourceId))
            {
                return [];
            }
        }

        return [new DebugFrame("source", sourceFrame)];
    }

    private FramePacket RenderDetections(FramePacket frame, IReadOnlyList<Detection> detections)
    {
        if (detections.Count == 0)
        {
            return frame;
        }

        int lineThickness;
        Dictionary<string, RgbColor> colors;
        lock (overlaySettingsLock)
        {
            lineThickness = overlayLineThickness;
            colors = new Dictionary<string, RgbColor>(overlayColors, StringComparer.OrdinalIgnoreCase);
        }

        using var image = Cv.Cv2.ImDecode(frame.EncodedJpeg, Cv.ImreadModes.Color);
        if (image.Empty())
        {
            return frame;
        }

        foreach (var detection in detections)
        {
            var color = GetColorForKind(detection.Kind, colors);
            var rect = new Cv.Rect(
                (int)detection.BoxX,
                (int)detection.BoxY,
                Math.Max(1, (int)detection.BoxWidth),
                Math.Max(1, (int)detection.BoxHeight));

            Cv.Cv2.Rectangle(image, rect, color, lineThickness);
            var labelPoint = new Cv.Point(rect.X, Math.Max(12, rect.Y - 6));
            Cv.Cv2.PutText(
                image,
                detection.Kind,
                labelPoint,
                Cv.HersheyFonts.HersheySimplex,
                0.55,
                Cv.Scalar.White,
                1,
                Cv.LineTypes.AntiAlias);
        }

        Cv.Cv2.ImEncode(".jpg", image, out var encoded, [new Cv.ImageEncodingParam(Cv.ImwriteFlags.JpegQuality, 90)]);
        return new FramePacket(
            frame.SourceId,
            frame.TimestampUtcMs,
            frame.Width,
            frame.Height,
            encoded);
    }

    private static Cv.Scalar GetColorForKind(string kind, IReadOnlyDictionary<string, RgbColor> colors)
    {
        if (colors.TryGetValue(kind, out var configured))
        {
            return new Cv.Scalar(configured.B, configured.G, configured.R);
        }

        return kind.ToUpperInvariant() switch
        {
            "RED" => new Cv.Scalar(0, 0, 255),
            "ORANGE" => new Cv.Scalar(0, 165, 255),
            "PINK" => new Cv.Scalar(180, 105, 255),
            "PURPLE" => new Cv.Scalar(160, 32, 240),
            "GREEN" => new Cv.Scalar(0, 255, 0),
            "BLUE" => new Cv.Scalar(255, 191, 0),
            "CYAN" => new Cv.Scalar(255, 255, 0),
            "YELLOW" => new Cv.Scalar(0, 255, 255),
            "WHITE" => new Cv.Scalar(255, 255, 255),
            "BLACK" => new Cv.Scalar(192, 192, 192),
            "ARUCO" => new Cv.Scalar(255, 255, 255),
            _ => new Cv.Scalar(0, 165, 255)
        };
    }

    private static IReadOnlyDictionary<string, RgbColor> CreateDefaultOverlayColors()
    {
        return new Dictionary<string, RgbColor>(StringComparer.OrdinalIgnoreCase)
        {
            ["RED"] = new RgbColor(255, 0, 0),
            ["ORANGE"] = new RgbColor(255, 165, 0),
            ["PINK"] = new RgbColor(255, 105, 180),
            ["PURPLE"] = new RgbColor(160, 32, 240),
            ["GREEN"] = new RgbColor(0, 255, 0),
            ["BLUE"] = new RgbColor(0, 191, 255),
            ["CYAN"] = new RgbColor(0, 255, 255),
            ["YELLOW"] = new RgbColor(255, 255, 0),
            ["WHITE"] = new RgbColor(255, 255, 255),
            ["BLACK"] = new RgbColor(192, 192, 192),
            ["ARUCO"] = new RgbColor(255, 255, 255)
        };
    }

    private int CalculateFps(long nowMs)
    {
        framesInWindow++;
        var elapsed = Math.Max(1, nowMs - windowStartMs);

        if (elapsed >= 1000)
        {
            var fps = (int)(framesInWindow * 1000 / elapsed);
            framesInWindow = 0;
            windowStartMs = nowMs;
            return fps;
        }

        return (int)(framesInWindow * 1000 / elapsed);
    }

    private sealed record LaneState(IFrameSource Source, CancellationTokenSource Cancellation, Task Task);
}
