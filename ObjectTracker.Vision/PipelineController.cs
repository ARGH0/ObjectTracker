using System.Diagnostics;
using Cv = OpenCvSharp;
using ObjectTracker.Core.Domain;
using ObjectTracker.Core.Ports;

namespace ObjectTracker.Vision;

public sealed class PipelineController : IPipelineController, IAsyncDisposable
{
    private readonly IFrameSourceFactory _frameSourceFactory;
    private readonly IDetectorManager _detectorManager;
    private readonly ITracker _tracker;
    private readonly IReadOnlyList<IOutputPort> _outputs;
    private readonly IClock _clock;
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private readonly object _overlaySettingsLock = new();
    private readonly Dictionary<string, RgbColor> _overlayColors = new(StringComparer.OrdinalIgnoreCase);

    private IFrameSource? _activeSource;
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private int _framesInWindow;
    private long _windowStartMs;
    private int _overlayLineThickness = 2;

    public PipelineController(
        IFrameSourceFactory frameSourceFactory,
        IDetectorManager detectorManager,
        ITracker tracker,
        IEnumerable<IOutputPort> outputs,
        IClock clock)
    {
        _frameSourceFactory = frameSourceFactory;
        _detectorManager = detectorManager;
        _tracker = tracker;
        _outputs = outputs.ToList();
        _clock = clock;
        _windowStartMs = _clock.UtcNowMs();
        ResetOverlaySettings();
    }

    public bool IsRunning => _loopTask is { IsCompleted: false };
    public IReadOnlyList<FrameSourceInfo> AvailableSources => _frameSourceFactory.GetAvailableSources();
    public IReadOnlyList<DetectorMode> AvailableDetectors => _detectorManager.SupportedModes;
    public IReadOnlyList<string> AvailableColorFilters => _detectorManager.AvailableColorFilters;
    public IReadOnlyList<string> EnabledColorFilters => _detectorManager.EnabledColorFilters;
    public int OverlayLineThickness
    {
        get
        {
            lock (_overlaySettingsLock)
            {
                return _overlayLineThickness;
            }
        }
    }

