using OpenCvSharp;

namespace ObjectTracker.UI.Desktop;

/// <summary>
/// Extracts large external contours and labels them with a simple polygon-based shape heuristic.
/// </summary>
internal sealed class ContoursSampleProcessor : IOpenCvSampleProcessor
{
    private readonly SampleSetting _blurKernel = SampleSetting.Integer("contours-blur-kernel", "Blur kernel", 5, 1, 15, 2, "Oneven kernel voor het gladstrijken voor Canny.");
    private readonly SampleSetting _cannyLow = SampleSetting.Integer("contours-canny-low", "Canny lower", 60, 0, 255, 1, "Lage Canny-drempel.");
    private readonly SampleSetting _cannyHigh = SampleSetting.Integer("contours-canny-high", "Canny upper", 180, 1, 255, 1, "Hoge Canny-drempel.");
    private readonly SampleSetting _minArea = SampleSetting.Integer("contours-min-area", "Min area", 1500, 100, 20000, 100, "Contouren kleiner dan deze oppervlakte worden genegeerd.");
    private readonly SampleSetting _approximationFactor = SampleSetting.Decimal("contours-approx-factor", "Approx factor", 0.03, 0.01, 0.1, 0.01, 2, "Factor voor polygon-approximation op basis van omtrek.");

    public OpenCvSampleMode Mode => OpenCvSampleMode.Contours;

    public IReadOnlyList<SampleSetting> Settings { get; }

    public ContoursSampleProcessor()
    {
        Settings = [_blurKernel, _cannyLow, _cannyHigh, _minArea, _approximationFactor];
    }

    /// <summary>
    /// Detects large contours and overlays coarse shape labels on the source image.
    /// </summary>
    /// <param name="source">The source image to analyze.</param>
    /// <param name="sourceName">The display name of the source being processed.</param>
    /// <returns>The annotated image and a summary of the retained contour candidates.</returns>
    public RecognitionResult Process(Mat source, string sourceName)
    {
        var blurKernel = _blurKernel.IntValue;
        var lowThreshold = Math.Min(_cannyLow.Value, _cannyHigh.Value - 1);
        var highThreshold = Math.Max(_cannyHigh.Value, lowThreshold + 1);
        var minArea = _minArea.Value;
        var approximationFactor = _approximationFactor.Value;
        using var annotated = source.Clone();
        using var gray = new Mat();
        using var blurred = new Mat();
        using var edges = new Mat();

        // The contour sample intentionally uses a straightforward edge pipeline so the intermediate logic stays teachable.
        Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.GaussianBlur(gray, blurred, new Size(blurKernel, blurKernel), 0);
        Cv2.Canny(blurred, edges, lowThreshold, highThreshold);

        Cv2.FindContours(edges, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        var details = new List<string>();
        var index = 1;

        foreach (var contour in contours.OrderByDescending(candidate => Cv2.ContourArea(candidate)))
        {
            var area = Cv2.ContourArea(contour);
            // Small contours usually come from texture or noise instead of meaningful objects.
            if (area < minArea)
            {
                continue;
            }

            var perimeter = Cv2.ArcLength(contour, true);
            if (perimeter <= 0)
            {
                continue;
            }

            var polygon = Cv2.ApproxPolyDP(contour, approximationFactor * perimeter, true);
            var bounds = Cv2.BoundingRect(polygon);
            if (bounds.Width < 30 || bounds.Height < 30)
            {
                continue;
            }

            var label = OpenCvSampleProcessingHelpers.ClassifyShape(polygon, bounds);
            details.Add($"{label} {index}: {bounds.Width}x{bounds.Height} at ({bounds.X}, {bounds.Y})");

            Cv2.Rectangle(annotated, bounds, new Scalar(32, 124, 229), 3);
            Cv2.PutText(
                annotated,
                label,
                new Point(bounds.X, Math.Max(24, bounds.Y - 8)),
                HersheyFonts.HersheySimplex,
                0.8,
                new Scalar(32, 124, 229),
                2,
                LineTypes.AntiAlias);

            index++;
        }

        var status = details.Count == 0
            ? $"Processed {sourceName} with the contour sample. No large contour-based objects were detected."
            : $"Processed {sourceName} with the contour sample. Detected {details.Count} object(s).";

        details.Insert(0, $"Canny thresholds: {lowThreshold:F0} / {highThreshold:F0}");
        details.Insert(1, $"Min area: {minArea:F0}");

        return OpenCvSampleProcessingHelpers.CreateResult(status, details, annotated);
    }
}

/// <summary>
/// Labels each connected foreground blob after Otsu thresholding.
/// </summary>
internal sealed class ConnectedComponentsSampleProcessor : IOpenCvSampleProcessor
{
    public OpenCvSampleMode Mode => OpenCvSampleMode.ConnectedComponents;

