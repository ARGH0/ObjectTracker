namespace ObjectTracker.Core.Domain;

public sealed record PipelineSnapshot(
    FramePacket Frame,
    IReadOnlyList<Detection> Detections,
    IReadOnlyList<TrainState> TrainStates,
    DetectorMode ActiveDetector,
    int FramesPerSecond,
    double ProcessingMs);
