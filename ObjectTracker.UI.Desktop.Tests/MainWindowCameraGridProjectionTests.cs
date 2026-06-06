using System.Linq;
using ObjectTracker.UI.Desktop;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class MainWindowCameraGridProjectionTests
{
    [Fact]
    public void CameraRenderMode_WhenExcluded_IsAlwaysRawEvenIfDebugEnabled()
    {
        var mode = MainWindow.GetCameraRenderMode(isIncludedInVisionPipeline: false, debugViewEnabled: true);

        Assert.Equal(MainWindow.CameraRenderMode.RawFeed, mode);
    }

    [Fact]
    public void CameraRenderMode_WhenIncludedAndDebugEnabled_IsDebug()
    {
        var mode = MainWindow.GetCameraRenderMode(isIncludedInVisionPipeline: true, debugViewEnabled: true);

        Assert.Equal(MainWindow.CameraRenderMode.DebugView, mode);
    }

    [Fact]
    public void CameraRenderMode_WhenVisionPipelineStopped_IsRawFeedEvenIfDebugEnabled()
    {
        var mode = MainWindow.GetCameraRenderMode(
            isIncludedInVisionPipeline: true,
            debugViewEnabled: true,
            isVisionPipelineRunning: false);

        Assert.Equal(MainWindow.CameraRenderMode.RawFeed, mode);
    }

    [Fact]
    public void CameraRenderMode_WhenIncludedAndDebugDisabled_IsLiveAnnotated()
    {
        var mode = MainWindow.GetCameraRenderMode(isIncludedInVisionPipeline: true, debugViewEnabled: false);

        Assert.Equal(MainWindow.CameraRenderMode.LiveAnnotated, mode);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void NormalizeDebugViewEnabled_RespectsVisionPipelineInclusion(bool included, bool debugRequested, bool expected)
    {
        Assert.Equal(expected, MainWindow.NormalizeDebugViewEnabled(included, debugRequested));
    }

    [Theory]
    [InlineData(MainWindow.CameraRenderMode.RawFeed, "RAW FEED")]
    [InlineData(MainWindow.CameraRenderMode.DebugView, "DEBUG VIEW")]
    [InlineData(MainWindow.CameraRenderMode.LiveAnnotated, "LIVE ANNOTATED")]
    public void CameraRenderModeBadge_ReturnsExpectedLabel(MainWindow.CameraRenderMode mode, string expected)
    {
        Assert.Equal(expected, MainWindow.GetCameraRenderModeBadge(mode));
    }

    [Fact]
    public void CameraGridProjection_OneVisibleCamera_ProducesOneTile()
    {
        var projection = MainWindow.BuildCameraGridProjection(new[]
        {
            new MainWindow.CameraWorkspaceCamera("cam-a", "Camera A", IsVisible: true, IsIncludedInVisionPipeline: true, DebugViewEnabled: false)
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
            new MainWindow.CameraWorkspaceCamera("cam-a", "Camera A", IsVisible: true, IsIncludedInVisionPipeline: true, DebugViewEnabled: false),
            new MainWindow.CameraWorkspaceCamera("cam-b", "Camera B", IsVisible: false, IsIncludedInVisionPipeline: true, DebugViewEnabled: false)
        });

        Assert.Equal(1, projection.VisibleCount);
        Assert.Equal(new[] { "cam-a" }, projection.Tiles.Select(tile => tile.CameraId).ToArray());
    }

    [Fact]
    public void CameraGridProjection_HiddenButIncludedCamera_IsNotRendered()
    {
        var projection = MainWindow.BuildCameraGridProjection(new[]
        {
            new MainWindow.CameraWorkspaceCamera("cam-a", "Camera A", IsVisible: false, IsIncludedInVisionPipeline: true, DebugViewEnabled: false)
        });

        Assert.Equal(0, projection.VisibleCount);
        Assert.Empty(projection.Tiles);
    }

    [Fact]
    public void CameraGridProjection_FollowsManualCameraListOrder()
    {
        var projection = MainWindow.BuildCameraGridProjection(new[]
        {
            new MainWindow.CameraWorkspaceCamera("cam-c", "Camera C", IsVisible: true, IsIncludedInVisionPipeline: true, DebugViewEnabled: false),
            new MainWindow.CameraWorkspaceCamera("cam-a", "Camera A", IsVisible: true, IsIncludedInVisionPipeline: true, DebugViewEnabled: false),
            new MainWindow.CameraWorkspaceCamera("cam-b", "Camera B", IsVisible: true, IsIncludedInVisionPipeline: true, DebugViewEnabled: false)
        });

        Assert.Equal(new[] { "cam-c", "cam-a", "cam-b" }, projection.Tiles.Select(tile => tile.CameraId).ToArray());
    }

    [Fact]
    public void CameraTileViewState_UsesProjectionOrderForTitlesAndVisibility()
    {
        var projection = MainWindow.BuildCameraGridProjection(new[]
        {
            new MainWindow.CameraWorkspaceCamera("cam-c", "Camera C", IsVisible: true, IsIncludedInVisionPipeline: true, DebugViewEnabled: false),
            new MainWindow.CameraWorkspaceCamera("cam-a", "Camera A", IsVisible: true, IsIncludedInVisionPipeline: true, DebugViewEnabled: false),
            new MainWindow.CameraWorkspaceCamera("cam-b", "Camera B", IsVisible: false, IsIncludedInVisionPipeline: true, DebugViewEnabled: false)
        });

        var viewState = MainWindow.BuildCameraTileViewState(projection);

        Assert.Equal(1, viewState.Rows);
        Assert.Equal(2, viewState.Columns);
        Assert.Equal("1. Camera C [LiveAnnotated]", viewState.Titles[0]);
        Assert.Equal("2. Camera A [LiveAnnotated]", viewState.Titles[1]);
        Assert.Equal(new[] { "cam-c", "cam-a" }, viewState.CameraIds.ToArray());
        Assert.Equal(new[] { MainWindow.CameraRenderMode.LiveAnnotated, MainWindow.CameraRenderMode.LiveAnnotated }, viewState.RenderModes.ToArray());
    }

    [Fact]
    public void CameraTileViewState_DoesNotCapVisibleTilesAtFour()
    {
        var projection = MainWindow.BuildCameraGridProjection(new[]
        {
            new MainWindow.CameraWorkspaceCamera("cam-1", "Camera 1", IsVisible: true, IsIncludedInVisionPipeline: true, DebugViewEnabled: false),
            new MainWindow.CameraWorkspaceCamera("cam-2", "Camera 2", IsVisible: true, IsIncludedInVisionPipeline: true, DebugViewEnabled: false),
            new MainWindow.CameraWorkspaceCamera("cam-3", "Camera 3", IsVisible: true, IsIncludedInVisionPipeline: true, DebugViewEnabled: false),
            new MainWindow.CameraWorkspaceCamera("cam-4", "Camera 4", IsVisible: true, IsIncludedInVisionPipeline: true, DebugViewEnabled: false),
            new MainWindow.CameraWorkspaceCamera("cam-5", "Camera 5", IsVisible: true, IsIncludedInVisionPipeline: true, DebugViewEnabled: false),
        });

        var viewState = MainWindow.BuildCameraTileViewState(projection);

        Assert.Equal(5, viewState.CameraIds.Count);
        Assert.Equal("5. Camera 5 [LiveAnnotated]", viewState.Titles[4]);
    }

    [Fact]
    public void CameraTileViewState_PreservesRenderModesPerVisibleCamera()
    {
        var projection = MainWindow.BuildCameraGridProjection(new[]
        {
            new MainWindow.CameraWorkspaceCamera("cam-a", "Camera A", IsVisible: true, IsIncludedInVisionPipeline: false, DebugViewEnabled: true),
            new MainWindow.CameraWorkspaceCamera("cam-b", "Camera B", IsVisible: true, IsIncludedInVisionPipeline: true, DebugViewEnabled: true),
            new MainWindow.CameraWorkspaceCamera("cam-c", "Camera C", IsVisible: true, IsIncludedInVisionPipeline: true, DebugViewEnabled: false)
        });

        var viewState = MainWindow.BuildCameraTileViewState(projection);

        Assert.Equal(new[]
        {
            MainWindow.CameraRenderMode.RawFeed,
            MainWindow.CameraRenderMode.DebugView,
            MainWindow.CameraRenderMode.LiveAnnotated
        }, viewState.RenderModes.ToArray());
    }

    [Fact]
    public void CameraGridProjection_WhenVisionPipelineStopped_RendersIncludedDebugCameraAsRawFeed()
    {
        var projection = MainWindow.BuildCameraGridProjection(new[]
        {
            new MainWindow.CameraWorkspaceCamera("usb:0:ANY", "USB Camera", IsVisible: true, IsIncludedInVisionPipeline: true, DebugViewEnabled: true)
        }, isVisionPipelineRunning: false);

        var viewState = MainWindow.BuildCameraTileViewState(projection);

        Assert.Equal(new[] { MainWindow.CameraRenderMode.RawFeed }, viewState.RenderModes.ToArray());
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
            .Select(index => new MainWindow.CameraWorkspaceCamera($"cam-{index}", $"Camera {index}", IsVisible: true, IsIncludedInVisionPipeline: true, DebugViewEnabled: false))
            .ToArray();

        var projection = MainWindow.BuildCameraGridProjection(cameras);

        Assert.Equal(visibleCount, projection.VisibleCount);
        Assert.Equal(expectedRows, projection.Rows);
        Assert.Equal(expectedColumns, projection.Columns);
    }
}
