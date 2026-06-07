using ObjectTracker.Core.Domain;
using ObjectTracker.Core.Ports;

namespace ObjectTracker.Vision;

public sealed class DetectorManager : IDetectorManager
{
    private readonly IDetectionAlgorithm algorithm;
    private readonly List<IColorFilterControl> colorFilterControls;

    public DetectorManager(IDetectionAlgorithm algorithm)
    {
        this.algorithm = algorithm;
        colorFilterControls = algorithm is IColorFilterControl colorFilterControl ? [colorFilterControl] : [];
    }

    public IReadOnlyList<string> AvailableColorFilters => colorFilterControls.SelectMany(control => control.AvailableColors).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToList();

    public IReadOnlyList<string> EnabledColorFilters => colorFilterControls.SelectMany(control => control.EnabledColors).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToList();

    public IReadOnlyList<ColorCalibrationProfile> ColorCalibrations => colorFilterControls
        .SelectMany(control => control.ColorCalibrations)
        .GroupBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
        .Select(group => group.First())
        .OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
        .ToList();

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
        return await algorithm.DetectAsync(frame, cancellationToken);
    }
}