    public IReadOnlyDictionary<string, RgbColor> OverlayColors
    {
        get
        {
            lock (_overlaySettingsLock)
            {
                return new Dictionary<string, RgbColor>(_overlayColors, StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    public DetectorMode ActiveDetector => _detectorManager.ActiveMode;

    public async Task StartAsync(string sourceId, CancellationToken cancellationToken)
    {
        await _lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            if (IsRunning)
            {
                return;
            }

            _activeSource = _frameSourceFactory.Create(sourceId);
            await _activeSource.StartAsync(cancellationToken);

            _loopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _loopTask = Task.Run(() => RunLoopAsync(_loopCts.Token), _loopCts.Token);
            await PublishStatusAsync($"Pipeline gestart met bron '{_activeSource.DisplayName}'.", cancellationToken);
            await PublishStatusAsync($"Capture diagnostics: {_activeSource.Diagnostics}", cancellationToken);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            if (!IsRunning)
            {
                return;
            }

            _loopCts?.Cancel();
            if (_loopTask is not null)
            {
                try
                {
                    await _loopTask;
                }
                catch (OperationCanceledException)
                {
                }
            }

            if (_activeSource is not null)
            {
                await _activeSource.StopAsync(cancellationToken);
                await _activeSource.DisposeAsync();
                _activeSource = null;
            }

            _tracker.Reset();
            await PublishStatusAsync("Pipeline gestopt.", cancellationToken);
        }
        finally
        {
            _loopTask = null;
            _loopCts?.Dispose();
            _loopCts = null;
            _lifecycleLock.Release();
        }
    }

    public async Task SwitchSourceAsync(string sourceId, CancellationToken cancellationToken)
    {
        await StopAsync(cancellationToken);
        await StartAsync(sourceId, cancellationToken);
    }

    public void SwitchDetector(DetectorMode mode)
    {
        _detectorManager.SwitchMode(mode);
        _ = PublishStatusAsync($"Detector gewijzigd naar '{mode}'.", CancellationToken.None);
    }

    public void SetEnabledColorFilters(IEnumerable<string> colors)
    {
        var selected = colors.ToList();
        _detectorManager.SetEnabledColorFilters(selected);
        var joined = selected.Count == 0 ? "(geen)" : string.Join(", ", selected);
        _ = PublishStatusAsync($"Kleurfilters bijgewerkt: {joined}", CancellationToken.None);
    }

    public void SetOverlayLineThickness(int thickness)
    {
        lock (_overlaySettingsLock)
        {
            _overlayLineThickness = Math.Clamp(thickness, 1, 10);
        }
    }

    public void SetOverlayColor(string kind, byte r, byte g, byte b)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return;
        }

        lock (_overlaySettingsLock)
        {
            _overlayColors[kind.Trim().ToLowerInvariant()] = new RgbColor(r, g, b);
        }
    }

    public void ResetOverlaySettings()
    {
        lock (_overlaySettingsLock)
        {
            _overlayLineThickness = 2;
            _overlayColors.Clear();
            foreach (var (kind, color) in CreateDefaultOverlayColors())
            {
                _overlayColors[kind] = color;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None);
        _lifecycleLock.Dispose();
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (_activeSource is null)
            {
                await Task.Delay(30, cancellationToken);
                continue;
            }

            var frame = await _activeSource.ReadFrameAsync(cancellationToken);
            if (frame is null)
            {
                await Task.Delay(10, cancellationToken);
                continue;
            }

            var sw = Stopwatch.StartNew();
            var detections = await _detectorManager.DetectAsync(frame, cancellationToken);
            var tracks = _tracker.Update(detections);
            sw.Stop();

            var fps = CalculateFps(frame.TimestampUtcMs);
            var renderedFrame = RenderDetections(frame, detections);
            var snapshot = new PipelineSnapshot(renderedFrame, detections, tracks, _detectorManager.ActiveMode, fps, sw.Elapsed.TotalMilliseconds);

            foreach (var output in _outputs)
            {
                await output.PublishSnapshotAsync(snapshot, cancellationToken);
            }

            var sourceDiagnosticEvent = _activeSource.ConsumeDiagnosticEvent();
            if (!string.IsNullOrWhiteSpace(sourceDiagnosticEvent))
            {
                await PublishStatusAsync(sourceDiagnosticEvent, cancellationToken);
            }
        }
    }

    private async Task PublishStatusAsync(string status, CancellationToken cancellationToken)
    {
        foreach (var output in _outputs)
        {
            await output.PublishStatusAsync(status, cancellationToken);
        }
    }

    private FramePacket RenderDetections(FramePacket frame, IReadOnlyList<Detection> detections)
    {
        if (detections.Count == 0)
        {
            return frame;
        }

        int lineThickness;
        Dictionary<string, RgbColor> colors;
        lock (_overlaySettingsLock)
        {
            lineThickness = _overlayLineThickness;
            colors = new Dictionary<string, RgbColor>(_overlayColors, StringComparer.OrdinalIgnoreCase);
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

        return kind.ToLowerInvariant() switch
        {
            "red" => new Cv.Scalar(0, 0, 255),
            "orange" => new Cv.Scalar(0, 165, 255),
            "pink" => new Cv.Scalar(180, 105, 255),
            "purple" => new Cv.Scalar(160, 32, 240),
            "green" => new Cv.Scalar(0, 255, 0),
            "blue" => new Cv.Scalar(255, 191, 0),
            "cyan" => new Cv.Scalar(255, 255, 0),
            "yellow" => new Cv.Scalar(0, 255, 255),
            "white" => new Cv.Scalar(255, 255, 255),
            "black" => new Cv.Scalar(192, 192, 192),
            "aruco" => new Cv.Scalar(255, 255, 255),
            _ => new Cv.Scalar(0, 165, 255)
        };
    }

    private static IReadOnlyDictionary<string, RgbColor> CreateDefaultOverlayColors()
    {
        return new Dictionary<string, RgbColor>(StringComparer.OrdinalIgnoreCase)
        {
            ["red"] = new RgbColor(255, 0, 0),
            ["orange"] = new RgbColor(255, 165, 0),
            ["pink"] = new RgbColor(255, 105, 180),
            ["purple"] = new RgbColor(160, 32, 240),
            ["green"] = new RgbColor(0, 255, 0),
            ["blue"] = new RgbColor(0, 191, 255),
            ["cyan"] = new RgbColor(0, 255, 255),
            ["yellow"] = new RgbColor(255, 255, 0),
            ["white"] = new RgbColor(255, 255, 255),
            ["black"] = new RgbColor(192, 192, 192),
            ["aruco"] = new RgbColor(255, 255, 255)
        };
    }

    private int CalculateFps(long nowMs)
    {
        _framesInWindow++;
        var elapsed = Math.Max(1, nowMs - _windowStartMs);

        if (elapsed >= 1000)
        {
            var fps = (int)(_framesInWindow * 1000 / elapsed);
            _framesInWindow = 0;
            _windowStartMs = nowMs;
            return fps;
        }

        return (int)(_framesInWindow * 1000 / elapsed);
    }
    }
}