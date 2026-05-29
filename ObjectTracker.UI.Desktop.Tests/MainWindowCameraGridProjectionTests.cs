using System.Linq;
using ObjectTracker.UI.Desktop;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class MainWindowCameraGridProjectionTests
{
    [Fact]
    public void CameraGridProjection_OneVisibleCamera_ProducesOneTile()
    {
        var projection = MainWindow.BuildCameraGridProjection(new[]
        {
            new MainWindow.CameraWorkspaceCamera("cam-a", "Camera A", IsVisible: true)
        });

        Assert.Equal(1, projection.VisibleCount);
        Assert.Equal(1, projection.Rows);
        Assert.Equal(1, projection.Columns);
        Assert.Equal(new[] { "cam-a" }, projection.Tiles.Select(tile => tile.CameraId).ToArray());
    }

    [Fact]
    public void CameraGridProjection_HiddenCamera_IsExcluded()
    {
        var projection = MainWindow.BuildCameraGridProjection(new[]
        {
            new MainWindow.CameraWorkspaceCamera("cam-a", "Camera A", IsVisible: true),
            new MainWindow.CameraWorkspaceCamera("cam-b", "Camera B", IsVisible: false)
        });

        Assert.Equal(1, projection.VisibleCount);
        Assert.Equal(new[] { "cam-a" }, projection.Tiles.Select(tile => tile.CameraId).ToArray());
    }

    [Fact]
    public void CameraGridProjection_FollowsManualCameraListOrder()
    {
        var projection = MainWindow.BuildCameraGridProjection(new[]
        {
            new MainWindow.CameraWorkspaceCamera("cam-c", "Camera C", IsVisible: true),
            new MainWindow.CameraWorkspaceCamera("cam-a", "Camera A", IsVisible: true),
            new MainWindow.CameraWorkspaceCamera("cam-b", "Camera B", IsVisible: true)
        });

        Assert.Equal(new[] { "cam-c", "cam-a", "cam-b" }, projection.Tiles.Select(tile => tile.CameraId).ToArray());
    }

    [Fact]
    public void CameraTileViewState_UsesProjectionOrderForTitlesAndVisibility()
    {
        var projection = MainWindow.BuildCameraGridProjection(new[]
        {
            new MainWindow.CameraWorkspaceCamera("cam-c", "Camera C", IsVisible: true),
            new MainWindow.CameraWorkspaceCamera("cam-a", "Camera A", IsVisible: true),
            new MainWindow.CameraWorkspaceCamera("cam-b", "Camera B", IsVisible: false)
        });

        var viewState = MainWindow.BuildCameraTileViewState(projection);

        Assert.Equal(1, viewState.Rows);
        Assert.Equal(2, viewState.Columns);
        Assert.Equal("1. Camera C", viewState.Titles[0]);
        Assert.Equal("2. Camera A", viewState.Titles[1]);
        Assert.True(viewState.VisibleSlots[0]);
        Assert.True(viewState.VisibleSlots[1]);
        Assert.False(viewState.VisibleSlots[2]);
        Assert.False(viewState.VisibleSlots[3]);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 1, 1)]
    [InlineData(2, 1, 2)]
    [InlineData(3, 2, 2)]
    [InlineData(4, 2, 2)]
    [InlineData(5, 2, 3)]
    public void CameraGridProjection_ReflowsByVisibleCameraCount(int visibleCount, int expectedRows, int expectedColumns)
    {
        var cameras = Enumerable
            .Range(1, visibleCount)
            .Select(index => new MainWindow.CameraWorkspaceCamera($"cam-{index}", $"Camera {index}", IsVisible: true))
            .ToArray();

        var projection = MainWindow.BuildCameraGridProjection(cameras);

        Assert.Equal(visibleCount, projection.VisibleCount);
        Assert.Equal(expectedRows, projection.Rows);
        Assert.Equal(expectedColumns, projection.Columns);
    }
}
