using OpenCvSharp;

namespace ObjectTracker.UI.Desktop;

internal sealed class ContoursSampleProcessor : IOpenCvSampleProcessor
{
    public OpenCvSampleMode Mode => OpenCvSampleMode.Contours;

    public RecognitionResult Process(Mat source, string sourceName)
    {
        using var annotated = source.Clone();
        using var gray = new Mat();
        using var blurred = new Mat();
        using var edges = new Mat();

        Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.GaussianBlur(gray, blurred, new Size(5, 5), 0);
        Cv2.Canny(blurred, edges, 60, 180);

        Cv2.FindContours(edges, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        var details = new List<string>();
        var index = 1;

        foreach (var contour in contours.OrderByDescending(candidate => Cv2.ContourArea(candidate)))
        {
            var area = Cv2.ContourArea(contour);
            if (area < 1500)
            {
                continue;
            }

            var perimeter = Cv2.ArcLength(contour, true);
            if (perimeter <= 0)
            {
                continue;
            }

            var polygon = Cv2.ApproxPolyDP(contour, 0.03 * perimeter, true);
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

        return OpenCvSampleProcessingHelpers.CreateResult(status, details, annotated);
    }
}

internal sealed class ConnectedComponentsSampleProcessor : IOpenCvSampleProcessor
{
    public OpenCvSampleMode Mode => OpenCvSampleMode.ConnectedComponents;

    public RecognitionResult Process(Mat source, string sourceName)
    {
        using var gray = source.CvtColor(ColorConversionCodes.BGR2GRAY);
        using var binary = gray.Threshold(0, 255, ThresholdTypes.Otsu | ThresholdTypes.Binary);
        using var annotated = binary.CvtColor(ColorConversionCodes.GRAY2BGR);

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

internal sealed class HoughLinesSampleProcessor : IOpenCvSampleProcessor
{
    public OpenCvSampleMode Mode => OpenCvSampleMode.HoughLines;

    public RecognitionResult Process(Mat source, string sourceName)
    {
        using var annotated = source.Clone();
        using var gray = new Mat();
        using var edges = new Mat();

        Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.Canny(gray, edges, 50, 200, 3, false);

        var lines = Cv2.HoughLinesP(edges, 1, Math.PI / 180, 60, 40, 12);
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

        return OpenCvSampleProcessingHelpers.CreateResult(status, details, annotated);
    }
}