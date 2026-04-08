namespace ObjectTracker.Core.Domain;

public sealed record FramePacket(
    string SourceId,
    long TimestampUtcMs,
    int Width,
    int Height,
    byte[] EncodedJpeg);