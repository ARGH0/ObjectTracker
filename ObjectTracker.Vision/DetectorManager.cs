using ObjectTracker.Core.Domain;
using ObjectTracker.Core.Ports;

namespace ObjectTracker.Vision;

public sealed class DetectorManager : IDetectorManager
{
    private readonly Dictionary<DetectorMode, IDetectionAlgorithm> algorithms;
    private readonly List<IColorFilterControl> colorFilterControls;
    private readonly Lock modeLock = new();
    private DetectorMode activeMode;

    public DetectorManager(IEnumerable<IDetectionAlgorithm> algorithms, DetectorMode defaultMode = DetectorMode.Hybrid)
    {
        var algorithmList = algorithms.ToList();
        this.algorithms = algorithmList.ToDictionary(algorithm => algorithm.Mode);
        colorFilterControls = algorithmList.OfType<IColorFilterControl>().ToList();
        if (!this.algorithms.ContainsKey(defaultMode))
        {
            throw new InvalidOperationException($"Default detector mode '{defaultMode}' is not registered.");
        }

        activeMode = defaultMode;
    }

    public DetectorMode ActiveMode
    {
        get
        {
            lock (modeLock)
            {
                return activeMode;
            }
        }
    }

    public IReadOnlyList<DetectorMode> SupportedModes => algorithms.Keys.Order().ToList();

    public IReadOnlyList<string> AvailableColorFilters => colorFilterControls.SelectMany(control => control.AvailableColors).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToList();

    public IReadOnlyList<string> EnabledColorFilters => colorFilterControls.SelectMany(control => control.EnabledColors).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToList();

    public IReadOnlyList<ColorCalibrationProfile> ColorCalibrations => colorFilterControls
        .SelectMany(control => control.ColorCalibrations)
        .GroupBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
        .Select(group => group.First())
        .OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
        .ToList();

    public void SwitchMode(DetectorMode mode)
    {
        lock (modeLock)
        {
            if (!algorithms.ContainsKey(mode))
            {
                throw new InvalidOperationException($"Detector mode '{mode}' is not registered.");
            }

            activeMode = mode;
        }
    }

    public void SetEnabledColorFilters(IEnumerable<string> colors)
    {
        foreach (var control in colorFilterControls)
        {
            control.SetEnabledColors(colors);
        }
    }

    public void SetColorCalibrations(IEnumerable<ColorCalibrationProfile> calibrations)
    {
        var snapshot = calibrations.ToList();
        foreach (var control in colorFilterControls)
        {
            control.SetColorCalibrations(snapshot);
        }
    }

    public async Task<IReadOnlyList<Detection>> DetectAsync(FramePacket frame, CancellationToken cancellationToken)
    {
        DetectorMode mode;
        lock (modeLock)
        {
            mode = activeMode;
        }

        if (mode == DetectorMode.Hybrid)
        {
            var merged = new List<Detection>();

            if (algorithms.TryGetValue(DetectorMode.Aruco, out var aruco))
            {
                merged.AddRange(await aruco.DetectAsync(frame, cancellationToken));
            }

            if (algorithms.TryGetValue(DetectorMode.Color, out var color))
            {
                merged.AddRange(await color.DetectAsync(frame, cancellationToken));
            }

            return merged;
        }

        if (!algorithms.TryGetValue(mode, out var algorithm))
        {
            return [];
        }

        return await algorithm.DetectAsync(frame, cancellationToken);
    }
}
