using OpenCvSharp;
using Xunit;

namespace ObjectTracker.Vision.Tests;

public sealed class MotionMaskRefinerTests
{
    /// <summary>
    /// <description>Feature: MotionMaskRefiner connects nearby train fragments using morphological operations.
    /// 
    ///   Scenario: Two adjacent white rectangles on a black background are merged into one contour.
    ///     Given an 80x50 source mask with two white rectangles (12x10 each) at positions (10,20) and (24,20), separated by a gap of 2 pixels,
    ///      And a MotionMaskRefiner configured with kernel size 5 and light open disabled,
    ///     When Refine is called on the source mask,
    ///     Then the output should contain exactly one contour.</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: MotionMaskRefiner preserves noise cleanup when light open is enabled.
    /// 
    ///   Scenario: Small isolated pixels outside the main mask region are removed while the main blob stays intact.
    ///     Given an 80x50 source mask with one white rectangle (18x12) at position (10,20),
    ///      And a single white pixel set at coordinates (4,4),
    ///      And a MotionMaskRefiner configured with kernel size 5 and light open enabled (size 3),
    ///     When Refine is called on the source mask,
    ///     Then the pixel at (4,4) should have value 0 (removed).</description>
    /// </summary>
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
