using ObjectTracker.Core.Domain;
using ObjectTracker.Core.Ports;
using OpenCvSharp;

namespace ObjectTracker.Vision;

public sealed class OpenCvColorDetector : IDetectionAlgorithm, IColorFilterControl
{
    public DetectorMode Mode => DetectorMode.Color;

    public string Name => "OpenCV Color";

    private readonly Lock filterLock = new ();

    private Dictionary<string, ColorRange> rangesByName = CreateDefaultRanges();
    private HashSet<string> enabledColors = CreateDefaultProfiles()
        .Select(profile => profile.Name)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> AvailableColors
    {
        get
        {
            lock (filterLock)
            {
                return rangesByName.Keys.Order().ToList();
            }
        }
    }

    public IReadOnlyList<string> EnabledColors
    {
        get
        {
            lock (filterLock)
            {
                return enabledColors.Order().ToList();
            }
        }
    }

    public IReadOnlyList<ColorCalibrationProfile> ColorCalibrations
    {
        get
        {
            lock (filterLock)
            {
                return rangesByName.Values
                    .Select(ToProfile)
                    .OrderBy(profile => profile.Name)
                    .ToList();
            }
        }
    }

    public void SetEnabledColors(IEnumerable<string> colors)
    {
        lock (filterLock)
        {
            var valid = colors
                .Where(color => !string.IsNullOrWhiteSpace(color))
                .Select(color => color.Trim())
                .Where(color => rangesByName.ContainsKey(color))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            enabledColors = valid;
        }
    }

    public void SetColorCalibrations(IEnumerable<ColorCalibrationProfile> calibrations)
    {
        var normalized = calibrations
            .Where(calibration => !string.IsNullOrWhiteSpace(calibration.Name))
            .Select(Normalize)
            .GroupBy(calibration => calibration.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToList();

        if (normalized.Count == 0)
        {
            return;
        }

        lock (filterLock)
        {
            rangesByName = normalized.ToDictionary(
                calibration => calibration.Name,
                calibration => ToColorRange(calibration),
                StringComparer.OrdinalIgnoreCase);

            enabledColors = enabledColors
                .Where(name => rangesByName.ContainsKey(name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (enabledColors.Count == 0)
            {
                enabledColors = rangesByName.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
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
        IReadOnlyList<ColorRange> ranges;
        lock (filterLock)
        {
            enabled = enabledColors.ToHashSet(StringComparer.OrdinalIgnoreCase);
            ranges = rangesByName.Values.ToList();
        }

        foreach (var range in ranges)
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
                var minArea = GetMinArea(range.Name);
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

    private static int GetMinArea(string colorName)
    {
        return colorName is "black" or "white" ? 240 : 120;
    }

    private static Dictionary<string, ColorRange> CreateDefaultRanges()
    {
        return CreateDefaultProfiles().ToDictionary(
            profile => profile.Name,
            profile => ToColorRange(profile),
            StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<ColorCalibrationProfile> CreateDefaultProfiles()
    {
        return new List<ColorCalibrationProfile>
        {
            new ("red", 170, 10, 120, 255, 70, 255),
            new ("orange", 10, 20, 120, 255, 80, 255),
            new ("pink", 145, 169, 70, 255, 80, 255),
            new ("purple", 130, 150, 70, 255, 60, 255),
            new ("green", 35, 85, 80, 255, 60, 255),
            new ("blue", 90, 130, 100, 255, 60, 255),
            new ("cyan", 80, 95, 70, 255, 70, 255),
            new ("yellow", 20, 35, 110, 255, 80, 255),
            new ("white", 0, 180, 0, 50, 190, 255),
            new ("black", 0, 180, 0, 255, 0, 45)
        };
    }

    private static ColorCalibrationProfile Normalize(ColorCalibrationProfile profile)
    {
        var normalizedName = profile.Name.Trim().ToLowerInvariant();
        return new ColorCalibrationProfile(
            normalizedName,
            Math.Clamp(profile.HueLower, 0, 180),
            Math.Clamp(profile.HueUpper, 0, 180),
            Math.Clamp(profile.SaturationLower, 0, 255),
            Math.Clamp(profile.SaturationUpper, 0, 255),
            Math.Clamp(profile.ValueLower, 0, 255),
            Math.Clamp(profile.ValueUpper, 0, 255));
    }

    private static ColorRange ToColorRange(ColorCalibrationProfile profile)
    {
        var normalized = Normalize(profile);
        var lower = new Scalar(normalized.HueLower, normalized.SaturationLower, normalized.ValueLower);
        var upper = new Scalar(normalized.HueUpper, normalized.SaturationUpper, normalized.ValueUpper);

        if (normalized.HueLower <= normalized.HueUpper)
        {
            return new ColorRange(normalized.Name, lower, upper);
        }

        var secondaryLower = new Scalar(0, normalized.SaturationLower, normalized.ValueLower);
        var secondaryUpper = new Scalar(normalized.HueUpper, normalized.SaturationUpper, normalized.ValueUpper);
        return new ColorRange(
            normalized.Name,
            lower,
            new Scalar(180, normalized.SaturationUpper, normalized.ValueUpper),
            secondaryLower,
            secondaryUpper);
    }

    private static ColorCalibrationProfile ToProfile(ColorRange range)
    {
        if (!range.HasSecondary)
        {
            return new ColorCalibrationProfile(
                range.Name,
                (int)range.Lower.Val0,
                (int)range.Upper.Val0,
                (int)range.Lower.Val1,
                (int)range.Upper.Val1,
                (int)range.Lower.Val2,
                (int)range.Upper.Val2);
        }

        return new ColorCalibrationProfile(
            range.Name,
            (int)range.Lower.Val0,
            (int)range.SecondaryUpper!.Value.Val0,
            (int)range.Lower.Val1,
            (int)range.Upper.Val1,
            (int)range.Lower.Val2,
            (int)range.Upper.Val2);
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
