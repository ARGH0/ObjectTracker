using System.Linq;
using ObjectTracker.UI.Desktop;
using ObjectTracker.UI.Desktop.Enums;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class MainWindowCameraGridProjectionTests
{
    /// <summary>
    /// <description>Feature: MainWindow.GetCameraRenderMode excludes a camera from pipeline rendering when it is not included in the Vision Pipeline.
    /// 
    ///   Scenario: A camera that is excluded from the Vision Pipeline should always render as raw feed regardless of debug view settings.
    ///     Given GetCameraRenderMode is called with isIncludedInVisionPipeline=false and debugViewEnabled=true,
    ///     Then the result should be CameraRenderMode.RawFeed.</description>
    /// </summary>
    [Fact]
    public void CameraRenderMode_WhenExcluded_IsAlwaysRawEvenIfDebugEnabled()
    {
        var mode = MainWindow.GetCameraRenderMode(isIncludedInVisionPipeline: false, debugViewEnabled: true);

        Assert.Equal(CameraRenderMode.RawFeed, mode);
    }

    /// <summary>
    /// <description>Feature: MainWindow.GetCameraRenderMode returns debug view mode when a camera is included and debug view is enabled.
    /// 
    ///   Scenario: A camera that is included in the Vision Pipeline with debug view enabled should render as debug.
    ///     Given GetCameraRenderMode is called with isIncludedInVisionPipeline=true and debugViewEnabled=true,
    ///     Then the result should be CameraRenderMode.DebugView.</description>
    /// </summary>
    [Fact]
    public void CameraRenderMode_WhenIncludedAndDebugEnabled_IsDebug()
    {
        var mode = MainWindow.GetCameraRenderMode(isIncludedInVisionPipeline: true, debugViewEnabled: true);

        Assert.Equal(CameraRenderMode.DebugView, mode);
    }

    /// <summary>
    /// <description>Feature: MainWindow.GetCameraRenderMode falls back to raw feed when the Vision Pipeline is stopped even if the camera is included and debug view is enabled.
    /// 
    ///   Scenario: A camera that is included in the Vision Pipeline but the pipeline itself has stopped should render as raw feed.
    ///     Given GetCameraRenderMode is called with isIncludedInVisionPipeline=true, debugViewEnabled=true, and isVisionPipelineRunning=false,
    ///     Then the result should be CameraRenderMode.RawFeed.</description>
    /// </summary>
    [Fact]
    public void CameraRenderMode_WhenVisionPipelineStopped_IsRawFeedEvenIfDebugEnabled()
    {
        var mode = MainWindow.GetCameraRenderMode(
            isIncludedInVisionPipeline: true,
            debugViewEnabled: true,
            isVisionPipelineRunning: false);

        Assert.Equal(CameraRenderMode.RawFeed, mode);
    }

    /// <summary>
    /// <description>Feature: MainWindow.GetCameraRenderMode returns live annotated mode when a camera is included and debug view is disabled.
    /// 
    ///   Scenario: A camera that is included in the Vision Pipeline with debug view disabled should render as live annotated.
    ///     Given GetCameraRenderMode is called with isIncludedInVisionPipeline=true and debugViewEnabled=false,
    ///     Then the result should be CameraRenderMode.LiveAnnotated.</description>
    /// </summary>
    [Fact]
    public void CameraRenderMode_WhenIncludedAndDebugDisabled_IsLiveAnnotated()
    {
        var mode = MainWindow.GetCameraRenderMode(isIncludedInVisionPipeline: true, debugViewEnabled: false);

        Assert.Equal(CameraRenderMode.LiveAnnotated, mode);
    }

    /// <summary>
    /// <description>Feature: MainWindow.NormalizeDebugViewEnabled respects Vision Pipeline inclusion when computing effective debug view state.
    /// 
    ///   Scenario: Debug view is only enabled when the camera source is included in the Vision Pipeline, regardless of user request.
    ///     Given NormalizeDebugViewEnabled is called with various combinations of included and debugRequested values,
    ///     Then the result should be true only when both included and debugRequested are true,
    ///      And false otherwise.</description>
    /// </summary>
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void NormalizeDebugViewEnabled_RespectsVisionPipelineInclusion(bool included, bool debugRequested, bool expected)
    {
        Assert.Equal(expected, MainWindow.NormalizeDebugViewEnabled(included, debugRequested));
    }

    /// <summary>
    /// <description>Feature: MainWindow.GetCameraRenderModeBadge returns the expected label text for each render mode.
    /// 
    ///   Scenario: Each CameraRenderMode enum value should map to its human-readable badge string.
    ///     Given GetCameraRenderModeBadge is called with RawFeed, DebugView, and LiveAnnotated values respectively,
    ///     Then the results should be "RAW FEED", "DEBUG VIEW", and "LIVE ANNOTATED".</description>
    /// </summary>
    [Theory]
    [InlineData(CameraRenderMode.RawFeed, "RAW FEED")]
    [InlineData(CameraRenderMode.DebugView, "DEBUG VIEW")]
    [InlineData(CameraRenderMode.LiveAnnotated, "LIVE ANNOTATED")]
    public void CameraRenderModeBadge_ReturnsExpectedLabel(CameraRenderMode mode, string expected)
    {
        Assert.Equal(expected, MainWindow.GetCameraRenderModeBadge(mode));
    }

    /// <summary>
    /// <description>Feature: MainWindow.BuildCameraGridProjection produces a single tile for one visible camera.
    /// 
    ///   Scenario: Building a grid projection with exactly one visible and included camera should produce one tile.
    ///     Given BuildCameraGridProjection is called with one CameraWorkspaceCamera "cam-a" that is visible and included,
    ///     Then VisibleCount should be 1, Rows should be 1, Columns should be 1,
    ///      And the tiles should contain only camera ID "cam-a".</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: MainWindow.BuildCameraGridProjection excludes hidden cameras from the projection.
    /// 
    ///   Scenario: Building a grid with one visible camera and one hidden camera should produce only one tile for the visible camera.
    ///     Given BuildCameraGridProjection is called with "cam-a" (visible, included) and "cam-b" (hidden, included),
    ///     Then VisibleCount should be 1,
    ///      And tiles should contain only camera ID "cam-a".</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: MainWindow.BuildCameraGridProjection produces no tiles when the only camera is hidden.
    /// 
    ///   Scenario: Building a grid with one camera that is not visible should produce zero tiles.
    ///     Given BuildCameraGridProjection is called with "cam-a" (hidden, included),
    ///     Then VisibleCount should be 0 and Tiles should be empty.</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: MainWindow.BuildCameraGridProjection follows the manual camera list order.
    /// 
    ///   Scenario: Building a grid with cameras in a specific order should produce tiles in that same order.
    ///     Given BuildCameraGridProjection is called with "cam-c", "cam-a", and "cam-b" all visible and included,
    ///     Then the tile camera IDs should be ["cam-c", "cam-a", "cam-b"].</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: MainWindow.BuildCameraTileViewState uses projection order for titles and visibility.
    /// 
    ///   Scenario: Building a tile view state from a grid projection should produce correct titles with render mode suffixes and camera IDs in the right order, excluding hidden cameras.
    ///     Given BuildCameraGridProjection is called with "cam-c" (visible), "cam-a" (visible), and "cam-b" (hidden), all included,
    ///      And BuildCameraTileViewState is called on that projection,
    ///     Then Rows should be 1, Columns should be 2,
    ///      And Titles[0] should be "1. Camera C [LiveAnnotated]", Titles[1] should be "2. Camera A [LiveAnnotated]",
    ///      And CameraIds should be ["cam-c", "cam-a"],
    ///      And RenderModes should be [LiveAnnotated, LiveAnnotated].</description>
    /// </summary>
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
        Assert.Equal(new[] { CameraRenderMode.LiveAnnotated, CameraRenderMode.LiveAnnotated }, viewState.RenderModes.ToArray());
    }

    /// <summary>
    /// <description>Feature: MainWindow.BuildCameraTileViewState does not cap visible tiles at four.
    /// 
    ///   Scenario: Building a tile view state with five visible cameras should produce five camera IDs in the view state.
    ///     Given BuildCameraGridProjection is called with 5 visible and included cameras,
    ///      And BuildCameraTileViewState is called on that projection,
    ///     Then CameraIds.Count should be 5,
    ///      And Titles[4] should be "5. Camera 5 [LiveAnnotated]".</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: MainWindow.BuildCameraTileViewState preserves render modes per visible camera.
    /// 
    ///   Scenario: Building a tile view state with cameras having different inclusion and debug settings should produce matching render modes in order.
    ///     Given BuildCameraGridProjection is called with "cam-a" (excluded, debug enabled), "cam-b" (included, debug enabled), and "cam-c" (included, debug disabled), all visible,
    ///      And BuildCameraTileViewState is called on that projection,
    ///     Then RenderModes should be [RawFeed, DebugView, LiveAnnotated].</description>
    /// </summary>
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
            CameraRenderMode.RawFeed,
            CameraRenderMode.DebugView,
            CameraRenderMode.LiveAnnotated
        }, viewState.RenderModes.ToArray());
    }

    /// <summary>
    /// <description>Feature: MainWindow.BuildCameraTileFrameRouting uses PipelineSnapshotAnnotatedFrame for visible, included cameras when the Vision Pipeline is running.
    /// 
    ///   Scenario: Building a frame routing with one visible and included camera while the pipeline runs should route to annotated frames.
    ///     Given BuildCameraGridProjection is called with "cam-a" (visible, included) with isVisionPipelineRunning=true,
    ///      And BuildCameraTileFrameRouting is called on that projection,
    ///     Then exactly one route should exist for "cam-a" with FrameSource PipelineSnapshotAnnotatedFrame.</description>
    /// </summary>
    [Fact]
    public void CameraTileFrameRouting_WhenVisibleIncludedAndRunning_UsesPipelineSnapshotAnnotatedFrame()
    {
        var projection = MainWindow.BuildCameraGridProjection(new[]
        {
            new MainWindow.CameraWorkspaceCamera("cam-a", "Camera A", IsVisible: true, IsIncludedInVisionPipeline: true, DebugViewEnabled: false)
        }, isVisionPipelineRunning: true);

        var routing = MainWindow.BuildCameraTileFrameRouting(projection);

        Assert.Collection(
            routing.Routes,
            route =>
            {
                Assert.Equal("cam-a", route.CameraId);
                Assert.Equal(CameraTileFrameSource.PipelineSnapshotAnnotatedFrame, route.FrameSource);
            });
    }

    /// <summary>
    /// <description>Feature: MainWindow.BuildCameraTileFrameRouting uses PipelineSnapshotDebugFrames when debug view is enabled on a visible, included camera.
    /// 
    ///   Scenario: Building a frame routing with one visible and included camera with debug enabled while the pipeline runs should route to debug frames.
    ///     Given BuildCameraGridProjection is called with "cam-a" (visible, included, debugEnabled=true) with isVisionPipelineRunning=true,
    ///      And BuildCameraTileFrameRouting is called on that projection,
    ///     Then exactly one route should exist for "cam-a" with FrameSource PipelineSnapshotDebugFrames.</description>
    /// </summary>
    [Fact]
    public void CameraTileFrameRouting_WhenDebugViewVisibleIncludedAndRunning_UsesPipelineSnapshotDebugFrames()
    {
        var projection = MainWindow.BuildCameraGridProjection(new[]
        {
            new MainWindow.CameraWorkspaceCamera("cam-a", "Camera A", IsVisible: true, IsIncludedInVisionPipeline: true, DebugViewEnabled: true)
        }, isVisionPipelineRunning: true);

        var routing = MainWindow.BuildCameraTileFrameRouting(projection);

        Assert.Collection(
            routing.Routes,
            route => Assert.Equal(CameraTileFrameSource.PipelineSnapshotDebugFrames, route.FrameSource));
    }

    /// <summary>
    /// <description>Feature: MainWindow.BuildCameraTileFrameRouting uses RawCameraSourceFeed for excluded or stopped cameras.
    /// 
    ///   Scenario: Building a frame routing with various combinations of exclusion and pipeline stop states should route to raw feed when the camera is not included or the pipeline is stopped.
    ///     Given BuildCameraGridProjection is called with "cam-a" under different (included, debugEnabled, pipelineRunning) combinations,
    ///      And BuildCameraTileFrameRouting is called on that projection,
    ///     Then FrameSource should be RawCameraSourceFeed when: not included and debug requested, not included and debug disabled, or included but pipeline stopped.
    /// </summary>
    [Theory]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    public void CameraTileFrameRouting_WhenVisibleTileIsExcludedOrStopped_UsesRawCameraSourceFeed(
        bool isIncludedInVisionPipeline,
        bool debugViewEnabled,
        bool isVisionPipelineRunning)
    {
        var projection = MainWindow.BuildCameraGridProjection(new[]
        {
            new MainWindow.CameraWorkspaceCamera("cam-a", "Camera A", IsVisible: true, isIncludedInVisionPipeline, debugViewEnabled)
        }, isVisionPipelineRunning);

        var routing = MainWindow.BuildCameraTileFrameRouting(projection);

        Assert.Collection(
            routing.Routes,
            route => Assert.Equal(CameraTileFrameSource.RawCameraSourceFeed, route.FrameSource));
    }

    /// <summary>
    /// <description>Feature: MainWindow.BuildCameraTileFrameRouting produces no routes for hidden cameras even when included.
    /// 
    ///   Scenario: A camera that is not visible but is included in the Vision Pipeline should have zero tile routes.
    ///     Given BuildCameraGridProjection is called with "cam-a" (hidden, included) with isVisionPipelineRunning=true,
    ///      And BuildCameraTileFrameRouting is called on that projection,
    ///     Then routing.Routes should be empty.</description>
    /// </summary>
    [Fact]
    public void CameraTileFrameRouting_WhenCameraSourceIsHiddenAndIncluded_HasNoTileRoute()
    {
        var projection = MainWindow.BuildCameraGridProjection(new[]
        {
            new MainWindow.CameraWorkspaceCamera("cam-a", "Camera A", IsVisible: false, IsIncludedInVisionPipeline: true, DebugViewEnabled: true)
        }, isVisionPipelineRunning: true);

        var routing = MainWindow.BuildCameraTileFrameRouting(projection);

        Assert.Empty(routing.Routes);
    }

    /// <summary>
    /// <description>Feature: MainWindow.BuildCameraTileFrameRouting covers all camera visibility, inclusion, running state, and debug view combinations.
    /// 
    ///   Scenario: Iterating through every combination of isVisible, isIncludedInVisionPipeline, isVisionPipelineRunning, and debugViewEnabled should produce correct routing for each case.
    ///     Given BuildCameraGridProjection is called with "cam-a" under all 16 boolean combinations,
    ///      And BuildCameraTileFrameRouting is called on each projection,
    ///     Then when isVisible=false, Routes should be empty,
    ///      When isVisible=true and isIncludedInVisionPipeline=true and isVisionPipelineRunning=true: FrameSource should be PipelineSnapshotDebugFrames if debugViewEnabled=true, else PipelineSnapshotAnnotatedFrame,
    ///      Otherwise (excluded or stopped pipeline): FrameSource should be RawCameraSourceFeed.</description>
    /// </summary>
    [Fact]
    public void CameraTileFrameRouting_CoversCameraVisibilityInclusionRunningAndDebugViewCombinations()
    {
        var combinations = from isVisible in new[] { false, true }
                           from isIncludedInVisionPipeline in new[] { false, true }
                           from isVisionPipelineRunning in new[] { false, true }
                           from debugViewEnabled in new[] { false, true }
                           select new { isVisible, isIncludedInVisionPipeline, isVisionPipelineRunning, debugViewEnabled };

        foreach (var combination in combinations)
        {
            var projection = MainWindow.BuildCameraGridProjection(new[]
            {
                new MainWindow.CameraWorkspaceCamera(
                    "cam-a",
                    "Camera A",
                    combination.isVisible,
                    combination.isIncludedInVisionPipeline,
                    combination.debugViewEnabled)
            }, combination.isVisionPipelineRunning);

            var routing = MainWindow.BuildCameraTileFrameRouting(projection);

            if (!combination.isVisible)
            {
                Assert.Empty(routing.Routes);
                continue;
            }

            var route = Assert.Single(routing.Routes);
            var expected = combination.isIncludedInVisionPipeline && combination.isVisionPipelineRunning
                ? combination.debugViewEnabled
                    ? CameraTileFrameSource.PipelineSnapshotDebugFrames
                    : CameraTileFrameSource.PipelineSnapshotAnnotatedFrame
                : CameraTileFrameSource.RawCameraSourceFeed;
            Assert.Equal(expected, route.FrameSource);
        }
    }

    /// <summary>
    /// <description>Feature: MainWindow.BuildCameraTileViewState renders included debug cameras as raw feed when the Vision Pipeline is stopped.
    /// 
    ///   Scenario: Building a tile view state with an included USB camera that has debug enabled while the pipeline is stopped should produce RawFeed mode.
    ///     Given BuildCameraGridProjection is called with "usb:0:ANY" (visible, included, debugEnabled=true) with isVisionPipelineRunning=false,
    ///      And BuildCameraTileViewState is called on that projection,
    ///     Then RenderModes should be [RawFeed].</description>
    /// </summary>
    [Fact]
    public void CameraGridProjection_WhenVisionPipelineStopped_RendersIncludedDebugCameraAsRawFeed()
    {
        var projection = MainWindow.BuildCameraGridProjection(new[]
        {
            new MainWindow.CameraWorkspaceCamera("usb:0:ANY", "USB Camera", IsVisible: true, IsIncludedInVisionPipeline: true, DebugViewEnabled: true)
        }, isVisionPipelineRunning: false);

        var viewState = MainWindow.BuildCameraTileViewState(projection);

        Assert.Equal(new[] { CameraRenderMode.RawFeed }, viewState.RenderModes.ToArray());
    }

    /// <summary>
    /// <description>Feature: MainWindow.BuildCameraGridProjection refines grid dimensions based on visible camera count.
    /// 
    ///   Scenario: Building a grid projection with varying numbers of visible cameras should produce correct row and column counts.
    ///     Given BuildCameraGridProjection is called with N visible cameras (N = 0, 1, 2, 3, 4, 5),
    ///      And the expected rows and columns are [0,0], [1,1], [1,2], [2,2], [2,2], [2,3] respectively,
    ///     Then VisibleCount should equal N for each case,
    ///      And Rows should match the expected value,
    ///      And Columns should match the expected value.</description>
    /// </summary>
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
