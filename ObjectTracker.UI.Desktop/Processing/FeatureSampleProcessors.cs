using OpenCvSharp;

namespace ObjectTracker.UI.Desktop;

/// <summary>
/// Highlights FAST keypoints directly on top of the original frame.
/// </summary>
internal sealed class FastCornersSampleProcessor : IOpenCvSampleProcessor
{
    private readonly SampleSetting _threshold = SampleSetting.Integer("fast-threshold", "Threshold", 50, 1, 100, 1, "Minimale hoekrespons voor FAST.");

    public OpenCvSampleMode Mode => OpenCvSampleMode.FastCorners;

    public IReadOnlyList<SampleSetting> Settings { get; }

    public FastCornersSampleProcessor()
    {
        Settings = [_threshold];
    }

    /// <summary>
    /// Detects FAST corners and overlays them on the source image.
    /// </summary>
    /// <param name="source">The source image to analyze.</param>
    /// <param name="sourceName">The display name of the source being processed.</param>
    /// <returns>The annotated image and FAST detector statistics.</returns>
    public RecognitionResult Process(Mat source, string sourceName)
    {
        var threshold = _threshold.IntValue;
        using var gray = new Mat();
        using var annotated = source.Clone();

        Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);
        // The threshold controls how strong the corner response must be before a point is accepted.
        var keypoints = Cv2.FAST(gray, threshold, true);

        foreach (var keyPoint in keypoints)
        {
            annotated.Circle((Point)keyPoint.Pt, 3, Scalar.Red, -1, LineTypes.AntiAlias);
        }

        var details = new[]
        {
            $"Keypoints: {keypoints.Length}",
            $"Threshold: {threshold}",
            "Nonmax suppression: enabled"
        };

        return OpenCvSampleProcessingHelpers.CreateResult($"Processed {sourceName} with the FAST sample.", details, annotated);
    }
}

/// <summary>
/// Uses OpenCV's built-in HOG plus SVM pedestrian detector.
/// </summary>
internal sealed class HogPeopleSampleProcessor : IOpenCvSampleProcessor
{
    private readonly SampleSetting _hitThreshold = SampleSetting.Decimal("hog-hit-threshold", "Hit threshold", 0, -1, 2, 0.05, 2, "Hogere waarden maken de detector strenger.");
    private readonly SampleSetting _scaleFactor = SampleSetting.Decimal("hog-scale-factor", "Scale factor", 1.05, 1.01, 1.2, 0.01, 2, "Stapgrootte voor de beeldpiramide.");
    private readonly SampleSetting _groupThreshold = SampleSetting.Integer("hog-group-threshold", "Group threshold", 2, 0, 8, 1, "Hoeveel overlappende hits nodig zijn voor een definitieve detectie.");

    public OpenCvSampleMode Mode => OpenCvSampleMode.HogPeople;

    public IReadOnlyList<SampleSetting> Settings { get; }

    public HogPeopleSampleProcessor()
    {
        Settings = [_hitThreshold, _scaleFactor, _groupThreshold];
    }

    /// <summary>
    /// Detects person-like regions with the default OpenCV HOG detector.
    /// </summary>
    /// <param name="source">The source image to analyze.</param>
    /// <param name="sourceName">The display name of the source being processed.</param>
    /// <returns>The annotated image and pedestrian detection details.</returns>
    public RecognitionResult Process(Mat source, string sourceName)
    {
        var hitThreshold = _hitThreshold.Value;
        var scaleFactor = _scaleFactor.Value;
        var groupThreshold = _groupThreshold.IntValue;
        using var annotated = source.Clone();
        using var hog = new HOGDescriptor();
        hog.SetSVMDetector(HOGDescriptor.GetDefaultPeopleDetector());

        // DetectMultiScale searches the frame pyramid for person-like windows at multiple scales.
        var found = hog.DetectMultiScale(source, hitThreshold, new Size(8, 8), new Size(24, 16), scaleFactor, groupThreshold);
        var details = new List<string>
        {
            $"Detector size valid: {hog.CheckDetectorSize()}",
            $"Regions found: {found.Length}",
            $"Hit threshold: {hitThreshold:F2}, scale factor: {scaleFactor:F2}, group threshold: {groupThreshold}"
        };

        for (var index = 0; index < found.Length; index++)
        {
            var rect = found[index];
            // Shrink the raw rectangle slightly because the default detector often returns padded boxes.
            var adjusted = new Rect
            {
                X = rect.X + (int)Math.Round(rect.Width * 0.1),
                Y = rect.Y + (int)Math.Round(rect.Height * 0.1),
                Width = (int)Math.Round(rect.Width * 0.8),
                Height = (int)Math.Round(rect.Height * 0.8)
            };

            annotated.Rectangle(adjusted.TopLeft, adjusted.BottomRight, Scalar.Red, 3);

            if (index < 10)
            {
                details.Add($"Detection {index + 1}: {adjusted.Width}x{adjusted.Height} at ({adjusted.X}, {adjusted.Y})");
            }
        }

        if (found.Length > 10)
        {
            details.Add($"... plus {found.Length - 10} more detection(s)");
        }

        return OpenCvSampleProcessingHelpers.CreateResult($"Processed {sourceName} with the HOG sample.", details, annotated);
    }
}

