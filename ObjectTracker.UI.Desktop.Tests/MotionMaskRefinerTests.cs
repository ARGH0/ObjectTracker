using OpenCvSharp;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class MotionMaskRefinerTests
{
    [Fact]
    public void Refine_ConnectsNearbyTrainFragments_WhenCloseFirstIsApplied()
    {
        using var sourceMask = new Mat(new Size(80, 50), MatType.CV_8UC1, Scalar.Black);
        Cv2.Rectangle(sourceMask, new Rect(10, 20, 12, 10), Scalar.White, -1);
        Cv2.Rectangle(sourceMask, new Rect(24, 20, 12, 10), Scalar.White, -1);

        var refiner = new MotionMaskRefiner();
        using var refined = refiner.Refine(sourceMask, new MotionMaskRefiner.Options(5, 1));

        Cv2.FindContours(refined, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        Assert.Single(contours);
    }

    [Fact]
    public void Refine_PreservesNoiseCleanup_WhenLightOpenIsEnabled()
    {
        using var sourceMask = new Mat(new Size(80, 50), MatType.CV_8UC1, Scalar.Black);
        Cv2.Rectangle(sourceMask, new Rect(10, 20, 18, 12), Scalar.White, -1);
        sourceMask.Set(4, 4, 255);

        var refiner = new MotionMaskRefiner();
        using var refined = refiner.Refine(sourceMask, new MotionMaskRefiner.Options(5, 3));

        Assert.Equal(0, refined.At<byte>(4, 4));
    }
}
