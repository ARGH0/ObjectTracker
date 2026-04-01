using ObjectTracker.Core.Domain;
using ObjectTracker.Core.Ports;

namespace ObjectTracker.Vision;

public sealed class DetectorManager : IDetectorManager
{
    private readonly Dictionary<DetectorMode, IDetectionAlgorithm> _algorithms;
    private readonly List<IColorFilterControl> _colorFilterControls;
    private readonly object _modeLock = new();
    private DetectorMode _activeMode;

    public DetectorManager(IEnumerable<IDetectionAlgorithm> algorithms, DetectorMode defaultMode = DetectorMode.Hybrid)
    {
        var algorithmList = algorithms.ToList();
        _algorithms = algorithmList.ToDictionary(algorithm => algorithm.Mode);
        _colorFilterControls = algorithmList.OfType<IColorFilterControl>().ToList();
        _activeMode = _algorithms.ContainsKey(defaultMode)
            ? defaultMode
            : _algorithms.Keys.First();
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
            var merged = new List<Detection>();

            if (_algorithms.TryGetValue(DetectorMode.Aruco, out var aruco))
            {
                merged.AddRange(await aruco.DetectAsync(frame, cancellationToken));
            }

            if (_algorithms.TryGetValue(DetectorMode.Color, out var color))
            {
                merged.AddRange(await color.DetectAsync(frame, cancellationToken));
            }

            return merged;
        }

        if (!_algorithms.TryGetValue(mode, out var algorithm))
        {
            return [];
        }

        return await algorithm.DetectAsync(frame, cancellationToken);
    }
}