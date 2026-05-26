using ObjectTracker.Core.Domain;

namespace ObjectTracker.Core.Ports;

public interface IColorFilterControl
{
    IReadOnlyList<string> AvailableColors { get; }

    IReadOnlyList<string> EnabledColors { get; }

    IReadOnlyList<ColorCalibrationProfile> ColorCalibrations { get; }

    void SetEnabledColors(IEnumerable<string> colors);

    void SetColorCalibrations(IEnumerable<ColorCalibrationProfile> calibrations);
}
