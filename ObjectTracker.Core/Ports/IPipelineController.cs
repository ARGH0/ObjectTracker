using ObjectTracker.Core.Domain;

namespace ObjectTracker.Core.Ports;

public interface IPipelineController
{
    bool IsRunning { get; }

    IReadOnlyList<FrameSourceInfo> AvailableSources { get; }

    IReadOnlyList<DetectorMode> AvailableDetectors { get; }

    IReadOnlyList<string> AvailableColorFilters { get; }

    IReadOnlyList<string> EnabledColorFilters { get; }

    IReadOnlyList<ColorCalibrationProfile> ColorCalibrations { get; }

    int OverlayLineThickness { get; }

    IReadOnlyDictionary<string, RgbColor> OverlayColors { get; }

    DetectorMode ActiveDetector { get; }

    int TargetFramesPerSecond { get; }

    Task StartAsync(CancellationToken cancellationToken);

    Task StartAsync(string sourceId, CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);

    Task SwitchSourceAsync(string sourceId, CancellationToken cancellationToken);

    void SwitchDetector(DetectorMode mode);

    void SetEnabledColorFilters(IEnumerable<string> colors);

    void SetColorCalibrations(IEnumerable<ColorCalibrationProfile> calibrations);

    void SetTargetFramesPerSecond(int framesPerSecond);

    void SetVisionPipelineInclusion(string cameraSourceId, bool included);

    void SetDebugViewEnabled(string cameraSourceId, bool enabled);

    void SetOverlayLineThickness(int thickness);

    void SetOverlayColor(string kind, byte r, byte g, byte b);

    void ResetOverlaySettings();
}
