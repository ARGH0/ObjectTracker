using ObjectTracker.Core.Domain;
using ObjectTracker.Core.Ports;

namespace ObjectTracker.Vision;

public sealed class AlternativeNoOpDetector : IDetectionAlgorithm
{
    public DetectorMode Mode => DetectorMode.Alternative;
    public string Name => "Alternative NoOp";

    public Task<IReadOnlyList<Detection>> DetectAsync(FramePacket frame, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<Detection>>([]);
    }
}