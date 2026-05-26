namespace ObjectTracker.Core.Domain;

public enum TrainMotionState
{
    Moving,
    Stationary,
    Uncertain,
    GoneFromTrack
}

public enum CollisionWarningState
{
    None,
    Warning
}

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
