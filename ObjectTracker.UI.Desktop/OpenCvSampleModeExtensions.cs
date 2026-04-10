namespace ObjectTracker.UI.Desktop;

/// <summary>
/// Provides UI-friendly metadata for <see cref="OpenCvSampleMode"/> values.
/// </summary>
internal static class OpenCvSampleModeExtensions
{
    /// <summary>
    /// Converts a sample mode into the label shown in the UI.
    /// </summary>
    /// <param name="mode">The sample mode to describe.</param>
    /// <returns>A human-readable display name for the sample mode.</returns>
    public static string ToDisplayName(this OpenCvSampleMode mode)
    {
        return mode switch
        {
            OpenCvSampleMode.BackgroundTracking => "Background Tracking",
            OpenCvSampleMode.TrainCollisionRisk => "Train Collision Risk",
            OpenCvSampleMode.Blur => "Gaussian Blur",
            OpenCvSampleMode.Clahe => "CLAHE",
            OpenCvSampleMode.Contours => "Contours",
            OpenCvSampleMode.Canny => "Canny Edges",
            OpenCvSampleMode.ConnectedComponents => "Connected Components",
            OpenCvSampleMode.FastCorners => "FAST Corners",
            OpenCvSampleMode.Histogram => "Histogram",
            OpenCvSampleMode.HogPeople => "HOG People",
            OpenCvSampleMode.HoughLines => "Hough Lines",
            OpenCvSampleMode.Morphology => "Morphology",
            OpenCvSampleMode.Mser => "MSER",
            OpenCvSampleMode.Yolo => "YOLO DNN",
            OpenCvSampleMode.SimpleBlobDetector => "Simple Blob Detector",
            _ => mode.ToString()
        };
    }

    /// <summary>
    /// Returns a short explanation of what the selected sample demonstrates.
    /// </summary>
    /// <param name="mode">The sample mode to describe.</param>
    /// <returns>A user-facing description of the sample.</returns>
    public static string GetDescription(this OpenCvSampleMode mode)
    {
        return mode switch
        {
            OpenCvSampleMode.BackgroundTracking => "Stateful motion tracking with MOG2 foreground extraction, contour boxes, and persistent object IDs.",
            OpenCvSampleMode.TrainCollisionRisk => "Fixed-camera train tracking with short-horizon motion prediction and collision risk estimation.",
            OpenCvSampleMode.Blur => "Blur sample from the repo family: smooths the frame with a Gaussian kernel.",
            OpenCvSampleMode.Clahe => "Contrast Limited Adaptive Histogram Equalization on grayscale luminance.",
            OpenCvSampleMode.Contours => "Contour extraction with polygon approximation and shape-style labeling.",
            OpenCvSampleMode.Canny => "Edge map rendering using classic Canny thresholds.",
            OpenCvSampleMode.ConnectedComponents => "Threshold, label, and outline connected blobs with bounding boxes.",
            OpenCvSampleMode.FastCorners => "FAST keypoint detector drawn directly onto the frame.",
            OpenCvSampleMode.Histogram => "Intensity histogram visualization similar to the upstream histogram sample.",
            OpenCvSampleMode.HogPeople => "Default HOG person detector using OpenCV's built-in SVM weights.",
            OpenCvSampleMode.HoughLines => "Probabilistic Hough transform over Canny edges.",
            OpenCvSampleMode.Morphology => "Threshold and dilation preview using a cross-shaped structuring element.",
            OpenCvSampleMode.Mser => "MSER region extraction with per-region overlays.",
            OpenCvSampleMode.Yolo => "OpenCV DNN inference over an ONNX YOLO model from the local Models folder, with optional class labels.",
            OpenCvSampleMode.SimpleBlobDetector => "SimpleBlobDetector tuned to highlight circular and oval-like blobs.",
            _ => mode.ToString()
        };
    }
}
