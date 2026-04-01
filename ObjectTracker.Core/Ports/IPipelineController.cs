using ObjectTracker.Core.Domain;

namespace ObjectTracker.Core.Ports;

public interface IPipelineController
{
    bool IsRunning { get; }
    IReadOnlyList<FrameSourceInfo> AvailableSources { get; }
    IReadOnlyList<DetectorMode> AvailableDetectors { get; }
    IReadOnlyList<string> AvailableColorFilters { get; }
    IReadOnlyList<string> EnabledColorFilters { get; }
    DetectorMode ActiveDetector { get; }
    Task StartAsync(string sourceId, CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
    Task SwitchSourceAsync(string sourceId, CancellationToken cancellationToken);
    void SwitchDetector(DetectorMode mode);
    void SetEnabledColorFilters(IEnumerable<string> colors);
}