/// <summary>
/// Extracts Maximally Stable Extremal Regions that often correspond to text-like or blob-like structures.
/// </summary>
internal sealed class MserSampleProcessor : IOpenCvSampleProcessor
{
    public OpenCvSampleMode Mode => OpenCvSampleMode.Mser;

    /// <summary>
    /// Detects MSER regions and overlays representative region points.
    /// </summary>
    /// <param name="source">The source image to analyze.</param>
    /// <param name="sourceName">The display name of the source being processed.</param>
    /// <returns>The annotated image and a summary of the detected regions.</returns>
    public RecognitionResult Process(Mat source, string sourceName)
    {
        using var gray = new Mat();
        using var annotated = source.Clone();
        using var detector = MSER.Create();

        Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);
        detector.DetectRegions(gray, out var contours, out _);

        // Draw a subset of points per region so dense regions remain legible in the preview.
        for (var i = 0; i < contours.Length; i++)
        {
            var color = Scalar.RandomColor();
            foreach (var point in contours[i].Take(250))
            {
                annotated.Circle(point, 1, color, -1);
            }
        }

        var details = new List<string>
        {
            $"Regions: {contours.Length}"
        };

        details.AddRange(contours
            .OrderByDescending(region => region.Length)
            .Take(8)
            .Select((region, index) => $"Region {index + 1}: {region.Length} point(s)"));

        return OpenCvSampleProcessingHelpers.CreateResult($"Processed {sourceName} with the MSER sample.", details, annotated);
    }
}

/// <summary>
/// Runs two differently tuned blob detectors to separate circular and elongated candidates.
/// </summary>
internal sealed class SimpleBlobDetectorSampleProcessor : IOpenCvSampleProcessor
{
    public OpenCvSampleMode Mode => OpenCvSampleMode.SimpleBlobDetector;

    /// <summary>
    /// Detects circular and oval-like blobs using two parameter sets.
    /// </summary>
    /// <param name="source">The source image to analyze.</param>
    /// <param name="sourceName">The display name of the source being processed.</param>
    /// <returns>The annotated image and the blob counts for each detector configuration.</returns>
    public RecognitionResult Process(Mat source, string sourceName)
    {
        using var working = source.Clone();
        using var annotated = new Mat();
        // Inverting the frame helps the detector when the objects of interest are darker than the background.
        Cv2.BitwiseNot(working, working);

        // Tight circularity and inertia filters bias this detector toward near-perfect circles.
        var circleParams = new SimpleBlobDetector.Params
        {
            MinThreshold = 10,
            MaxThreshold = 230,
            FilterByArea = true,
            MinArea = 500,
            MaxArea = 50000,
            FilterByCircularity = true,
            MinCircularity = 0.9f,
            FilterByConvexity = true,
            MinConvexity = 0.95f,
            FilterByInertia = true,
            MinInertiaRatio = 0.95f
        };

        // Looser inertia keeps elongated but still compact blobs such as ovals.
        var ovalParams = new SimpleBlobDetector.Params
        {
            MinThreshold = 10,
            MaxThreshold = 230,
            FilterByArea = true,
            MinArea = 500,
            MaxArea = 10000,
            FilterByCircularity = true,
            MinCircularity = 0.58f,
            FilterByConvexity = true,
            MinConvexity = 0.96f,
            FilterByInertia = true,
            MinInertiaRatio = 0.1f
        };

        using var circleDetector = SimpleBlobDetector.Create(circleParams);
        using var ovalDetector = SimpleBlobDetector.Create(ovalParams);

        var circles = circleDetector.Detect(working);
        var ovals = ovalDetector.Detect(working);

        Cv2.DrawKeypoints(working, circles, annotated, Scalar.HotPink, DrawMatchesFlags.DrawRichKeypoints);
        Cv2.DrawKeypoints(annotated, ovals, annotated, Scalar.LimeGreen, DrawMatchesFlags.DrawRichKeypoints);

        var details = new[]
        {
            $"Circle-like blobs: {circles.Length}",
            $"Oval-like blobs: {ovals.Length}",
            "Input inverted before detection"
        };

        return OpenCvSampleProcessingHelpers.CreateResult($"Processed {sourceName} with the simple blob detector sample.", details, annotated);
    }
}