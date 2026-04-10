using OpenCvSharp;

namespace ObjectTracker.UI.Desktop;

internal static class OpenCvSampleProcessingHelpers
{
    public static RecognitionResult CreateResult(string status, IReadOnlyList<string> details, Mat annotated)
    {
        Cv2.ImEncode(".png", annotated, out var encoded);
        return new RecognitionResult(status, details, encoded);
    }

    public static double ComputeStdDev(Mat gray)
    {
        Cv2.MeanStdDev(gray, out _, out var stddev);
        return stddev.Val0;
    }

    public static string ClassifyShape(Point[] polygon, Rect bounds)
    {
        return polygon.Length switch
        {
            3 => "Triangle",
            4 => IsSquare(bounds) ? "Square" : "Rectangle",
            >= 8 => "Circle",
            _ => "Object"
        };
    }

    public static bool IsSquare(Rect bounds)
    {
        var ratio = bounds.Width / (double)Math.Max(1, bounds.Height);
        return ratio is > 0.85 and < 1.15;
    }
}