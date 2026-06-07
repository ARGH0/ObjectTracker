namespace ObjectTracker.Core.Domain;

public sealed record VisualObservationSettings(
    int Threshold,
    int MotionArea,
    int ColorMinPixels,
    int MorphKernelSize,
    int ProcessMaxWidth,
    IReadOnlyList<ColorCalibrationProfile> ColorCalibrations,
    bool DebugViewEnabled)
{
    public static VisualObservationSettings Default { get; } = new(
        Threshold: 100,
        MotionArea: 220,
        ColorMinPixels: 40,
        MorphKernelSize: 3,
        ProcessMaxWidth: 640,
        ColorCalibrations: [],
        DebugViewEnabled: false);
}
