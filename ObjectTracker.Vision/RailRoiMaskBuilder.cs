using OpenCvSharp;

namespace ObjectTracker.Vision;

public sealed class RailRoiMaskBuilder
{
    public readonly record struct Options(
        int CorridorKernelSize,
        double MinCoverageRatio,
        double MaxCoverageRatio)
    {
        public static Options Default => new(31, 0.01, 0.85);
    }

    public Mat BuildFromBackground(Mat grayscaleBackground, Options? options = null)
    {
        if (grayscaleBackground.Empty())
        {
            throw new ArgumentException("Background image is empty.", nameof(grayscaleBackground));
        }

        if (grayscaleBackground.Type() != MatType.CV_8UC1)
        {
            throw new ArgumentException("Background image must be grayscale (CV_8UC1).", nameof(grayscaleBackground));
        }

        var active = options ?? Options.Default;

        using var blurred = new Mat();
        using var railSeed = new Mat();
        using var closed = new Mat();
        var roiMask = new Mat();

        Cv2.GaussianBlur(grayscaleBackground, blurred, new Size(5, 5), 0);
        Cv2.Threshold(blurred, railSeed, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);

        using var closeKernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
        Cv2.MorphologyEx(railSeed, closed, MorphTypes.Close, closeKernel);

        var corridorKernelSize = EnsureOddAtLeastOne(active.CorridorKernelSize);
        using var corridorKernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(corridorKernelSize, corridorKernelSize));
        Cv2.Dilate(closed, roiMask, corridorKernel);

        var coverage = Coverage(roiMask);
        if (coverage < active.MinCoverageRatio || coverage > active.MaxCoverageRatio)
        {
            roiMask.SetTo(Scalar.All(255));
        }

        return roiMask;
    }

    private static int EnsureOddAtLeastOne(int value)
    {
        var clamped = Math.Max(1, value);
        return clamped % 2 == 0 ? clamped + 1 : clamped;
    }

    private static double Coverage(Mat mask)
    {
        var nonZero = Cv2.CountNonZero(mask);
        var total = mask.Rows * mask.Cols;
        return total == 0 ? 0 : (double)nonZero / total;
    }
}
