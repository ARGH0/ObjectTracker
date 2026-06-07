using ObjectTracker.Core.Domain;

namespace ObjectTracker.Core.Ports;

public interface IDetectorManager
{
    IReadOnlyList<string> AvailableColorFilters { get; }

    IReadOnlyList<string> EnabledColorFilters { get; }

    IReadOnlyList<ColorCalibrationProfile> ColorCalibrations { get; }

    void SetEnabledColorFilters(IEnumerable<string> colors);

    void SetColorCalibrations(IEnumerable<ColorCalibrationProfile> calibrations);

    Task<IReadOnlyList<Detection>> DetectAsync(FramePacket frame, CancellationToken cancellationToken);
}
