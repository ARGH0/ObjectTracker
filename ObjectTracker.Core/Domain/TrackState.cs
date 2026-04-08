namespace ObjectTracker.Core.Domain;

public sealed record TrackState(
    string TrackId,
    float X,
    float Y,
    float SpeedPixelsPerSecond,
    float DirectionDegrees,
    string SourceId,
    long TimestampUtcMs);