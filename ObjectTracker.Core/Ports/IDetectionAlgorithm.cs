using ObjectTracker.Core.Domain;

namespace ObjectTracker.Core.Ports;

public interface IDetectionAlgorithm
{
    DetectorMode Mode { get; }
    string Name { get; }
    Task<IReadOnlyList<Detection>> DetectAsync(FramePacket frame, CancellationToken cancellationToken);
}