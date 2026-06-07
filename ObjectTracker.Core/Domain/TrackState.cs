using ObjectTracker.Core.Domain.Enums;

namespace ObjectTracker.Core.Domain;

public sealed record TrainState(
    string LocalTrainId,
    string? TrainColor,
    float X,
    float Y,
    float SpeedPixelsPerSecond,
    float DirectionDegrees,
    float Confidence,
    TrainMotionState MotionState,
    CollisionWarningState CollisionWarningState,
    string SourceId,
    long TimestampUtcMs);
