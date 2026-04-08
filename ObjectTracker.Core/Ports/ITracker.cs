using ObjectTracker.Core.Domain;

namespace ObjectTracker.Core.Ports;

public interface ITracker
{
    IReadOnlyList<TrackState> Update(IReadOnlyList<Detection> detections);
    void Reset();
}