using ObjectTracker.UI.Desktop.Region.Contracts;
using ObjectTracker.UI.Desktop.Region.Implementation;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class CoordinateMapperTests
{
    [Fact]
    public void Map_PixelAtOrigin_MapsToCellZeroZero()
    {
        var mapper = new CoordinateMapper();

        var result = mapper.Map(0f, 0.1f, 0f, 0.1f, 64, 48);

        Assert.Equal(0, result.Column);
        Assert.Equal(0, result.Row);
    }

    [Fact]
    public void Map_PixelAtLastValidPosition_MapsToLastCell()
    {
        var mapper = new CoordinateMapper();

        var result = mapper.Map(639.9f, 0.1f, 479.9f, 0.1f, 64, 48);

        Assert.Equal(63, result.Column);
        Assert.Equal(47, result.Row);
    }

    [Fact]
    public void Map_NormalMidCellCoordinate_MapsToCorrectCell()
    {
        var mapper = new CoordinateMapper();

        var result = mapper.Map(100f, 0.1f, 200f, 0.1f, 64, 48);

        Assert.Equal(10, result.Column);
        Assert.Equal(20, result.Row);
    }

    [Fact]
    public void Map_PixelAtExactCellBoundary_FloorProducesLowerCell()
    {
        var mapper = new CoordinateMapper();

        var result = mapper.Map(50f, 0.1f, 30f, 0.1f, 64, 48);

        Assert.Equal(5, result.Column);
        Assert.Equal(3, result.Row);
    }
}
