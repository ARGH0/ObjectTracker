using OpenCvSharp;
using ObjectTracker.Core.Domain;
using ObjectTracker.Core.Ports;

namespace ObjectTracker.Vision;

/// <summary>
/// Internal interface for detection algorithms that support cached/pre-decoded image optimization.
/// Extends IDetectionAlgorithm to allow high-performance decoding strategies without polluting Core API.
/// </summary>
internal interface ICachedDetectionAlgorithm : IDetectionAlgorithm
{
    /// <summary>
    /// Detect using a pre-decoded image for performance optimization.
    /// Reduces redundant JPEG decoding when multiple detectors process the same frame.
    /// </summary>
    /// <param name="frame">Original frame packet with metadata</param>
    /// <param name="decodedImage">Pre-decoded Mat image (format depends on detector type: Color BGR or Grayscale)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Detections from the provided image</returns>
    Task<IReadOnlyList<Detection>> DetectAsync(FramePacket frame, Mat decodedImage, CancellationToken cancellationToken);
}
