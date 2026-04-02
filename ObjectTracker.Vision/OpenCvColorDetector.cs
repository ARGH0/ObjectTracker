using ObjectTracker.Core.Domain;
using ObjectTracker.Core.Ports;
using OpenCvSharp;

namespace ObjectTracker.Vision;

public sealed class OpenCvColorDetector : IDetectionAlgorithm, IColorFilterControl
{
    public DetectorMode Mode => DetectorMode.Color;
    public string Name => "OpenCV Color";
    private readonly object _filterLock = new();

    private static readonly ColorRange[] Ranges =
    [
        new("red", new Scalar(0, 120, 70), new Scalar(10, 255, 255), new Scalar(170, 120, 70), new Scalar(180, 255, 255)),
        new("orange", new Scalar(10, 120, 80), new Scalar(20, 255, 255)),
        new("pink", new Scalar(145, 70, 80), new Scalar(169, 255, 255)),
        new("purple", new Scalar(130, 70, 60), new Scalar(150, 255, 255)),
        new("green", new Scalar(35, 80, 60), new Scalar(85, 255, 255)),
        new("blue", new Scalar(90, 100, 60), new Scalar(130, 255, 255)),
        new("cyan", new Scalar(80, 70, 70), new Scalar(95, 255, 255)),
        new("yellow", new Scalar(20, 110, 80), new Scalar(35, 255, 255)),
        new("white", new Scalar(0, 0, 190), new Scalar(180, 50, 255)),
        new("black", new Scalar(0, 0, 0), new Scalar(180, 255, 45))
    ];

    private HashSet<string> _enabledColors = Ranges.Select(range => range.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> AvailableColors => Ranges.Select(range => range.Name).ToList();

    public IReadOnlyList<string> EnabledColors
    {
        get
        {
            lock (_filterLock)
            {
                return _enabledColors.OrderBy(name => name).ToList();
            }
        }
    }

    public void SetEnabledColors(IEnumerable<string> colors)
    {
        var valid = colors
            .Where(color => !string.IsNullOrWhiteSpace(color))
            .Select(color => color.Trim())
            .Where(color => Ranges.Any(range => range.Name.Equals(color, StringComparison.OrdinalIgnoreCase)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        lock (_filterLock)
        {
            _enabledColors = valid;
        }
    }

    public Task<IReadOnlyList<Detection>> DetectAsync(FramePacket frame, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var image = Cv2.ImDecode(frame.EncodedJpeg, ImreadModes.Color);
        cancellationToken.ThrowIfCancellationRequested();

        if (image.Empty())
        {
            return Task.FromResult<IReadOnlyList<Detection>>([]);
        }

        using var hsv = new Mat();
        Cv2.CvtColor(image, hsv, ColorConversionCodes.BGR2HSV);
        cancellationToken.ThrowIfCancellationRequested();

        var detections = new List<Detection>();

        HashSet<string> enabled;
        lock (_filterLock)
        {
            enabled = _enabledColors.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        foreach (var range in Ranges)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!enabled.Contains(range.Name))
            {
                continue;
            }

            using var mask = BuildMask(hsv, range);
            cancellationToken.ThrowIfCancellationRequested();

            Cv2.FindContours(mask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

            var index = 0;
            foreach (var contour in contours)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var area = Cv2.ContourArea(contour);
                var minArea = range.Name is "black" or "white" ? 240 : 120;
                if (area < minArea)
                {
                    continue;
                }

                var rect = Cv2.BoundingRect(contour);
                var moments = Cv2.Moments(contour);
                if (Math.Abs(moments.M00) < double.Epsilon)
                {
                    continue;
                }

                var centerX = (float)(moments.M10 / moments.M00);
                var centerY = (float)(moments.M01 / moments.M00);

                detections.Add(new Detection(
                    $"{range.Name}-{index}",
                    centerX,
                    centerY,
                    rect.X,
                    rect.Y,
                    rect.Width,
                    rect.Height,
                    Math.Min(1f, (float)(area / (frame.Width * frame.Height))),
                    range.Name,
                    frame.SourceId,
                    frame.TimestampUtcMs));

                index++;
            }
        }

        return Task.FromResult<IReadOnlyList<Detection>>(detections);
    }

    private static Mat BuildMask(Mat hsv, ColorRange range)
    {
        if (!range.HasSecondary)
        {
            var mask = new Mat();
            Cv2.InRange(hsv, range.Lower, range.Upper, mask);
            return mask;
        }

        var primary = new Mat();
        var secondary = new Mat();
        var combined = new Mat();

        Cv2.InRange(hsv, range.Lower, range.Upper, primary);
        Cv2.InRange(hsv, range.SecondaryLower!.Value, range.SecondaryUpper!.Value, secondary);
        Cv2.BitwiseOr(primary, secondary, combined);

        primary.Dispose();
        secondary.Dispose();
        return combined;
    }

    private sealed record ColorRange(
        string Name,
        Scalar Lower,
        Scalar Upper,
        Scalar? SecondaryLower = null,
        Scalar? SecondaryUpper = null)
    {
        public bool HasSecondary => SecondaryLower.HasValue && SecondaryUpper.HasValue;
    }
}