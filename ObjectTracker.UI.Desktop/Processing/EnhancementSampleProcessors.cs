using OpenCvSharp;

namespace ObjectTracker.UI.Desktop;

/// <summary>
/// Demonstrates Gaussian blur as a baseline image enhancement step.
/// </summary>
internal sealed class BlurSampleProcessor : IOpenCvSampleProcessor
{
    public OpenCvSampleMode Mode => OpenCvSampleMode.Blur;

    /// <summary>
    /// Applies Gaussian blur to the supplied image.
    /// </summary>
    /// <param name="source">The source image to blur.</param>
    /// <param name="sourceName">The display name of the source being processed.</param>
    /// <returns>The blurred image and a summary of the blur settings.</returns>
    public RecognitionResult Process(Mat source, string sourceName)
    {
        using var annotated = new Mat();
        // Apply the blur directly to the color image so the preview matches the original scene layout.
        Cv2.GaussianBlur(source, annotated, new Size(9, 9), 1.8);

        var details = new[]
        {
            "Kernel: 9x9",
            "Sigma: 1.8",
            $"Frame size: {source.Width}x{source.Height}"
        };

        return OpenCvSampleProcessingHelpers.CreateResult($"Processed {sourceName} with the blur sample.", details, annotated);
    }
}

/// <summary>
/// Demonstrates CLAHE-based local contrast enhancement on grayscale luminance.
/// </summary>
internal sealed class ClaheSampleProcessor : IOpenCvSampleProcessor
{
    public OpenCvSampleMode Mode => OpenCvSampleMode.Clahe;

    /// <summary>
    /// Applies CLAHE to the grayscale luminance representation of the source image.
    /// </summary>
    /// <param name="source">The source image to enhance.</param>
    /// <param name="sourceName">The display name of the source being processed.</param>
    /// <returns>The contrast-enhanced image and summary statistics.</returns>
    public RecognitionResult Process(Mat source, string sourceName)
    {
        using var gray = new Mat();
        using var enhanced = new Mat();
        using var annotated = new Mat();
        using var clahe = Cv2.CreateCLAHE();

        // CLAHE works on a single channel, so the sample converts to grayscale first.
        Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);
        clahe.ClipLimit = 20;
        clahe.TilesGridSize = new Size(8, 8);
        clahe.Apply(gray, enhanced);
        // Convert back to BGR so the UI can render the result consistently with other samples.
        Cv2.CvtColor(enhanced, annotated, ColorConversionCodes.GRAY2BGR);

        var details = new[]
        {
            "Clip limit: 20",
            "Tile grid: 8x8",
            $"Mean intensity: {Cv2.Mean(enhanced).Val0:F1}"
        };

        return OpenCvSampleProcessingHelpers.CreateResult($"Processed {sourceName} with the CLAHE sample.", details, annotated);
    }
}

/// <summary>
/// Demonstrates a classic Canny edge-detection pipeline.
/// </summary>
internal sealed class CannySampleProcessor : IOpenCvSampleProcessor
{
    public OpenCvSampleMode Mode => OpenCvSampleMode.Canny;

    /// <summary>
    /// Extracts edges from the supplied image using Canny thresholding.
    /// </summary>
    /// <param name="source">The source image to analyze.</param>
    /// <param name="sourceName">The display name of the source being processed.</param>
    /// <returns>The rendered edge map and basic edge statistics.</returns>
    public RecognitionResult Process(Mat source, string sourceName)
    {
        using var gray = new Mat();
        using var blurred = new Mat();
        using var edges = new Mat();
        using var annotated = new Mat();

        // A light blur reduces noise before edge extraction and cuts down on false positives.
        Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.GaussianBlur(gray, blurred, new Size(5, 5), 1.2);
        Cv2.Canny(blurred, edges, 60, 180);
        Cv2.CvtColor(edges, annotated, ColorConversionCodes.GRAY2BGR);

        var details = new List<string>
        {
            $"Edge pixels: {Cv2.CountNonZero(edges):N0}",
            "Thresholds: 60 / 180",
            $"Frame size: {source.Width}x{source.Height}"
        };

        return OpenCvSampleProcessingHelpers.CreateResult($"Processed {sourceName} with the Canny sample.", details, annotated);
    }
}