    /// <summary>
    /// Detects connected foreground components and draws their bounding boxes.
    /// </summary>
    /// <param name="source">The source image to analyze.</param>
    /// <param name="sourceName">The display name of the source being processed.</param>
    /// <returns>The annotated image and statistics for the labeled blobs.</returns>
    public RecognitionResult Process(Mat source, string sourceName)
    {
        using var gray = source.CvtColor(ColorConversionCodes.BGR2GRAY);
        using var binary = gray.Threshold(0, 255, ThresholdTypes.Otsu | ThresholdTypes.Binary);
        using var annotated = binary.CvtColor(ColorConversionCodes.GRAY2BGR);

        // Label 0 is the background, so only the remaining blobs represent candidate objects.
        var cc = Cv2.ConnectedComponentsEx(binary);
        if (cc.LabelCount <= 1)
        {
            return OpenCvSampleProcessingHelpers.CreateResult(
                $"Processed {sourceName} with the connected components sample. No foreground blobs survived thresholding.",
                new[] { "Label count: 0" },
                annotated);
        }

        var blobs = cc.Blobs.Skip(1).ToArray();
        foreach (var blob in blobs)
        {
            annotated.Rectangle(blob.Rect, Scalar.Red, 2);
        }

        var largestBlob = blobs.OrderByDescending(blob => blob.Area).First();
        var details = blobs
            .OrderByDescending(blob => blob.Area)
            .Take(10)
            .Select((blob, index) => $"Blob {index + 1}: area {blob.Area}, rect {blob.Rect.Width}x{blob.Rect.Height} at ({blob.Rect.X}, {blob.Rect.Y})")
            .ToList();

        if (blobs.Length > 10)
        {
            details.Add($"... plus {blobs.Length - 10} more blob(s)");
        }

        details.Insert(0, $"Largest area: {largestBlob.Area}");
        details.Insert(1, $"Label count: {blobs.Length}");

        return OpenCvSampleProcessingHelpers.CreateResult($"Processed {sourceName} with the connected components sample.", details, annotated);
    }
}

/// <summary>
/// Uses the probabilistic Hough transform to extract visible line segments.
/// </summary>
internal sealed class HoughLinesSampleProcessor : IOpenCvSampleProcessor
{
    private readonly SampleSetting _cannyLow = SampleSetting.Integer("hough-canny-low", "Canny lower", 50, 0, 255, 1, "Lage drempel voor de edge map.");
    private readonly SampleSetting _cannyHigh = SampleSetting.Integer("hough-canny-high", "Canny upper", 200, 1, 255, 1, "Hoge drempel voor de edge map.");
    private readonly SampleSetting _houghThreshold = SampleSetting.Integer("hough-threshold", "Accumulator threshold", 60, 1, 200, 1, "Minimum aantal votes voor een lijnsegment.");
    private readonly SampleSetting _minLineLength = SampleSetting.Integer("hough-min-line-length", "Min line length", 40, 1, 400, 1, "Minimum lengte van een lijnsegment.");
    private readonly SampleSetting _maxLineGap = SampleSetting.Integer("hough-max-line-gap", "Max line gap", 12, 0, 100, 1, "Maximale afstand tussen segmenten die worden samengevoegd.");

    public OpenCvSampleMode Mode => OpenCvSampleMode.HoughLines;

    public IReadOnlyList<SampleSetting> Settings { get; }

    public HoughLinesSampleProcessor()
    {
        Settings = [_cannyLow, _cannyHigh, _houghThreshold, _minLineLength, _maxLineGap];
    }

    /// <summary>
    /// Detects prominent line segments and overlays them on the source image.
    /// </summary>
    /// <param name="source">The source image to analyze.</param>
    /// <param name="sourceName">The display name of the source being processed.</param>
    /// <returns>The annotated image and a summary of the detected line segments.</returns>
    public RecognitionResult Process(Mat source, string sourceName)
    {
        var lowThreshold = Math.Min(_cannyLow.Value, _cannyHigh.Value - 1);
        var highThreshold = Math.Max(_cannyHigh.Value, lowThreshold + 1);
        var houghThreshold = _houghThreshold.IntValue;
        var minLineLength = _minLineLength.IntValue;
        var maxLineGap = _maxLineGap.IntValue;
        using var annotated = source.Clone();
        using var gray = new Mat();
        using var edges = new Mat();

        // Canny edges provide the sparse binary input expected by the Hough transform.
        Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.Canny(gray, edges, lowThreshold, highThreshold, 3, false);

        var lines = Cv2.HoughLinesP(edges, 1, Math.PI / 180, houghThreshold, minLineLength, maxLineGap);
        var details = new List<string>();

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var start = new Point(line.P1.X, line.P1.Y);
            var end = new Point(line.P2.X, line.P2.Y);
            var length = Math.Sqrt(Math.Pow(line.P1.X - line.P2.X, 2) + Math.Pow(line.P1.Y - line.P2.Y, 2));

            Cv2.Line(annotated, start, end, new Scalar(216, 78, 61), 3, LineTypes.AntiAlias);

            if (index < 12)
            {
                details.Add($"Line {index + 1}: ({line.P1.X}, {line.P1.Y}) -> ({line.P2.X}, {line.P2.Y}), len {length:F1}");
            }
        }

        if (lines.Length > 12)
        {
            details.Add($"... plus {lines.Length - 12} more line segment(s)");
        }

        var status = lines.Length == 0
            ? $"Processed {sourceName} with the Hough line sample. No strong line segments were found."
            : $"Processed {sourceName} with the Hough line sample. Found {lines.Length} line segment(s).";

        details.Insert(0, $"Canny thresholds: {lowThreshold:F0} / {highThreshold:F0}");
        details.Insert(1, $"Hough threshold: {houghThreshold}, min length {minLineLength}, max gap {maxLineGap}");

        return OpenCvSampleProcessingHelpers.CreateResult(status, details, annotated);
    }
}