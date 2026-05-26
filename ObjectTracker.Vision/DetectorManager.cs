using ObjectTracker.Core.Domain;
using ObjectTracker.Core.Ports;
using OpenCvSharp;

namespace ObjectTracker.Vision;

public sealed class DetectorManager : IDetectorManager, IAsyncDisposable
{
    private readonly Dictionary<DetectorMode, ICachedDetectionAlgorithm> _algorithms;
    private readonly List<IColorFilterControl> _colorFilterControls;
    private readonly FrameDecodeCache _decodeCache;
    private readonly object _modeLock = new();
    private DetectorMode _activeMode;

    public DetectorManager(IEnumerable<IDetectionAlgorithm> algorithms, DetectorMode defaultMode = DetectorMode.Hybrid)
    {
        var algorithmList = algorithms.ToList();
        
        // All algorithms in Vision layer implement ICachedDetectionAlgorithm
        _algorithms = algorithmList.Cast<ICachedDetectionAlgorithm>().ToDictionary(algorithm => algorithm.Mode);
        _colorFilterControls = algorithmList.OfType<IColorFilterControl>().ToList();
        _decodeCache = new FrameDecodeCache();
        if (!_algorithms.ContainsKey(defaultMode))
        {
            throw new InvalidOperationException($"Default detector mode '{defaultMode}' is not registered.");
        }

        _activeMode = defaultMode;
    }

    public DetectorMode ActiveMode
    {
        get
        {
            lock (_modeLock)
            {
                return _activeMode;
            }
        }
    }

    public IReadOnlyList<DetectorMode> SupportedModes => _algorithms.Keys.OrderBy(mode => mode).ToList();
    public IReadOnlyList<string> AvailableColorFilters => _colorFilterControls.SelectMany(control => control.AvailableColors).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name).ToList();
    public IReadOnlyList<string> EnabledColorFilters => _colorFilterControls.SelectMany(control => control.EnabledColors).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name).ToList();

    public void SwitchMode(DetectorMode mode)
    {
        lock (_modeLock)
        {
            if (!_algorithms.ContainsKey(mode))
            {
                throw new InvalidOperationException($"Detector mode '{mode}' is not registered.");
            }

            _activeMode = mode;
        }
    }

    public void SetEnabledColorFilters(IEnumerable<string> colors)
    {
        foreach (var control in _colorFilterControls)
        {
            control.SetEnabledColors(colors);
        }
    }

    public async Task<IReadOnlyList<Detection>> DetectAsync(FramePacket frame, CancellationToken cancellationToken)
    {
        DetectorMode mode;
        lock (_modeLock)
        {
            mode = _activeMode;
        }

        if (mode == DetectorMode.Hybrid)
        {
            // Performance optimization: decode once, pass to parallel detector execution
            var bgrImage = _decodeCache.GetOrDecodeColor(frame);
            var grayscaleImage = _decodeCache.GetOrDecodeGrayscale(frame);

            var detectors = new List<(DetectorMode mode, ICachedDetectionAlgorithm algo, Mat image)>();
            
            if (_algorithms.TryGetValue(DetectorMode.Aruco, out var aruco))
            {
                detectors.Add((DetectorMode.Aruco, aruco, grayscaleImage));
            }

            if (_algorithms.TryGetValue(DetectorMode.Color, out var color))
            {
                detectors.Add((DetectorMode.Color, color, bgrImage));
            }

            // Run detectors in parallel with cached decoded images
            var merged = new List<Detection>();
            var detectionTasks = new List<Task<IReadOnlyList<Detection>>>();

            foreach (var (_, detector, image) in detectors)
            {
                var task = detector.DetectAsync(frame, image, cancellationToken);
                detectionTasks.Add(task);
            }

            try
            {
                var results = await Task.WhenAll(detectionTasks);
                foreach (var result in results)
                {
                    merged.AddRange(result);
                }
            }
            finally
            {
                // Mark frame as processed so cache can clean it up
                _decodeCache.MarkProcessed(frame);
            }

            return merged;
        }

        if (!_algorithms.TryGetValue(mode, out var algorithm))
        {
            return [];
        }

        // Single detector mode - use cached decode for consistency
        Mat decodedImage = mode == DetectorMode.Aruco
            ? _decodeCache.GetOrDecodeGrayscale(frame)
            : _decodeCache.GetOrDecodeColor(frame);

        try
        {
            return await algorithm.DetectAsync(frame, decodedImage, cancellationToken);
        }
        finally
        {
            _decodeCache.MarkProcessed(frame);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _decodeCache?.Dispose();
        await ValueTask.CompletedTask;
    }
}