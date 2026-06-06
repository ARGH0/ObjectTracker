namespace ObjectTracker.Core.Domain;

public sealed record PipelineSnapshot(
    string CameraSourceId,
    FramePacket SourceFrame,
    FramePacket AnnotatedFrame,
    IReadOnlyList<MovingObjectObservation> MovingObjectObservations,
    IReadOnlyList<TrainObservation> TrainObservations,
    IReadOnlyList<TrainState> TrainStates,
    IReadOnlyList<DebugFrame> DebugFrames,
    DetectorMode ActiveDetector,
    PipelineSnapshotTiming Timing);

public sealed record DebugFrame(string Name, FramePacket Frame);

public sealed record MovingObjectObservation(
    string SourceId,
    long TimestampUtcMs,
    float X,
    float Y,
    float BoxX,
    float BoxY,
    float BoxWidth,
    float BoxHeight,
    float Confidence);

public sealed record TrainObservation(
    string SourceId,
    long TimestampUtcMs,
    string TrainColor,
    float X,
    float Y,
    float BoxX,
    float BoxY,
    float BoxWidth,
    float BoxHeight,
    float Confidence);

public sealed record PipelineSnapshotTiming(
    long TimestampUtcMs,
    int TargetFramesPerSecond,
    int FramesPerSecond,
    double ProcessingMs);
