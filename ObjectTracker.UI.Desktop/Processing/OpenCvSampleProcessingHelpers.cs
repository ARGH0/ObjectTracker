using OpenCvSharp;

namespace ObjectTracker.UI.Desktop;

/// <summary>
/// Provides helper methods shared by multiple sample processors.
/// </summary>
internal static class OpenCvSampleProcessingHelpers
{
    /// <summary>
    /// Creates a UI-ready recognition result from an annotated OpenCV image.
    /// </summary>
    /// <param name="status">The high-level status message for the processed source.</param>
    /// <param name="details">The detail lines to display in the UI.</param>
    /// <param name="annotated">The annotated image to encode for preview.</param>
    /// <returns>A recognition result containing encoded image bytes and textual diagnostics.</returns>
    public static RecognitionResult CreateResult(string status, IReadOnlyList<string> details, Mat annotated)
    {
        // The UI preview consumes encoded image bytes, so processors can stay OpenCV-centric.
        Cv2.ImEncode(".png", annotated, out var encoded);
        return new RecognitionResult(status, details, encoded);
    }

    /// <summary>
    /// Computes the grayscale standard deviation for the supplied image.
    /// </summary>
    /// <param name="gray">The grayscale image to measure.</param>
    /// <returns>The standard deviation of the grayscale intensities.</returns>
    public static double ComputeStdDev(Mat gray)
    {
        // Standard deviation is a compact way to describe contrast spread in grayscale samples.
        Cv2.MeanStdDev(gray, out _, out var stddev);
        return stddev.Val0;
    }

    /// <summary>
    /// Classifies a polygon approximation into a coarse shape label.
    /// </summary>
    /// <param name="polygon">The approximated contour polygon.</param>
    /// <param name="bounds">The bounding rectangle of the polygon.</param>
    /// <returns>A best-effort shape label for the contour.</returns>
    public static string ClassifyShape(Point[] polygon, Rect bounds)
    {
        // This intentionally stays heuristic-based because the contour sample is demonstrational, not a full classifier.
        return polygon.Length switch
        {
            3 => "Triangle",
            4 => IsSquare(bounds) ? "Square" : "Rectangle",
            >= 8 => "Circle",
            _ => "Object"
        };
    }

    /// <summary>
    /// Determines whether the supplied bounding rectangle is close to square.
    /// </summary>
    /// <param name="bounds">The rectangle to inspect.</param>
    /// <returns><see langword="true"/> when the rectangle aspect ratio is near 1:1; otherwise, <see langword="false"/>.</returns>
    public static bool IsSquare(Rect bounds)
    {
        var ratio = bounds.Width / (double)Math.Max(1, bounds.Height);
        return ratio is > 0.85 and < 1.15;
    }
}