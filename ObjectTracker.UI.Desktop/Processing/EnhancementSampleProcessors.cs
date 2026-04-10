using OpenCvSharp;

namespace ObjectTracker.UI.Desktop;

internal sealed class BlurSampleProcessor : IOpenCvSampleProcessor
{
    public OpenCvSampleMode Mode => OpenCvSampleMode.Blur;

    public RecognitionResult Process(Mat source, string sourceName)
    {
        using var annotated = new Mat();
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

internal sealed class ClaheSampleProcessor : IOpenCvSampleProcessor
{
    public OpenCvSampleMode Mode => OpenCvSampleMode.Clahe;

    public RecognitionResult Process(Mat source, string sourceName)
    {
        using var gray = new Mat();
        using var enhanced = new Mat();
        using var annotated = new Mat();
        using var clahe = Cv2.CreateCLAHE();

        Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);
        clahe.ClipLimit = 20;
        clahe.TilesGridSize = new Size(8, 8);
        clahe.Apply(gray, enhanced);
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

internal sealed class CannySampleProcessor : IOpenCvSampleProcessor
{
    public OpenCvSampleMode Mode => OpenCvSampleMode.Canny;

    public RecognitionResult Process(Mat source, string sourceName)
    {
        using var gray = new Mat();
        using var blurred = new Mat();
        using var edges = new Mat();
        using var annotated = new Mat();

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

internal sealed class HistogramSampleProcessor : IOpenCvSampleProcessor
{
    public OpenCvSampleMode Mode => OpenCvSampleMode.Histogram;

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

internal sealed class MorphologySampleProcessor : IOpenCvSampleProcessor
{
    public OpenCvSampleMode Mode => OpenCvSampleMode.Morphology;

    public RecognitionResult Process(Mat source, string sourceName)
    {
        using var gray = new Mat();
        using var binary = new Mat();
        using var annotated = new Mat();

        byte[] kernelValues = [0, 1, 0, 1, 1, 1, 0, 1, 0];
        using var kernel = Mat.FromPixelData(3, 3, MatType.CV_8UC1, kernelValues);

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