using ObjectTracker.Core.Domain;
using ObjectTracker.Core.Ports;
using OpenCvSharp;

namespace ObjectTracker.Vision;

public sealed class AlternativeNoOpDetector : ICachedDetectionAlgorithm
{
    public DetectorMode Mode => DetectorMode.Alternative;
    public string Name => "Alternative NoOp";

    public Task<IReadOnlyList<Detection>> DetectAsync(FramePacket frame, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<Detection>>([]);
    }

    public Task<IReadOnlyList<Detection>> DetectAsync(FramePacket frame, Mat decodedImage, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<Detection>>([]);
    }
}