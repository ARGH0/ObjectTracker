using System.Linq;
using ObjectTracker.UI.Desktop;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class MainWindowCameraGridProjectionTests
{
    [Fact]
    public void FeedKind_WhenExcluded_IsAlwaysRawEvenIfDebugEnabled()
    {
        var kind = MainWindow.GetFeedKind(isIncludedInVisionPipeline: false, debugViewEnabled: true);

        Assert.Equal(MainWindow.FeedKind.RawFeed, kind);
    }

    [Fact]
    public void FeedKind_WhenIncludedAndDebugEnabled_IsDebug()
    {
        var kind = MainWindow.GetFeedKind(isIncludedInVisionPipeline: true, debugViewEnabled: true);

        Assert.Equal(MainWindow.FeedKind.DebugView, kind);
    }

    [Fact]
    public void FeedKind_WhenIncludedAndDebugDisabled_IsLiveAnnotated()
    {
        var kind = MainWindow.GetFeedKind(isIncludedInVisionPipeline: true, debugViewEnabled: false);

        Assert.Equal(MainWindow.FeedKind.LiveAnnotated, kind);
    }

    [Fact]
    public void TileLayout_DebugView_YieldsMultiImage2x2()
    {
        var layout = MainWindow.GetTileLayout(MainWindow.FeedKind.DebugView);

        Assert.Equal(MainWindow.TileLayout.MultiImage2x2, layout);
    }

    [Fact]
    public void TileLayout_NonDebugView_YieldsSingleImage()
    {
        Assert.Equal(MainWindow.TileLayout.SingleImage, MainWindow.GetTileLayout(MainWindow.FeedKind.RawFeed));
        Assert.Equal(MainWindow.TileLayout.SingleImage, MainWindow.GetTileLayout(MainWindow.FeedKind.LiveAnnotated));
    }

    [Theory]
    [InlineData(MainWindow.FeedKind.RawFeed, "RAW FEED")]
    [InlineData(MainWindow.FeedKind.DebugView, "DEBUG VIEW")]
    [InlineData(MainWindow.FeedKind.LiveAnnotated, "LIVE ANNOTATED")]
    public void FeedKindBadge_ReturnsExpectedLabel(MainWindow.FeedKind kind, string expected)
    {
        Assert.Equal(expected, MainWindow.GetFeedKindBadge(kind));
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
        Assert.Equal(new[] { MainWindow.FeedKind.LiveAnnotated, MainWindow.FeedKind.LiveAnnotated }, viewState.FeedKinds.ToArray());
        Assert.Equal(new[] { MainWindow.TileLayout.SingleImage, MainWindow.TileLayout.SingleImage }, viewState.Layouts.ToArray());
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
    public void CameraTileViewState_PreservesFeedKindsAndLayoutsPerVisibleCamera()
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
            MainWindow.FeedKind.RawFeed,
            MainWindow.FeedKind.DebugView,
            MainWindow.FeedKind.LiveAnnotated
        }, viewState.FeedKinds.ToArray());

        Assert.Equal(new[]
        {
            MainWindow.TileLayout.SingleImage,
            MainWindow.TileLayout.MultiImage2x2,
            MainWindow.TileLayout.SingleImage
        }, viewState.Layouts.ToArray());
    }

    [Fact]
    public void CameraGridProjection_CorrectlyDerivesLayoutFromFeedKind()
    {
        var projection = MainWindow.BuildCameraGridProjection(new[]
        {
            new MainWindow.CameraWorkspaceCamera("cam-raw", "Raw Camera", IsVisible: true, IsIncludedInVisionPipeline: false, DebugViewEnabled: false),
            new MainWindow.CameraWorkspaceCamera("cam-debug", "Debug Camera", IsVisible: true, IsIncludedInVisionPipeline: true, DebugViewEnabled: true)
        });

        Assert.Equal(MainWindow.TileLayout.SingleImage, projection.Tiles[0].Layout);
        Assert.Equal(MainWindow.TileLayout.MultiImage2x2, projection.Tiles[1].Layout);
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
