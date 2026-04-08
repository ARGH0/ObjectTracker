namespace ObjectTracker.Core.Domain;

public sealed record FrameSourceInfo(string Id, string DisplayName)
{
    public override string ToString() => DisplayName;
}