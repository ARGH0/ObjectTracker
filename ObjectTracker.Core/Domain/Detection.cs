namespace ObjectTracker.Core.Domain;

public sealed record Detection(
    string Id,
    float X,
    float Y,
    float BoxX,
    float BoxY,
    float BoxWidth,
    float BoxHeight,
    float Confidence,
    string Kind,
    string SourceId,
    long TimestampUtcMs);