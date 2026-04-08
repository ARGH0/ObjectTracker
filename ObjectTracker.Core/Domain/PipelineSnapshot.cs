namespace ObjectTracker.Core.Domain;

public sealed record PipelineSnapshot(
    FramePacket Frame,
    IReadOnlyList<Detection> Detections,
    IReadOnlyList<TrackState> Tracks,
    DetectorMode ActiveDetector,
    int FramesPerSecond,
    double ProcessingMs);