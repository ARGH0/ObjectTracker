using OpenCvSharp;
using Xunit;

namespace ObjectTracker.Vision.Tests;

public sealed class RailRoiMaskBuilderTests
{
    /// <summary>
    /// <description>Feature: RailRoiMaskBuilder builds a corridor mask from a rail-like background image.
    /// 
    ///   Scenario: A 100x100 grayscale image with a horizontal line seed produces an ROI mask covering more than 5% but less than 95% of the frame.
    ///     Given a 100x100 background Mat filled with value 230 (light gray),
    ///      And a white line drawn from (10,50) to (90,50) representing the rail seed,
    ///      And RailRoiMaskBuilder configured with Options(kernelSize=21, minCoverage=0.01, maxCoverage=0.95),
    ///     When BuildFromBackground is called on the background image,
    ///     Then the ROI mask coverage (non-zero pixels / total pixels) should be greater than 5%,
    ///      And the ROI mask coverage should be less than 95%.</description>
    /// </summary>
    [Fact]
    public void BuildFromBackground_ReturnsCorridorMask_ForRailLikeBackground()
    {
        using var background = new Mat(new Size(100, 100), MatType.CV_8UC1, Scalar.All(230));
        Cv2.Line(background, new Point(10, 50), new Point(90, 50), Scalar.All(20), 2);

        var builder = new RailRoiMaskBuilder();
        using var roiMask = builder.BuildFromBackground(background, new RailRoiMaskBuilder.Options(21, 0.01, 0.95));

        var coverage = (double)Cv2.CountNonZero(roiMask) / (roiMask.Rows * roiMask.Cols);
        Assert.True(coverage > 0.05, $"Expected coverage above 5%, got {coverage:P2}");
        Assert.True(coverage < 0.95, $"Expected coverage below 95%, got {coverage:P2}");
    }

    /// <summary>
    /// <description>Feature: RailRoiMaskBuilder falls back to full-frame mask when no rail seed is found in the background.
    /// 
    ///   Scenario: A uniform grayscale image without any distinguishing features produces a fully white ROI mask covering every pixel.
    ///     Given an 80x80 background Mat filled with value 128 (uniform gray, no rail structure),
    ///      And RailRoiMaskBuilder configured with Options(kernelSize=21, minCoverage=0.01, maxCoverage=0.85),
    ///     When BuildFromBackground is called on the background image,
    ///     Then every pixel in the ROI mask should be non-zero (full-frame coverage).</description>
    /// </summary>
    [Fact]
    public void BuildFromBackground_FallsBackToFullFrame_WhenNoRailSeedIsFound()
    {
        using var background = new Mat(new Size(80, 80), MatType.CV_8UC1, Scalar.All(128));

        var builder = new RailRoiMaskBuilder();
        using var roiMask = builder.BuildFromBackground(background, new RailRoiMaskBuilder.Options(21, 0.01, 0.85));

        Assert.Equal(roiMask.Rows * roiMask.Cols, Cv2.CountNonZero(roiMask));
    }
}
