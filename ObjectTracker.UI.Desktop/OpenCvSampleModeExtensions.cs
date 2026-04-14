namespace ObjectTracker.UI.Desktop;

internal static class OpenCvSampleModeExtensions
{
    public static string ToDisplayName(this OpenCvSampleMode mode)
    {
        return mode switch
        {
            OpenCvSampleMode.BackgroundTracking => "Background Tracking",
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
            OpenCvSampleMode.SimpleBlobDetector => "Simple Blob Detector",
            _ => mode.ToString()
        };
    }

    public static string GetDescription(this OpenCvSampleMode mode)
    {
        return mode switch
        {
            OpenCvSampleMode.BackgroundTracking => "Stateful motion tracking with MOG2 foreground extraction, contour boxes, and persistent object IDs.",
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
            OpenCvSampleMode.SimpleBlobDetector => "SimpleBlobDetector tuned to highlight circular and oval-like blobs.",
            _ => mode.ToString()
        };
    }
}
