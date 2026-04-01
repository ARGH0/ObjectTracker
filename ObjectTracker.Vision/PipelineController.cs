using System.Diagnostics;
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

    private IFrameSource? _activeSource;
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private int _framesInWindow;
    private long _windowStartMs;

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
    }

    public bool IsRunning => _loopTask is { IsCompleted: false };
    public IReadOnlyList<FrameSourceInfo> AvailableSources => _frameSourceFactory.GetAvailableSources();
    public IReadOnlyList<DetectorMode> AvailableDetectors => _detectorManager.SupportedModes;
    public IReadOnlyList<string> AvailableColorFilters => _detectorManager.AvailableColorFilters;
    public IReadOnlyList<string> EnabledColorFilters => _detectorManager.EnabledColorFilters;
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
            var snapshot = new PipelineSnapshot(frame, detections, tracks, _detectorManager.ActiveMode, fps, sw.Elapsed.TotalMilliseconds);

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

    private int CalculateFps(long nowMs)
    {
        _framesInWindow++;
        var elapsed = Math.Max(1, nowMs - _windowStartMs);

        if (elapsed >= 1000)
        {
            var fps = _framesInWindow;
            _framesInWindow = 0;
            _windowStartMs = nowMs;
            return fps;
        }

        return _framesInWindow;
    }
}