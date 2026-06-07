using ObjectTracker.UI.Desktop;
using ObjectTracker.UI.Desktop.Enums;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class CameraTileDisplayProjectionTests
{
    /// <summary>
    /// <description>Feature: CameraTileDisplayProjection.Build handles the case when no frame has arrived yet.
    /// 
    ///   Scenario: The tile is waiting for its first frame with no last frame to repeat.
    ///     Given Build is called with hasCurrentFrame false and hasLastFrame false,
    ///     Then MissingFrameBehavior should be RepeatLastFrame,
    ///      And FrameDisplay should be CameraTileFrameDisplay.Placeholder,
    ///      And PlaceholderText should be "Waiting for first frame".</description>
    /// </summary>
    [Fact]
    public void Build_WhenNoFrameHasArrived_UsesDefaultRepeatLastFrameBehaviorAndShowsPlaceholder()
    {
        var view = CameraTileDisplayProjection.Build(
            hasCurrentFrame: false,
            hasLastFrame: false);

        Assert.Equal(CameraTileMissingFrameBehavior.RepeatLastFrame, view.MissingFrameBehavior);
        Assert.Equal(CameraTileFrameDisplay.Placeholder, view.FrameDisplay);
        Assert.Equal("Waiting for first frame", view.PlaceholderText);
    }

    /// <summary>
    /// <description>Feature: CameraTileDisplayProjection.Build applies the configured missing frame behavior when a previous frame exists.
    /// 
    ///   Scenario: A frame is missing but there is a last frame available, and the configured missing frame behavior determines what is displayed.
    ///     Given Build is called with hasCurrentFrame false, hasLastFrame true, and a specific CameraTileMissingFrameBehavior,
    ///     Then FrameDisplay should match the expected display for that behavior (RepeatLastFrame or BlackFrame).
    /// </summary>
    [Theory]
    [InlineData(CameraTileMissingFrameBehavior.RepeatLastFrame, CameraTileFrameDisplay.LastFrame)]
    [InlineData(CameraTileMissingFrameBehavior.BlackFrame, CameraTileFrameDisplay.BlackFrame)]
    public void Build_WhenFrameIsMissingAfterPreviousFrame_AppliesConfiguredMissingFrameBehavior(
        CameraTileMissingFrameBehavior missingFrameBehavior,
        CameraTileFrameDisplay expectedDisplay)
    {
        var view = CameraTileDisplayProjection.Build(
            hasCurrentFrame: false,
            hasLastFrame: true,
            missingFrameBehavior);

        Assert.Equal(expectedDisplay, view.FrameDisplay);
    }

    /// <summary>
    /// <description>Feature: CameraTileDisplayProjection.Build shows camera source status overlay regardless of missing frame behavior.
    /// 
    ///   Scenario: The camera source is stale or failed, the overlay should be shown for both cases regardless of configured missing frame behavior.
    ///     Given Build is called with hasCurrentFrame false, hasLastFrame true, a CameraTileMissingFrameBehavior, and a CameraSourceStatus that is either Stale or Failed,
    ///     Then ShowCameraSourceStatusOverlay should be true for both stale and failed states.</description>
    /// </summary>
    [Theory]
    [InlineData(CameraTileMissingFrameBehavior.RepeatLastFrame)]
    [InlineData(CameraTileMissingFrameBehavior.BlackFrame)]
    public void Build_WhenCameraSourceIsStaleOrFailed_ShowsCameraSourceStatusOverlayRegardlessOfMissingFrameBehavior(
        CameraTileMissingFrameBehavior missingFrameBehavior)
    {
        var stale = CameraTileDisplayProjection.Build(
            hasCurrentFrame: false,
            hasLastFrame: true,
            missingFrameBehavior,
            cameraSourceStatus: new CameraSourceStatus(CameraSourceStatusState.Stale, LatestFrameVersion: 4, FailureMessage: null));
        var failed = CameraTileDisplayProjection.Build(
            hasCurrentFrame: false,
            hasLastFrame: true,
            missingFrameBehavior,
            cameraSourceStatus: new CameraSourceStatus(CameraSourceStatusState.Failed, LatestFrameVersion: 4, FailureMessage: "camera unavailable"));

        Assert.True(stale.ShowCameraSourceStatusOverlay);
        Assert.True(failed.ShowCameraSourceStatusOverlay);
    }

    /// <summary>
    /// <description>Feature: CameraTileDisplayProjection.Build shows lane status overlay for snapshot tile modes when the Vision Pipeline Lane is stale or failed.
    /// 
    ///   Scenario: The Vision Pipeline lane processing is stale or failed, the overlay should be shown for both PipelineSnapshotAnnotatedFrame and PipelineSnapshotDebugFrames frame sources regardless of missing frame behavior.
    ///     Given Build is called with hasCurrentFrame false, hasLastFrame true, a CameraTileFrameSource that is either PipelineSnapshotAnnotatedFrame or PipelineSnapshotDebugFrames, and a VisionPipelineLaneStatus that is either Stale or Failed,
    ///     Then ShowVisionPipelineLaneStatusOverlay should be true for both stale and failed states.</description>
    /// </summary>
    [Theory]
    [InlineData(CameraTileFrameSource.PipelineSnapshotAnnotatedFrame)]
    [InlineData(CameraTileFrameSource.PipelineSnapshotDebugFrames)]
    public void Build_WhenVisionPipelineLaneIsStaleOrFailed_ShowsLaneStatusOverlayForSnapshotTileModes(
        CameraTileFrameSource frameSource)
    {
        var stale = CameraTileDisplayProjection.Build(
            hasCurrentFrame: false,
            hasLastFrame: true,
            frameSource: frameSource,
            visionPipelineLaneStatus: new VisionPipelineLaneStatus(VisionPipelineLaneStatusState.Stale, LatestSnapshotVersion: 4, FailureMessage: null));
        var failed = CameraTileDisplayProjection.Build(
            hasCurrentFrame: false,
            hasLastFrame: true,
            frameSource: frameSource,
            visionPipelineLaneStatus: new VisionPipelineLaneStatus(VisionPipelineLaneStatusState.Failed, LatestSnapshotVersion: 4, FailureMessage: "mask missing"));

        Assert.True(stale.ShowVisionPipelineLaneStatusOverlay);
        Assert.True(failed.ShowVisionPipelineLaneStatusOverlay);
    }
}
