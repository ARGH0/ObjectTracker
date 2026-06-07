using ObjectTracker.Core.Domain;

namespace ObjectTracker.Vision;

public interface IVisualObservationPipeline
{
    Task<VisualObservationResult> ObserveAsync(
        FramePacket sourceFrame,
        VisualObservationSettings settings,
        CancellationToken cancellationToken);
}

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

public sealed record VisualObservationResult(
    IReadOnlyList<MovingObjectObservation> MovingObjectObservations,
    IReadOnlyList<TrainObservation> TrainObservations,
    IReadOnlyList<DebugFrame> DebugFrames)
{
    public static VisualObservationResult Empty { get; } = new([], [], []);
}
