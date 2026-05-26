using ObjectTracker.Core.Domain;

namespace ObjectTracker.Core.Ports;

public interface ITracker
{
    IReadOnlyList<TrainState> Update(IReadOnlyList<Detection> detections, long frameTimestampUtcMs);

    void Reset();
}
