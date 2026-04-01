using ObjectTracker.Core.Domain;

namespace ObjectTracker.Core.Ports;

public interface IFrameSourceFactory
{
    IReadOnlyList<FrameSourceInfo> GetAvailableSources();
    IFrameSource Create(string sourceId);
}