/// <summary>
/// Renders a grayscale intensity histogram instead of annotating the original frame.
/// </summary>
internal sealed class HistogramSampleProcessor : IOpenCvSampleProcessor
{
    public OpenCvSampleMode Mode => OpenCvSampleMode.Histogram;

    /// <summary>
    /// Builds a histogram visualization for the grayscale intensities of the source image.
    /// </summary>
    /// <param name="source">The source image to analyze.</param>
    /// <param name="sourceName">The display name of the source being processed.</param>
    /// <returns>A histogram preview and summary statistics for the input image.</returns>
    public RecognitionResult Process(Mat source, string sourceName)
    {
        using var gray = new Mat();
        Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);

        const int width = 260;
        const int height = 200;
        using var render = new Mat(new Size(width, height), MatType.CV_8UC3, Scalar.All(255));
        using var hist = new Mat();

        var dims = new[] { 256 };
        var ranges = new[] { new Rangef(0, 256) };
        Cv2.CalcHist([gray], [0], null, hist, 1, dims, ranges);
        Cv2.MinMaxLoc(hist, out double _, out double maxValue);

        // Draw the histogram as a polyline so each bin becomes visually inspectable in the preview.
        for (var i = 1; i < 256; i++)
        {
            var previous = hist.At<float>(i - 1);
            var current = hist.At<float>(i);
            var p1 = new Point(i - 1, height - Math.Round(previous / maxValue * height));
            var p2 = new Point(i, height - Math.Round(current / maxValue * height));
            Cv2.Line(render, p1, p2, new Scalar(32, 124, 229), 1, LineTypes.AntiAlias);
        }

        var details = new[]
        {
            $"Mean intensity: {Cv2.Mean(gray).Val0:F1}",
            $"StdDev: {OpenCvSampleProcessingHelpers.ComputeStdDev(gray):F1}",
            "Bins: 256"
        };

        return OpenCvSampleProcessingHelpers.CreateResult($"Processed {sourceName} with the histogram sample.", details, render);
    }
}

/// <summary>
/// Demonstrates binary thresholding followed by dilation with a small cross-shaped kernel.
/// </summary>
internal sealed class MorphologySampleProcessor : IOpenCvSampleProcessor
{
    public OpenCvSampleMode Mode => OpenCvSampleMode.Morphology;

    /// <summary>
    /// Applies thresholding and dilation to highlight foreground structures.
    /// </summary>
    /// <param name="source">The source image to analyze.</param>
    /// <param name="sourceName">The display name of the source being processed.</param>
    /// <returns>The dilated binary preview and summary statistics.</returns>
    public RecognitionResult Process(Mat source, string sourceName)
    {
        using var gray = new Mat();
        using var binary = new Mat();
        using var annotated = new Mat();

        byte[] kernelValues = [0, 1, 0, 1, 1, 1, 0, 1, 0];
        using var kernel = Mat.FromPixelData(3, 3, MatType.CV_8UC1, kernelValues);

        // Otsu chooses the threshold automatically, making the sample adapt to different lighting conditions.
        Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.Threshold(gray, binary, 0, 255, ThresholdTypes.Otsu);
        Cv2.Dilate(binary, annotated, kernel);
        Cv2.CvtColor(annotated, annotated, ColorConversionCodes.GRAY2BGR);

        var details = new[]
        {
            "Operation: dilate",
            "Kernel: 3x3 cross",
            $"Foreground pixels: {Cv2.CountNonZero(binary):N0}"
        };

        return OpenCvSampleProcessingHelpers.CreateResult($"Processed {sourceName} with the morphology sample.", details, annotated);
    }
}