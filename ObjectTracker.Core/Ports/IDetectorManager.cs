using ObjectTracker.Core.Domain;

namespace ObjectTracker.Core.Ports;

public interface IDetectorManager
{
    DetectorMode ActiveMode { get; }
    IReadOnlyList<DetectorMode> SupportedModes { get; }
    IReadOnlyList<string> AvailableColorFilters { get; }
    IReadOnlyList<string> EnabledColorFilters { get; }
    void SwitchMode(DetectorMode mode);
    void SetEnabledColorFilters(IEnumerable<string> colors);
    Task<IReadOnlyList<Detection>> DetectAsync(FramePacket frame, CancellationToken cancellationToken);
}