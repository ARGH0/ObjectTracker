using OpenCvSharp;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class RailRoiMaskBuilderTests
{
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

    [Fact]
    public void BuildFromBackground_FallsBackToFullFrame_WhenNoRailSeedIsFound()
    {
        using var background = new Mat(new Size(80, 80), MatType.CV_8UC1, Scalar.All(128));

        var builder = new RailRoiMaskBuilder();
        using var roiMask = builder.BuildFromBackground(background, new RailRoiMaskBuilder.Options(21, 0.01, 0.85));

        Assert.Equal(roiMask.Rows * roiMask.Cols, Cv2.CountNonZero(roiMask));
    }
}
