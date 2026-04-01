using ObjectTracker.Core.Domain;
using ObjectTracker.Core.Ports;
using OpenCvSharp;

namespace ObjectTracker.Vision;

public sealed class OpenCvArucoDetector : IDetectionAlgorithm
{
    public DetectorMode Mode => DetectorMode.Aruco;
    public string Name => "OpenCV ArUco-like";

    public Task<IReadOnlyList<Detection>> DetectAsync(FramePacket frame, CancellationToken cancellationToken)
    {
        using var image = Cv2.ImDecode(frame.EncodedJpeg, ImreadModes.Grayscale);
        if (image.Empty())
        {
            return Task.FromResult<IReadOnlyList<Detection>>([]);
        }

        using var threshold = new Mat();
        Cv2.AdaptiveThreshold(image, threshold, 255, AdaptiveThresholdTypes.GaussianC, ThresholdTypes.BinaryInv, 15, 5);

        Cv2.FindContours(threshold, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        var detections = new List<Detection>();

        foreach (var contour in contours)
        {
            var perimeter = Cv2.ArcLength(contour, true);
            if (perimeter < 100)
            {
                continue;
            }

            var approx = Cv2.ApproxPolyDP(contour, 0.03 * perimeter, true);
            var area = Cv2.ContourArea(approx);

            if (approx.Length is < 4 or > 6 || area < 600)
            {
                continue;
            }

            var rect = Cv2.BoundingRect(approx);
            var ratio = rect.Width / (float)Math.Max(1, rect.Height);
            if (ratio < 0.7f || ratio > 1.3f)
            {
                continue;
            }

            detections.Add(new Detection(
                $"aruco-{rect.X}-{rect.Y}",
                rect.X + rect.Width / 2f,
                rect.Y + rect.Height / 2f,
                rect.X,
                rect.Y,
                rect.Width,
                rect.Height,
                Math.Min(1f, (float)(area / (frame.Width * frame.Height))),
                "aruco",
                frame.SourceId,
                frame.TimestampUtcMs));
        }

        return Task.FromResult<IReadOnlyList<Detection>>(detections);
    }
}