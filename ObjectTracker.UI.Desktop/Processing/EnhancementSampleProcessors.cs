using OpenCvSharp;

namespace ObjectTracker.UI.Desktop;

/// <summary>
/// Demonstrates Gaussian blur as a baseline image enhancement step.
/// </summary>
internal sealed class BlurSampleProcessor : IOpenCvSampleProcessor
{
    private readonly SampleSetting _kernelSize = SampleSetting.Integer("blur-kernel", "Kernel", 9, 1, 31, 2, "Oneven kernelgrootte voor de Gaussian blur.");
    private readonly SampleSetting _sigma = SampleSetting.Decimal("blur-sigma", "Sigma", 1.8, 0.1, 10, 0.1, 1, "Hogere sigma geeft meer vervaging.");

    public OpenCvSampleMode Mode => OpenCvSampleMode.Blur;

    public IReadOnlyList<SampleSetting> Settings { get; }

    public BlurSampleProcessor()
    {
        Settings = [_kernelSize, _sigma];
    }

    /// <summary>
    /// Applies Gaussian blur to the supplied image.
    /// </summary>
    /// <param name="source">The source image to blur.</param>
    /// <param name="sourceName">The display name of the source being processed.</param>
    /// <returns>The blurred image and a summary of the blur settings.</returns>
    public RecognitionResult Process(Mat source, string sourceName)
    {
        var kernelSize = _kernelSize.IntValue;
        var sigma = _sigma.Value;
        using var annotated = new Mat();
        // Apply the blur directly to the color image so the preview matches the original scene layout.
        Cv2.GaussianBlur(source, annotated, new Size(kernelSize, kernelSize), sigma);

        var details = new[]
        {
            $"Kernel: {kernelSize}x{kernelSize}",
            $"Sigma: {sigma:F1}",
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
    private readonly SampleSetting _clipLimit = SampleSetting.Decimal("clahe-clip-limit", "Clip limit", 20, 1, 40, 0.5, 1, "Beperkt lokale contrastversterking.");
    private readonly SampleSetting _tileSize = SampleSetting.Integer("clahe-tile-size", "Tile size", 8, 2, 16, 1, "Aantal pixels per CLAHE-tile in beide richtingen.");

    public OpenCvSampleMode Mode => OpenCvSampleMode.Clahe;

    public IReadOnlyList<SampleSetting> Settings { get; }

    public ClaheSampleProcessor()
    {
        Settings = [_clipLimit, _tileSize];
    }

    /// <summary>
    /// Applies CLAHE to the grayscale luminance representation of the source image.
    /// </summary>
    /// <param name="source">The source image to enhance.</param>
    /// <param name="sourceName">The display name of the source being processed.</param>
    /// <returns>The contrast-enhanced image and summary statistics.</returns>
    public RecognitionResult Process(Mat source, string sourceName)
    {
        var clipLimit = _clipLimit.Value;
        var tileSize = _tileSize.IntValue;
        using var gray = new Mat();
        using var enhanced = new Mat();
        using var annotated = new Mat();
        using var clahe = Cv2.CreateCLAHE();

        // CLAHE works on a single channel, so the sample converts to grayscale first.
        Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);
        clahe.ClipLimit = clipLimit;
        clahe.TilesGridSize = new Size(tileSize, tileSize);
        clahe.Apply(gray, enhanced);
        // Convert back to BGR so the UI can render the result consistently with other samples.
        Cv2.CvtColor(enhanced, annotated, ColorConversionCodes.GRAY2BGR);

        var details = new[]
        {
            $"Clip limit: {clipLimit:F1}",
            $"Tile grid: {tileSize}x{tileSize}",
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
    private readonly SampleSetting _blurKernel = SampleSetting.Integer("canny-blur-kernel", "Blur kernel", 5, 1, 15, 2, "Oneven kernel om ruis te dempen voor edge detection.");
    private readonly SampleSetting _blurSigma = SampleSetting.Decimal("canny-blur-sigma", "Blur sigma", 1.2, 0, 5, 0.1, 1, "Sigma voor de pre-blur stap.");
    private readonly SampleSetting _lowThreshold = SampleSetting.Integer("canny-low-threshold", "Lower threshold", 60, 0, 255, 1, "Lage drempel voor de hysteresisstap.");
    private readonly SampleSetting _highThreshold = SampleSetting.Integer("canny-high-threshold", "Upper threshold", 180, 1, 255, 1, "Hoge drempel voor sterke edges.");

    public OpenCvSampleMode Mode => OpenCvSampleMode.Canny;

    public IReadOnlyList<SampleSetting> Settings { get; }

    public CannySampleProcessor()
    {
        Settings = [_blurKernel, _blurSigma, _lowThreshold, _highThreshold];
    }

    /// <summary>
    /// Extracts edges from the supplied image using Canny thresholding.
    /// </summary>
    /// <param name="source">The source image to analyze.</param>
    /// <param name="sourceName">The display name of the source being processed.</param>
    /// <returns>The rendered edge map and basic edge statistics.</returns>
    public RecognitionResult Process(Mat source, string sourceName)
    {
        var blurKernel = _blurKernel.IntValue;
        var blurSigma = _blurSigma.Value;
        var lowThreshold = Math.Min(_lowThreshold.Value, _highThreshold.Value - 1);
        var highThreshold = Math.Max(_highThreshold.Value, lowThreshold + 1);
        using var gray = new Mat();
        using var blurred = new Mat();
        using var edges = new Mat();
        using var annotated = new Mat();

        // A light blur reduces noise before edge extraction and cuts down on false positives.
        Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.GaussianBlur(gray, blurred, new Size(blurKernel, blurKernel), blurSigma);
        Cv2.Canny(blurred, edges, lowThreshold, highThreshold);
        Cv2.CvtColor(edges, annotated, ColorConversionCodes.GRAY2BGR);

        var details = new List<string>
        {
            $"Edge pixels: {Cv2.CountNonZero(edges):N0}",
            $"Thresholds: {lowThreshold:F0} / {highThreshold:F0}",
            $"Blur: {blurKernel}x{blurKernel}, sigma {blurSigma:F1}",
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
    private readonly SampleSetting _kernelSize = SampleSetting.Integer("morphology-kernel-size", "Kernel", 3, 3, 11, 2, "Oneven kernel voor de dilate-stap.");
    private readonly SampleSetting _iterations = SampleSetting.Integer("morphology-iterations", "Iterations", 1, 1, 5, 1, "Aantal dilate-iteraties.");

    public OpenCvSampleMode Mode => OpenCvSampleMode.Morphology;

    public IReadOnlyList<SampleSetting> Settings { get; }

    public MorphologySampleProcessor()
    {
        Settings = [_kernelSize, _iterations];
    }

    /// <summary>
    /// Applies thresholding and dilation to highlight foreground structures.
    /// </summary>
    /// <param name="source">The source image to analyze.</param>
    /// <param name="sourceName">The display name of the source being processed.</param>
    /// <returns>The dilated binary preview and summary statistics.</returns>
    public RecognitionResult Process(Mat source, string sourceName)
    {
        var kernelSize = _kernelSize.IntValue;
        var iterations = _iterations.IntValue;
        using var gray = new Mat();
        using var binary = new Mat();
        using var annotated = new Mat();
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Cross, new Size(kernelSize, kernelSize));

        // Otsu chooses the threshold automatically, making the sample adapt to different lighting conditions.
        Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.Threshold(gray, binary, 0, 255, ThresholdTypes.Otsu);
        Cv2.Dilate(binary, annotated, kernel, iterations: iterations);
        Cv2.CvtColor(annotated, annotated, ColorConversionCodes.GRAY2BGR);

        var details = new[]
        {
            "Operation: dilate",
            $"Kernel: {kernelSize}x{kernelSize} cross",
            $"Iterations: {iterations}",
            $"Foreground pixels: {Cv2.CountNonZero(binary):N0}"
        };

        return OpenCvSampleProcessingHelpers.CreateResult($"Processed {sourceName} with the morphology sample.", details, annotated);
    }
}